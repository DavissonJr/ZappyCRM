using WhatsappCrmIA.Domain.Enums;

namespace WhatsappCrmIA.Application.Interfaces;

public record CreateSubscriptionResult(string PreapprovalId, string CheckoutUrl);

public enum RemoteSubscriptionStatus { Pending, Authorized, Paused, Cancelled, Unknown }

public record RemoteSubscriptionInfo(
    string PreapprovalId, RemoteSubscriptionStatus Status, string? ExternalReference, DateTime? NextPaymentDateUtc);

/// <summary>
/// Abstração sobre o gateway de pagamento — hoje implementado com Mercado
/// Pago (Preapproval = assinatura recorrente), mas o resto do sistema não
/// depende disso diretamente.
/// </summary>
public interface IPaymentGateway
{
    Task<CreateSubscriptionResult> CreateSubscriptionAsync(
        Guid tenantId, string payerEmail, PlanTier plan, string backUrl, CancellationToken ct = default);

    Task CancelSubscriptionAsync(string preapprovalId, CancellationToken ct = default);

    Task<RemoteSubscriptionInfo> GetSubscriptionAsync(string preapprovalId, CancellationToken ct = default);
}
