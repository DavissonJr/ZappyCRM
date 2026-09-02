using MediatR;
using Microsoft.EntityFrameworkCore;
using WhatsappCrmIA.Application.Interfaces;
using WhatsappCrmIA.Domain.Entities;
using WhatsappCrmIA.Domain.Enums;

namespace WhatsappCrmIA.Application.UseCases.Auth;

public record VerifyRegistrationCommand(string Email, string Code) : IRequest<AuthResult>;

public class VerifyRegistrationHandler : IRequestHandler<VerifyRegistrationCommand, AuthResult>
{
    private const int MaxAttempts = 5;

    private readonly IApplicationDbContext _db;
    private readonly IJwtTokenService _jwtTokenService;

    public VerifyRegistrationHandler(IApplicationDbContext db, IJwtTokenService jwtTokenService)
    {
        _db = db;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<AuthResult> Handle(VerifyRegistrationCommand request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var pending = await _db.PendingRegistrations.FirstOrDefaultAsync(p => p.Email == email, ct);

        if (pending is null)
            return new AuthResult(false, null, "Não encontramos um cadastro pendente para esse e-mail. Cadastre-se de novo.");

        if (pending.ExpiresAtUtc < DateTime.UtcNow)
        {
            _db.PendingRegistrations.Remove(pending);
            await _db.SaveChangesAsync(ct);
            return new AuthResult(false, null, "Esse código expirou. Cadastre-se de novo pra receber um código novo.");
        }

        if (pending.AttemptCount >= MaxAttempts)
        {
            _db.PendingRegistrations.Remove(pending);
            await _db.SaveChangesAsync(ct);
            return new AuthResult(false, null, "Muitas tentativas erradas. Cadastre-se de novo pra receber um código novo.");
        }

        if (pending.VerificationCode != request.Code.Trim())
        {
            pending.AttemptCount++;
            await _db.SaveChangesAsync(ct);
            return new AuthResult(false, null, "Código incorreto. Confira e tente de novo.");
        }

        // Código certo: agora sim cria a conta de verdade.
        var tenant = new Tenant
        {
            Name = pending.CompanyName,
            Segment = pending.Segment,
            Plan = PlanTier.Starter,
            IsActive = true
        };
        _db.Tenants.Add(tenant);

        var user = new User
        {
            TenantId = tenant.Id,
            Email = pending.Email,
            PasswordHash = pending.PasswordHash,
            FullName = pending.FullName,
            Role = UserRole.Owner
        };
        _db.Users.Add(user);

        _db.AiAgentConfigs.Add(new AiAgentConfig
        {
            TenantId = tenant.Id,
            AgentName = "Assistente Virtual",
            SystemPrompt = $"Você é o assistente virtual da empresa {pending.CompanyName}, " +
                            $"do segmento {pending.Segment}. Seja cordial, objetivo e sempre em pt-BR.",
            AutoReplyEnabled = true,
            RequireHumanApproval = true
        });

        _db.PendingRegistrations.Remove(pending);
        await _db.SaveChangesAsync(ct);

        var token = _jwtTokenService.GenerateToken(user, tenant.Id);
        return new AuthResult(true, token, null);
    }
}
