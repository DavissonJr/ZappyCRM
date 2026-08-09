using MediatR;
using Microsoft.EntityFrameworkCore;
using WhatsappCrmIA.Application.Interfaces;

namespace WhatsappCrmIA.Application.UseCases.Auth;

public record LoginCommand(string Email, string Password) : IRequest<AuthResult>;

public class LoginHandler : IRequestHandler<LoginCommand, AuthResult>
{
    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;

    public LoginHandler(
        IApplicationDbContext db, IPasswordHasher passwordHasher, IJwtTokenService jwtTokenService)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<AuthResult> Handle(LoginCommand request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        // Users não tem query filter de tenant (não temos tenant atual antes do login).
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user is null || !user.IsActive || !_passwordHasher.Verify(request.Password, user.PasswordHash))
            return new AuthResult(false, null, "E-mail ou senha inválidos.");

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == user.TenantId, ct);
        if (tenant is null || !tenant.IsActive)
            return new AuthResult(false, null, "Essa conta está temporariamente desativada. Fale com o suporte.");

        // Não bloqueia o login quando o trial acaba — a pessoa precisa conseguir
        // entrar pra chegar na tela de Assinatura e pagar. Só atualiza o status
        // pra o frontend saber mostrar o aviso.
        if (tenant.SubscriptionStatus == Domain.Enums.SubscriptionStatus.TrialActive
            && DateTime.UtcNow > tenant.TrialEndsAtUtc)
        {
            tenant.SubscriptionStatus = Domain.Enums.SubscriptionStatus.TrialExpired;
            await _db.SaveChangesAsync(ct);
        }

        var token = _jwtTokenService.GenerateToken(user, user.TenantId);
        return new AuthResult(true, token, null);
    }
}
