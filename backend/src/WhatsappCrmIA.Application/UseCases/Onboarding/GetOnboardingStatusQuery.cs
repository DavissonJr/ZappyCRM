using MediatR;
using Microsoft.EntityFrameworkCore;
using WhatsappCrmIA.Application.DTOs;
using WhatsappCrmIA.Application.Interfaces;

namespace WhatsappCrmIA.Application.UseCases.Onboarding;

public record GetOnboardingStatusQuery : IRequest<OnboardingStatusDto>;

public class GetOnboardingStatusHandler : IRequestHandler<GetOnboardingStatusQuery, OnboardingStatusDto>
{
    private readonly IApplicationDbContext _db;
    public GetOnboardingStatusHandler(IApplicationDbContext db) => _db = db;

    public async Task<OnboardingStatusDto> Handle(GetOnboardingStatusQuery request, CancellationToken ct)
    {
        var hasConnectedWhatsApp = await _db.WhatsAppConnections.AnyAsync(w => w.IsConnected, ct);
        return new OnboardingStatusDto(hasConnectedWhatsApp);
    }
}
