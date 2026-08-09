using MediatR;
using Microsoft.EntityFrameworkCore;
using WhatsappCrmIA.Application.Interfaces;
using WhatsappCrmIA.Domain.Enums;

namespace WhatsappCrmIA.Application.UseCases.Subscription;

public record CancelSubscriptionCommand : IRequest<(bool Success, string? Error)>;

public class CancelSubscriptionHandler : IRequestHandler<CancelSubscriptionCommand, (bool Success, string? Error)>
{
    private readonly IApplicationDbContext _db;
    private readonly IPaymentGateway _paymentGateway;
    private readonly ICurrentTenantService _currentTenant;

    public CancelSubscriptionHandler(
        IApplicationDbContext db, IPaymentGateway paymentGateway, ICurrentTenantService currentTenant)
    {
        _db = db;
        _paymentGateway = paymentGateway;
        _currentTenant = currentTenant;
    }

    public async Task<(bool Success, string? Error)> Handle(CancelSubscriptionCommand request, CancellationToken ct)
    {
        if (_currentTenant.TenantId is not { } tenantId) return (false, "Sessão inválida.");

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null) return (false, "Empresa não encontrada.");

        if (string.IsNullOrEmpty(tenant.MercadoPagoPreapprovalId))
            return (false, "Essa empresa não tem uma assinatura ativa pra cancelar.");

        try
        {
            await _paymentGateway.CancelSubscriptionAsync(tenant.MercadoPagoPreapprovalId, ct);
        }
        catch (Exception ex)
        {
            return (false, $"Não foi possível cancelar no Mercado Pago: {ex.Message}");
        }

        tenant.SubscriptionStatus = SubscriptionStatus.Cancelled;
        tenant.SubscriptionCancelledAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return (true, null);
    }
}
