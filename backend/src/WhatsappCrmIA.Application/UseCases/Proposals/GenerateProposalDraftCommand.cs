using MediatR;
using Microsoft.EntityFrameworkCore;
using WhatsappCrmIA.Application.Interfaces;
using WhatsappCrmIA.Domain.Entities;
using WhatsappCrmIA.Domain.Enums;

namespace WhatsappCrmIA.Application.UseCases.Proposals;

/// <summary>
/// Gera um rascunho de proposta comercial com a IA, baseado no histórico da
/// conversa. Fica como "Draft" — não é enviado automaticamente, o atendente
/// revisa/edita antes de mandar pro cliente.
/// </summary>
public record GenerateProposalDraftCommand(Guid ConversationId) : IRequest<(Guid? ProposalId, string? Error)>;

public class GenerateProposalDraftHandler : IRequestHandler<GenerateProposalDraftCommand, (Guid? ProposalId, string? Error)>
{
    private readonly IApplicationDbContext _db;
    private readonly IAiAgentService _aiAgent;
    private readonly IGlobalSecretsProvider _globalSecrets;
    private readonly ICurrentTenantService _currentTenant;

    public GenerateProposalDraftHandler(
        IApplicationDbContext db,
        IAiAgentService aiAgent,
        IGlobalSecretsProvider globalSecrets,
        ICurrentTenantService currentTenant)
    {
        _db = db;
        _aiAgent = aiAgent;
        _globalSecrets = globalSecrets;
        _currentTenant = currentTenant;
    }

    public async Task<(Guid? ProposalId, string? Error)> Handle(GenerateProposalDraftCommand request, CancellationToken ct)
    {
        var conversation = await _db.Conversations
            .Include(c => c.Contact)
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.Id == request.ConversationId, ct);
        if (conversation is null) return (null, "Conversa não encontrada.");

        var apiKey = _globalSecrets.GetAnthropicApiKey();
        if (string.IsNullOrEmpty(apiKey))
            return (null, "A IA não está configurada no momento. Fale com o suporte.");

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == _currentTenant.TenantId, ct);
        var businessContext = $"{tenant?.Name} (segmento: {tenant?.Segment})";

        var history = conversation.Messages
            .OrderBy(m => m.CreatedAtUtc)
            .Select(m => (
                role: m.Direction == MessageDirection.Inbound ? "user" : "assistant",
                content: m.Content))
            .ToList();

        if (history.Count == 0)
            return (null, "Essa conversa ainda não tem mensagens pra basear a proposta.");

        // A API da Anthropic exige que a conversa termine com uma mensagem do
        // usuário (não aceita "prefill" de assistente nesse tipo de chamada).
        // Se a última mensagem foi enviada pela empresa, completa com uma
        // instrução explícita pra IA saber que deve gerar a proposta agora.
        if (history[^1].role != "user")
        {
            history.Add(("user", "Com base em tudo o que conversamos até aqui, gere a proposta comercial."));
        }

        string draftText;
        try
        {
            draftText = await _aiAgent.GenerateProposalDraftAsync(apiKey, businessContext, history, ct);
        }
        catch (Exception ex)
        {
            return (null, $"Não foi possível gerar a proposta com a IA: {ex.Message}");
        }

        var proposal = new Proposal
        {
            ContactId = conversation.ContactId,
            ConversationId = conversation.Id,
            Title = $"Proposta para {conversation.Contact.Name ?? conversation.Contact.PhoneNumber}",
            Description = draftText,
            Status = ProposalStatus.Draft,
            AiGenerated = true
        };
        _db.Proposals.Add(proposal);
        await _db.SaveChangesAsync(ct);

        return (proposal.Id, null);
    }
}
