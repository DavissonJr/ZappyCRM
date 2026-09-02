using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WhatsappCrmIA.Application.Interfaces;
using WhatsappCrmIA.Domain.Entities;
using WhatsappCrmIA.Domain.Enums;

namespace WhatsappCrmIA.Application.UseCases.Messaging;

/// <summary>
/// Fluxo central do produto: mensagem chega do webhook da Evolution API,
/// é persistida, a IA gera (ou sugere) uma resposta — podendo até criar um
/// agendamento de verdade — e opcionalmente já dispara o envio de volta.
/// </summary>
public record ProcessIncomingMessageCommand(
    Guid TenantId,
    string InstanceName,
    string FromPhoneNumber,
    string ContactName,
    string MessageText,
    string WhatsAppMessageId
) : IRequest<ProcessIncomingMessageResult>;

public record ProcessIncomingMessageResult(bool AutoReplied, string? ReplyText);

public class ProcessIncomingMessageHandler
    : IRequestHandler<ProcessIncomingMessageCommand, ProcessIncomingMessageResult>
{
    // Preço aproximado por milhão de tokens (modelo Sonnet). Ajuste aqui se
    // trocar de modelo ou se os preços da Anthropic mudarem.
    private const decimal InputCostPerMillionTokens = 3.00m;
    private const decimal OutputCostPerMillionTokens = 15.00m;

    // Lembretes padrão quando a IA cria um agendamento sozinha (sem passar
    // pela tela de criação manual, onde o atendente escolhe isso).
    private static readonly int[] DefaultReminderOffsetMinutes = [1440, 180]; // 1 dia e 3h antes

    private readonly IApplicationDbContext _db;
    private readonly IAiAgentService _aiAgent;
    private readonly IWhatsAppGateway _whatsApp;
    private readonly ILogger<ProcessIncomingMessageHandler> _logger;
    private readonly INotificationService _notifications;
    private readonly IGlobalSecretsProvider _globalSecrets;
    private readonly IReminderScheduler _reminderScheduler;

    public ProcessIncomingMessageHandler(
        IApplicationDbContext db,
        IAiAgentService aiAgent,
        IWhatsAppGateway whatsApp,
        ILogger<ProcessIncomingMessageHandler> logger,
        INotificationService notifications,
        IGlobalSecretsProvider globalSecrets,
        IReminderScheduler reminderScheduler)
    {
        _db = db;
        _aiAgent = aiAgent;
        _whatsApp = whatsApp;
        _logger = logger;
        _notifications = notifications;
        _globalSecrets = globalSecrets;
        _reminderScheduler = reminderScheduler;
    }

    public async Task<ProcessIncomingMessageResult> Handle(
        ProcessIncomingMessageCommand request, CancellationToken ct)
    {
        // 0. Resolve qual número da empresa recebeu essa mensagem
        // IMPORTANTE: IgnoreQueryFilters() é necessário aqui porque o webhook não tem
        // usuário autenticado (não existe JWT nessa chamada). O filtro automático de
        // tenant (que normalmente isola os dados por usuário logado) ficaria comparando
        // com "null" e zeraria qualquer resultado. O TenantId já vem validado pela
        // própria URL do webhook (que só nós configuramos), então isso é seguro aqui.
        var whatsappConnection = await _db.WhatsAppConnections
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(w => w.TenantId == request.TenantId
                                       && w.InstanceName == request.InstanceName, ct);

        if (whatsappConnection is null)
        {
            _logger.LogWarning(
                "Mensagem descartada: nenhuma WhatsAppConnection encontrada para tenant={TenantId} instance={InstanceName}.",
                request.TenantId, request.InstanceName);
            return new ProcessIncomingMessageResult(false, null);
        }

        // 1. Garante o contato
        var normalizedPhone = Domain.Common.PhoneNumberNormalizer.Normalize(request.FromPhoneNumber);
        var contact = await _db.Contacts
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TenantId == request.TenantId
                                       && c.PhoneNumber == normalizedPhone, ct);

        if (contact is null)
        {
            contact = new Contact
            {
                TenantId = request.TenantId,
                PhoneNumber = normalizedPhone,
                Name = request.ContactName
            };
            _db.Contacts.Add(contact);
        }

        if (string.IsNullOrEmpty(contact.ProfilePictureUrl))
        {
            try
            {
                contact.ProfilePictureUrl = await _whatsApp.GetProfilePictureUrlAsync(
                    whatsappConnection.InstanceName, normalizedPhone, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao buscar foto de perfil para {Phone}.", normalizedPhone);
            }
        }

        // 2. Garante a conversa aberta (ligada a esse contato E a esse número específico)
        var conversation = await _db.Conversations
            .IgnoreQueryFilters()
            .Include(c => c.Messages)
            .Where(c => c.ContactId == contact.Id
                        && c.WhatsAppConnectionId == whatsappConnection.Id
                        && c.Status != ConversationStatus.Closed)
            .OrderByDescending(c => c.LastMessageAtUtc)
            .FirstOrDefaultAsync(ct);

        if (conversation is null)
        {
            conversation = new Conversation
            {
                TenantId = request.TenantId,
                Contact = contact,
                WhatsAppConnectionId = whatsappConnection.Id,
                Status = ConversationStatus.Open
            };
            _db.Conversations.Add(conversation);
        }

        // 3. Persiste a mensagem recebida
        _db.Messages.Add(new Message
        {
            TenantId = request.TenantId,
            Conversation = conversation,
            Content = request.MessageText,
            Direction = MessageDirection.Inbound,
            SentBy = MessageSender.Contact,
            WhatsAppMessageId = request.WhatsAppMessageId
        });
        conversation.LastMessageAtUtc = DateTime.UtcNow;

        // 4. Busca config do agente de IA e do tenant
        var agentConfig = await _db.AiAgentConfigs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.TenantId == request.TenantId, ct);

        if (agentConfig is null || !agentConfig.AutoReplyEnabled)
        {
            await _db.SaveChangesAsync(ct);
            await _notifications.NotifyConversationUpdated(request.TenantId, conversation.Id);
            return new ProcessIncomingMessageResult(false, null);
        }

        // Contato bloqueado manualmente (tela de Contatos): a mensagem ainda é
        // salva normalmente, só não recebe resposta automática da IA.
        if (contact.IsBlocked)
        {
            conversation.Status = ConversationStatus.WaitingHuman;
            await _db.SaveChangesAsync(ct);
            await _notifications.NotifyConversationUpdated(request.TenantId, conversation.Id);
            return new ProcessIncomingMessageResult(false, null);
        }

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == request.TenantId, ct);
        var currentLocalTime = GetTenantLocalTime(tenant?.TimeZoneId);

        // 4.4 Fora do horário de atendimento? Não chama a IA (economiza custo e
        // evita respostas estranhas fora de hora) — só manda a mensagem de
        // fallback, se configurada, e deixa esperando um humano.
        if (!string.IsNullOrWhiteSpace(agentConfig.BusinessHours)
            && !IsWithinBusinessHours(agentConfig.BusinessHours, currentLocalTime))
        {
            conversation.Status = ConversationStatus.WaitingHuman;
            await SendFallbackMessageAsync(agentConfig, whatsappConnection, conversation, request.FromPhoneNumber, ct);
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation(
                "Mensagem recebida fora do horário de atendimento ({Horario}). Conversa={ConversationId}",
                agentConfig.BusinessHours, conversation.Id);
            await _notifications.NotifyConversationUpdated(request.TenantId, conversation.Id);
            return new ProcessIncomingMessageResult(false, null);
        }

        // 4.5 Checa se ainda sobra crédito de IA no plano desse tenant esse mês.
        // 1 crédito = 1 resposta gerada pela IA. O orçamento mensal é
        // (créditos do plano por usuário) × (quantidade de usuários da empresa).
        var planDef = Domain.Common.PlanCatalog.Get(tenant?.Plan ?? PlanTier.Starter);
        var userCount = await _db.Users.CountAsync(u => u.TenantId == request.TenantId && u.IsActive, ct);
        var monthlyBudget = planDef.AiCreditsPerUserPerMonth * Math.Max(1, userCount);

        var monthStartUtc = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var usedThisMonth = await _db.AiUsageLogs
            .CountAsync(u => u.TenantId == request.TenantId && u.CreatedAtUtc >= monthStartUtc, ct);

        if (usedThisMonth >= monthlyBudget)
        {
            conversation.Status = ConversationStatus.WaitingHuman;
            await SendFallbackMessageAsync(agentConfig, whatsappConnection, conversation, request.FromPhoneNumber, ct);
            await _db.SaveChangesAsync(ct);
            _logger.LogWarning(
                "Tenant {TenantId} estourou o limite de créditos de IA do mês ({Used}/{Budget}).",
                request.TenantId, usedThisMonth, monthlyBudget);
            await _notifications.NotifyConversationUpdated(request.TenantId, conversation.Id);
            return new ProcessIncomingMessageResult(false, null);
        }

        var anthropicApiKey = _globalSecrets.GetAnthropicApiKey();
        if (string.IsNullOrEmpty(anthropicApiKey))
        {
            conversation.Status = ConversationStatus.WaitingHuman;
            await SendFallbackMessageAsync(agentConfig, whatsappConnection, conversation, request.FromPhoneNumber, ct);
            await _db.SaveChangesAsync(ct);
            _logger.LogError("ANTHROPIC_API_KEY não está configurada no ambiente — nenhum tenant consegue usar a IA.");
            await _notifications.NotifyConversationUpdated(request.TenantId, conversation.Id);
            return new ProcessIncomingMessageResult(false, null);
        }

        // 5. Monta histórico e chama a IA (Claude) — com a ferramenta de criar
        // agendamento disponível de verdade.
        var history = conversation.Messages
            .OrderBy(m => m.CreatedAtUtc)
            .Select(m => (
                role: m.Direction == MessageDirection.Inbound ? "user" : "assistant",
                content: m.Content))
            .ToList();
        history.Add(("user", request.MessageText));

        Task<(bool Success, string Message)> OnCreateAppointment(AppointmentToolRequest toolRequest) =>
            CreateAppointmentFromAiAsync(request.TenantId, contact, whatsappConnection, toolRequest, ct);

        AiReplyResult aiResult;
        try
        {
            aiResult = await _aiAgent.GenerateReplyAsync(
                anthropicApiKey, agentConfig.SystemPrompt, history, currentLocalTime, OnCreateAppointment, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Falha ao chamar a IA para gerar resposta. Conversa={ConversationId}", conversation.Id);

            conversation.Status = ConversationStatus.WaitingHuman;
            await SendFallbackMessageAsync(agentConfig, whatsappConnection, conversation, request.FromPhoneNumber, ct);
            await _db.SaveChangesAsync(ct);
            await _notifications.NotifyConversationUpdated(request.TenantId, conversation.Id);
            return new ProcessIncomingMessageResult(false, null);
        }

        conversation.LastDetectedIntent = aiResult.DetectedIntent;

        // Custo real (em USD) fica só como informação interna sua — o cliente não
        // vê isso, ele só vê "créditos usados" contra o limite do plano dele.
        var costUsd = (aiResult.InputTokens / 1_000_000m) * InputCostPerMillionTokens
                     + (aiResult.OutputTokens / 1_000_000m) * OutputCostPerMillionTokens;
        _db.AiUsageLogs.Add(new AiUsageLog
        {
            TenantId = request.TenantId,
            ConversationId = conversation.Id,
            InputTokens = aiResult.InputTokens,
            OutputTokens = aiResult.OutputTokens,
            CostUsd = costUsd
        });

        if (aiResult.CreatedAppointment)
        {
            _logger.LogInformation("IA criou um agendamento automaticamente. Conversa={ConversationId}", conversation.Id);
        }

        // 6. Se precisa de aprovação humana, apenas registra sugestão e para por aqui.
        if (agentConfig.RequireHumanApproval || aiResult.ShouldEscalateToHuman)
        {
            conversation.Status = ConversationStatus.WaitingHuman;
            conversation.PendingAiSuggestion = aiResult.ReplyText;
            await _db.SaveChangesAsync(ct);
            await _notifications.NotifyConversationUpdated(request.TenantId, conversation.Id);
            return new ProcessIncomingMessageResult(false, aiResult.ReplyText);
        }

        // 7. Envia a resposta automaticamente
        try
        {
            await _whatsApp.SendTextMessageAsync(
                whatsappConnection.InstanceName, request.FromPhoneNumber, aiResult.ReplyText, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Falha ao enviar a resposta automática pelo WhatsApp. Conversa={ConversationId}", conversation.Id);

            conversation.Status = ConversationStatus.WaitingHuman;
            await _db.SaveChangesAsync(ct);
            await _notifications.NotifyConversationUpdated(request.TenantId, conversation.Id);
            return new ProcessIncomingMessageResult(false, aiResult.ReplyText);
        }

        _db.Messages.Add(new Message
        {
            TenantId = request.TenantId,
            Conversation = conversation,
            Content = aiResult.ReplyText,
            Direction = MessageDirection.Outbound,
            SentBy = MessageSender.AiAgent,
            AiGenerated = true
        });

        await _db.SaveChangesAsync(ct);
        await _notifications.NotifyConversationUpdated(request.TenantId, conversation.Id);
        return new ProcessIncomingMessageResult(true, aiResult.ReplyText);
    }

    /// <summary>
    /// Callback chamado pela IA (via tool use) quando o cliente confirma um
    /// agendamento. Checa conflito de horário, cria o Appointment de verdade
    /// (com lembretes padrão) e devolve se conseguiu + uma frase pra IA usar
    /// na resposta final (de confirmação ou explicando o motivo de não ter dado certo).
    /// </summary>
    private async Task<(bool Success, string Message)> CreateAppointmentFromAiAsync(
        Guid tenantId, Contact contact, WhatsAppConnection connection,
        AppointmentToolRequest toolRequest, CancellationToken ct)
    {
        try
        {
            if (toolRequest.ScheduledForUtc <= DateTime.UtcNow)
                return (false, "Erro: essa data/hora já passou. Peça pro cliente confirmar uma data futura.");

            // Checa se já não tem outro agendamento marcado muito perto desse horário
            // (mesma empresa, qualquer contato) — evita dois compromissos conflitando.
            var conflictWindow = TimeSpan.FromMinutes(30);
            var hasConflict = await _db.Appointments
                .IgnoreQueryFilters()
                .AnyAsync(a => a.TenantId == tenantId
                            && a.Status != AppointmentStatus.Cancelled
                            && a.Status != AppointmentStatus.Completed
                            && a.ScheduledForUtc > toolRequest.ScheduledForUtc - conflictWindow
                            && a.ScheduledForUtc < toolRequest.ScheduledForUtc + conflictWindow, ct);

            if (hasConflict)
            {
                return (false,
                    $"Erro: já existe outro agendamento marcado perto de {toolRequest.ScheduledForUtc:dd/MM HH:mm}. " +
                    "Peça pro cliente escolher outro horário (pelo menos 30 minutos de diferença) e ofereça alternativas próximas.");
            }

            var appointment = new Appointment
            {
                TenantId = tenantId,
                ContactId = contact.Id,
                WhatsAppConnectionId = connection.Id,
                Title = toolRequest.Title,
                ScheduledForUtc = toolRequest.ScheduledForUtc,
                Notes = toolRequest.Notes,
                Status = AppointmentStatus.Scheduled
            };
            _db.Appointments.Add(appointment);

            foreach (var minutesBefore in DefaultReminderOffsetMinutes)
            {
                var sendAt = toolRequest.ScheduledForUtc.AddMinutes(-minutesBefore);
                if (sendAt <= DateTime.UtcNow) continue;

                var reminder = new Reminder
                {
                    TenantId = tenantId,
                    Appointment = appointment,
                    SendAtUtc = sendAt,
                    Channel = ReminderChannel.WhatsApp,
                    Status = ReminderStatus.Pending,
                    MessageTemplate = "Olá {nome}! Passando para lembrar do seu compromisso \"{titulo}\" em {data} às {hora}. Até lá!"
                };
                reminder.HangfireJobId = _reminderScheduler.Schedule(reminder.Id, sendAt);
                _db.Reminders.Add(reminder);
            }

            // Salva já aqui pra garantir que o agendamento existe mesmo que o
            // resto do fluxo (resposta da IA, envio da mensagem) falhe depois.
            await _db.SaveChangesAsync(ct);

            return (true,
                $"Agendamento \"{toolRequest.Title}\" criado com sucesso para " +
                $"{toolRequest.ScheduledForUtc:dd/MM/yyyy} às {toolRequest.ScheduledForUtc:HH:mm}.");
        }
        catch (Exception ex)
        {
            // IMPORTANTE: sem esse log, esse erro ficava mudo — só aparecia
            // disfarçado numa resposta genérica da IA pro cliente.
            _logger.LogError(ex,
                "Falha ao criar agendamento via IA. Tenant={TenantId} Título={Titulo} ScheduledForUtc={ScheduledFor}",
                tenantId, toolRequest.Title, toolRequest.ScheduledForUtc);
            return (false, "Erro interno ao tentar criar o agendamento. Avise o cliente que um atendente vai confirmar manualmente.");
        }
    }

    private async Task SendFallbackMessageAsync(
        AiAgentConfig agentConfig, WhatsAppConnection connection, Conversation conversation,
        string toPhoneNumber, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(agentConfig.FallbackMessage)) return;

        try
        {
            await _whatsApp.SendTextMessageAsync(connection.InstanceName, toPhoneNumber, agentConfig.FallbackMessage, ct);
            _db.Messages.Add(new Message
            {
                TenantId = conversation.TenantId,
                Conversation = conversation,
                Content = agentConfig.FallbackMessage,
                Direction = MessageDirection.Outbound,
                SentBy = MessageSender.System,
                AiGenerated = false
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao enviar mensagem de fallback. Conversa={ConversationId}", conversation.Id);
        }
    }

    private static DateTime GetTenantLocalTime(string? timeZoneId)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId ?? "America/Sao_Paulo");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        }
        catch
        {
            // Fuso desconhecido no sistema — cai pro horário de Brasília fixo (UTC-3) como último recurso.
            return DateTime.UtcNow.AddHours(-3);
        }
    }

    /// <summary>
    /// Formato esperado em AiAgentConfig.BusinessHours: "HH:mm-HH:mm" (ex: "08:00-18:00").
    /// Não considera dia da semana — é um horário fixo todos os dias, por enquanto.
    /// </summary>
    private static bool IsWithinBusinessHours(string businessHours, DateTime currentLocalTime)
    {
        var parts = businessHours.Split('-');
        if (parts.Length != 2) return true; // formato inesperado: não bloqueia, assume 24h

        if (!TimeSpan.TryParse(parts[0].Trim(), out var start) ||
            !TimeSpan.TryParse(parts[1].Trim(), out var end))
            return true;

        var now = currentLocalTime.TimeOfDay;
        return start <= end
            ? now >= start && now <= end
            : now >= start || now <= end; // horário que vira a meia-noite, ex: 22:00-06:00
    }
}
