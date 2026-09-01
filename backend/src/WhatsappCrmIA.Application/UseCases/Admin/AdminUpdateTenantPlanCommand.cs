using MediatR;
using Microsoft.EntityFrameworkCore;
using WhatsappCrmIA.Application.Interfaces;
using WhatsappCrmIA.Domain.Enums;

namespace WhatsappCrmIA.Application.UseCases.Admin;

/// <summary>
/// Você controla o plano (e portanto os limites de número/usuários) de cada
/// empresa manualmente por aqui — não tem cobrança automática, é você quem
/// decide e ajusta conforme o que combinou com o cliente.
/// </summary>
public record AdminUpdateTenantPlanCommand(Guid TenantId, PlanTier Plan) : IRequest<bool>;

public class AdminUpdateTenantPlanHandler : IRequestHandler<AdminUpdateTenantPlanCommand, bool>
{
    private readonly IApplicationDbContext _db;
    public AdminUpdateTenantPlanHandler(IApplicationDbContext db) => _db = db;

    public async Task<bool> Handle(AdminUpdateTenantPlanCommand request, CancellationToken ct)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == request.TenantId, ct);
        if (tenant is null) return false;

        tenant.Plan = request.Plan;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
