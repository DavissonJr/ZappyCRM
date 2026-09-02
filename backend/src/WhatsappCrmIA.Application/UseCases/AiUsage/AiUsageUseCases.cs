using MediatR;
using Microsoft.EntityFrameworkCore;
using WhatsappCrmIA.Application.DTOs;
using WhatsappCrmIA.Application.Interfaces;
using WhatsappCrmIA.Domain.Common;
using WhatsappCrmIA.Domain.Enums;

namespace WhatsappCrmIA.Application.UseCases.AiUsage;

public record GetAiUsageQuery : IRequest<AiCreditsStatusDto?>;

public class GetAiUsageHandler : IRequestHandler<GetAiUsageQuery, AiCreditsStatusDto?>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;

    public GetAiUsageHandler(IApplicationDbContext db, ICurrentTenantService currentTenant)
    {
        _db = db;
        _currentTenant = currentTenant;
    }

    public async Task<AiCreditsStatusDto?> Handle(GetAiUsageQuery request, CancellationToken ct)
    {
        if (_currentTenant.TenantId is not { } tenantId) return null;

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        var planDef = PlanCatalog.Get(tenant?.Plan ?? PlanTier.Starter);

        var userCount = await _db.Users.CountAsync(u => u.TenantId == tenantId && u.IsActive, ct);
        var budget = planDef.AiCreditsPerUserPerMonth * Math.Max(1, userCount);

        var monthStartUtc = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var usedThisMonth = await _db.AiUsageLogs.CountAsync(u => u.CreatedAtUtc >= monthStartUtc, ct);

        return new AiCreditsStatusDto(planDef.DisplayName, usedThisMonth, budget, monthStartUtc);
    }
}
