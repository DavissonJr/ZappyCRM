using MediatR;
using Microsoft.EntityFrameworkCore;
using WhatsappCrmIA.Application.DTOs;
using WhatsappCrmIA.Application.Interfaces;
using WhatsappCrmIA.Domain.Entities;
using WhatsappCrmIA.Domain.Enums;

namespace WhatsappCrmIA.Application.UseCases.Team;

public record GetTeamQuery : IRequest<IReadOnlyList<TeamMemberDto>>;

public class GetTeamHandler : IRequestHandler<GetTeamQuery, IReadOnlyList<TeamMemberDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;

    public GetTeamHandler(IApplicationDbContext db, ICurrentTenantService currentTenant)
    {
        _db = db;
        _currentTenant = currentTenant;
    }

    public async Task<IReadOnlyList<TeamMemberDto>> Handle(GetTeamQuery request, CancellationToken ct)
    {
        // Users não tem query filter automático de tenant (ver AppDbContext), então filtramos aqui.
        return await _db.Users
            .Where(u => u.TenantId == _currentTenant.TenantId)
            .OrderBy(u => u.CreatedAtUtc)
            .Select(u => new TeamMemberDto(u.Id, u.FullName, u.Email, u.Role.ToString(), u.IsActive))
            .ToListAsync(ct);
    }
}

public record InviteTeamMemberCommand(string FullName, string Email, string TemporaryPassword)
    : IRequest<(bool Success, string? Error)>;

public class InviteTeamMemberHandler : IRequestHandler<InviteTeamMemberCommand, (bool Success, string? Error)>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;
    private readonly IPasswordHasher _passwordHasher;

    public InviteTeamMemberHandler(
        IApplicationDbContext db, ICurrentTenantService currentTenant, IPasswordHasher passwordHasher)
    {
        _db = db;
        _currentTenant = currentTenant;
        _passwordHasher = passwordHasher;
    }

    public async Task<(bool Success, string? Error)> Handle(InviteTeamMemberCommand request, CancellationToken ct)
    {
        if (_currentTenant.TenantId is not { } tenantId) return (false, "Tenant não identificado.");

        var emailTaken = await _db.Users.AnyAsync(u => u.Email == request.Email, ct);
        if (emailTaken) return (false, "Esse e-mail já está em uso.");

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        var planLimit = Domain.Common.PlanCatalog.Get(tenant?.Plan ?? Domain.Enums.PlanTier.Trial).MaxUsers;
        if (planLimit != Domain.Common.PlanCatalog.UnlimitedMarker)
        {
            var currentCount = await _db.Users.IgnoreQueryFilters().CountAsync(u => u.TenantId == tenantId, ct);
            if (currentCount >= planLimit)
                return (false,
                    $"Seu plano atual permite até {planLimit} usuário(s). " +
                    "Faça upgrade em Configurações → Assinatura pra adicionar mais.");
        }

        _db.Users.Add(new User
        {
            TenantId = tenantId,
            FullName = request.FullName,
            Email = request.Email,
            PasswordHash = _passwordHasher.Hash(request.TemporaryPassword),
            Role = UserRole.Agent,
            IsActive = true
        });

        await _db.SaveChangesAsync(ct);
        return (true, null);
    }
}

public record SetTeamMemberActiveCommand(Guid UserId, bool IsActive) : IRequest<bool>;

public class SetTeamMemberActiveHandler : IRequestHandler<SetTeamMemberActiveCommand, bool>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentTenantService _currentTenant;

    public SetTeamMemberActiveHandler(IApplicationDbContext db, ICurrentTenantService currentTenant)
    {
        _db = db;
        _currentTenant = currentTenant;
    }

    public async Task<bool> Handle(SetTeamMemberActiveCommand request, CancellationToken ct)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId && u.TenantId == _currentTenant.TenantId, ct);
        if (user is null) return false;

        // Não deixa desativar o próprio dono por engano deixando o tenant sem ninguém.
        if (user.Role == UserRole.Owner && !request.IsActive) return false;

        user.IsActive = request.IsActive;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
