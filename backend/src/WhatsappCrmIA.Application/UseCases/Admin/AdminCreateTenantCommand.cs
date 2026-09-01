using MediatR;
using Microsoft.EntityFrameworkCore;
using WhatsappCrmIA.Application.Interfaces;
using WhatsappCrmIA.Domain.Entities;
using WhatsappCrmIA.Domain.Enums;

namespace WhatsappCrmIA.Application.UseCases.Admin;

/// <summary>
/// Cria uma empresa (tenant) direto, sem passar pelo cadastro público nem
/// pelo código de confirmação por e-mail — é você (admin da plataforma)
/// criando a conta pro cliente, então não precisa dessas proteções
/// anti-bot. O dono da empresa recebe e-mail/senha temporária e troca depois.
/// </summary>
public record AdminCreateTenantCommand(
    string CompanyName,
    string Segment,
    PlanTier Plan,
    string OwnerFullName,
    string OwnerEmail,
    string TemporaryPassword
) : IRequest<(Guid? TenantId, string? Error)>;

public class AdminCreateTenantHandler : IRequestHandler<AdminCreateTenantCommand, (Guid? TenantId, string? Error)>
{
    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;

    public AdminCreateTenantHandler(IApplicationDbContext db, IPasswordHasher passwordHasher)
    {
        _db = db;
        _passwordHasher = passwordHasher;
    }

    public async Task<(Guid? TenantId, string? Error)> Handle(AdminCreateTenantCommand request, CancellationToken ct)
    {
        var email = request.OwnerEmail.Trim().ToLowerInvariant();

        var emailInUse = await _db.Users.AnyAsync(u => u.Email == email, ct);
        if (emailInUse) return (null, "Já existe uma conta com esse e-mail.");

        var tenant = new Tenant
        {
            Name = request.CompanyName,
            Segment = request.Segment,
            Plan = request.Plan,
            IsActive = true
        };
        _db.Tenants.Add(tenant);

        var user = new User
        {
            TenantId = tenant.Id,
            Email = email,
            PasswordHash = _passwordHasher.Hash(request.TemporaryPassword),
            FullName = request.OwnerFullName,
            Role = UserRole.Owner
        };
        _db.Users.Add(user);

        _db.AiAgentConfigs.Add(new AiAgentConfig
        {
            TenantId = tenant.Id,
            AgentName = "Assistente Virtual",
            SystemPrompt = $"Você é o assistente virtual da empresa {request.CompanyName}, " +
                            $"do segmento {request.Segment}. Seja cordial, objetivo e sempre em pt-BR.",
            AutoReplyEnabled = true,
            RequireHumanApproval = true
        });

        await _db.SaveChangesAsync(ct);
        return (tenant.Id, null);
    }
}
