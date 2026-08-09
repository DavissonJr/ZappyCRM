using MediatR;
using Microsoft.EntityFrameworkCore;
using WhatsappCrmIA.Application.DTOs;
using WhatsappCrmIA.Application.Interfaces;
using WhatsappCrmIA.Domain.Common;

namespace WhatsappCrmIA.Application.UseCases.Subscription;

public record GetSubscriptionStatusQuery : IRequest<SubscriptionStatusDto?>;

public class GetSubscriptionStatusHandler : IRequestHandler<GetSubscriptionStatusQuery, SubscriptionStatusDto?>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;

    public GetSubscriptionStatusHandler(IApplicationDbContext db, ICurrentTenantService currentTenant)
    {
        _db = db;
        _currentTenant = currentTenant;
    }

    public async Task<SubscriptionStatusDto?> Handle(GetSubscriptionStatusQuery request, CancellationToken ct)
    {
        if (_currentTenant.TenantId is not { } tenantId) return null;

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null) return null;

        var connectionCount = await _db.WhatsAppConnections.CountAsync(ct);
        var userCount = await _db.Users.IgnoreQueryFilters().CountAsync(u => u.TenantId == tenantId, ct);

        var daysLeftInTrial = tenant.SubscriptionStatus == Domain.Enums.SubscriptionStatus.TrialActive
            ? Math.Max(0, (int)Math.Ceiling((tenant.TrialEndsAtUtc - DateTime.UtcNow).TotalDays))
            : 0;

        var plans = PlanCatalog.Sellable.Select(p => new PlanDto(
            p.Tier.ToString(), p.DisplayName, p.PriceMonthly,
            p.MaxWhatsAppConnections == PlanCatalog.UnlimitedMarker ? -1 : p.MaxWhatsAppConnections,
            p.MaxUsers == PlanCatalog.UnlimitedMarker ? -1 : p.MaxUsers,
            p.Highlights, p.Tier == tenant.Plan)).ToList();

        return new SubscriptionStatusDto(
            tenant.Plan.ToString(), tenant.SubscriptionStatus.ToString(),
            tenant.SubscriptionStatus == Domain.Enums.SubscriptionStatus.TrialActive ? tenant.TrialEndsAtUtc : null,
            tenant.CurrentPeriodEndUtc, daysLeftInTrial,
            connectionCount, userCount, plans);
    }
}
