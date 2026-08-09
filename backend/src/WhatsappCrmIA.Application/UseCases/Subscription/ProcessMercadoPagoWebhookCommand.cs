using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WhatsappCrmIA.Application.Interfaces;
using WhatsappCrmIA.Domain.Enums;

namespace WhatsappCrmIA.Application.UseCases.Subscription;

/// <summary>
/// Processa a notificação do Mercado Pago. IMPORTANTE: nunca confia no
/// conteúdo do webhook em si (ele pode ser forjado) — sempre busca o status
/// real direto na API do Mercado Pago antes de atualizar qualquer coisa.
/// </summary>
public record ProcessMercadoPagoWebhookCommand(string PreapprovalId) : IRequest;

public class ProcessMercadoPagoWebhookHandler : IRequestHandler<ProcessMercadoPagoWebhookCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IPaymentGateway _paymentGateway;
    private readonly ILogger<ProcessMercadoPagoWebhookHandler> _logger;

    public ProcessMercadoPagoWebhookHandler(
        IApplicationDbContext db, IPaymentGateway paymentGateway, ILogger<ProcessMercadoPagoWebhookHandler> logger)
    {
        _db = db;
        _paymentGateway = paymentGateway;
        _logger = logger;
    }

    public async Task Handle(ProcessMercadoPagoWebhookCommand request, CancellationToken ct)
    {
        RemoteSubscriptionInfo info;
        try
        {
            info = await _paymentGateway.GetSubscriptionAsync(request.PreapprovalId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao consultar assinatura {PreapprovalId} no Mercado Pago.", request.PreapprovalId);
            return;
        }

        // external_reference foi montado como "{tenantId}:{plano}" na hora de criar.
        Guid? tenantId = null;
        PlanTier? plan = null;
        if (!string.IsNullOrEmpty(info.ExternalReference))
        {
            var parts = info.ExternalReference.Split(':');
            if (parts.Length == 2 && Guid.TryParse(parts[0], out var parsedId) &&
                Enum.TryParse<PlanTier>(parts[1], out var parsedPlan))
            {
                tenantId = parsedId;
                plan = parsedPlan;
            }
        }

        var tenant = tenantId is not null
            ? await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            : await _db.Tenants.FirstOrDefaultAsync(t => t.MercadoPagoPreapprovalId == request.PreapprovalId, ct);

        if (tenant is null)
        {
            _logger.LogWarning("Webhook do Mercado Pago recebido pra um tenant não encontrado. PreapprovalId={Id}", request.PreapprovalId);
            return;
        }

        tenant.MercadoPagoPreapprovalId = info.PreapprovalId;

        switch (info.Status)
        {
            case RemoteSubscriptionStatus.Authorized:
                tenant.SubscriptionStatus = SubscriptionStatus.Active;
                if (plan is not null) tenant.Plan = plan.Value;
                tenant.CurrentPeriodEndUtc = info.NextPaymentDateUtc;
                tenant.SubscriptionCancelledAtUtc = null;
                break;

            case RemoteSubscriptionStatus.Paused:
                tenant.SubscriptionStatus = SubscriptionStatus.PastDue;
                break;

            case RemoteSubscriptionStatus.Cancelled:
                tenant.SubscriptionStatus = SubscriptionStatus.Cancelled;
                tenant.SubscriptionCancelledAtUtc = DateTime.UtcNow;
                break;

            case RemoteSubscriptionStatus.Pending:
            default:
                // Ainda esperando o cliente concluir o pagamento no checkout — não faz nada ainda.
                break;
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Assinatura do tenant {TenantId} atualizada: status={Status} plano={Plan}",
            tenant.Id, tenant.SubscriptionStatus, tenant.Plan);
    }
}
