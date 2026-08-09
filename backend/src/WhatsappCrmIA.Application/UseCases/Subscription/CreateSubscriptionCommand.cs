using MediatR;
using Microsoft.EntityFrameworkCore;
using WhatsappCrmIA.Application.Interfaces;
using WhatsappCrmIA.Domain.Enums;

namespace WhatsappCrmIA.Application.UseCases.Subscription;

public record CreateSubscriptionCommand(PlanTier Plan, string BackUrl) : IRequest<(string? CheckoutUrl, string? Error)>;

public class CreateSubscriptionHandler : IRequestHandler<CreateSubscriptionCommand, (string? CheckoutUrl, string? Error)>
{
    private readonly IApplicationDbContext _db;
    private readonly IPaymentGateway _paymentGateway;
    private readonly ICurrentTenantService _currentTenant;

    public CreateSubscriptionHandler(
        IApplicationDbContext db, IPaymentGateway paymentGateway, ICurrentTenantService currentTenant)
    {
        _db = db;
        _paymentGateway = paymentGateway;
        _currentTenant = currentTenant;
    }

    public async Task<(string? CheckoutUrl, string? Error)> Handle(CreateSubscriptionCommand request, CancellationToken ct)
    {
        if (_currentTenant.TenantId is not { } tenantId) return (null, "Sessão inválida.");

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null) return (null, "Empresa não encontrada.");

        var owner = await _db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Role == UserRole.Owner, ct);
        if (owner is null) return (null, "Não encontramos o responsável pela conta.");

        try
        {
            var result = await _paymentGateway.CreateSubscriptionAsync(tenantId, owner.Email, request.Plan, request.BackUrl, ct);

            // Guarda o id — o plano só vira "Active" de verdade quando o
            // webhook confirmar que o pagamento foi autorizado.
            tenant.MercadoPagoPreapprovalId = result.PreapprovalId;
            await _db.SaveChangesAsync(ct);

            return (result.CheckoutUrl, null);
        }
        catch (Exception ex)
        {
            return (null, $"Não foi possível iniciar a assinatura: {ex.Message}");
        }
    }
}
