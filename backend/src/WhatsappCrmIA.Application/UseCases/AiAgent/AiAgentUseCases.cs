using MediatR;
using Microsoft.EntityFrameworkCore;
using WhatsappCrmIA.Application.DTOs;
using WhatsappCrmIA.Application.Interfaces;

namespace WhatsappCrmIA.Application.UseCases.AiAgent;

public record GetAiAgentConfigQuery : IRequest<AiAgentConfigDto?>;

public class GetAiAgentConfigHandler : IRequestHandler<GetAiAgentConfigQuery, AiAgentConfigDto?>
{
    private readonly IApplicationDbContext _db;
    public GetAiAgentConfigHandler(IApplicationDbContext db) => _db = db;

    public async Task<AiAgentConfigDto?> Handle(GetAiAgentConfigQuery request, CancellationToken ct)
    {
        var config = await _db.AiAgentConfigs.FirstOrDefaultAsync(ct);
        return config is null
            ? null
            : new AiAgentConfigDto(
                config.AgentName, config.SystemPrompt, config.AutoReplyEnabled,
                config.RequireHumanApproval, config.BusinessHours, config.FallbackMessage);
    }
}

public record UpdateAiAgentConfigCommand(
    string AgentName,
    string SystemPrompt,
    bool AutoReplyEnabled,
    bool RequireHumanApproval,
    string BusinessHours,
    string? FallbackMessage
) : IRequest<bool>;

public class UpdateAiAgentConfigHandler : IRequestHandler<UpdateAiAgentConfigCommand, bool>
{
    private readonly IApplicationDbContext _db;
    public UpdateAiAgentConfigHandler(IApplicationDbContext db) => _db = db;

    public async Task<bool> Handle(UpdateAiAgentConfigCommand request, CancellationToken ct)
    {
        var config = await _db.AiAgentConfigs.FirstOrDefaultAsync(ct);
        if (config is null) return false;

        config.AgentName = request.AgentName;
        config.SystemPrompt = request.SystemPrompt;
        config.AutoReplyEnabled = request.AutoReplyEnabled;
        config.RequireHumanApproval = request.RequireHumanApproval;
        config.BusinessHours = request.BusinessHours;
        config.FallbackMessage = request.FallbackMessage;

        await _db.SaveChangesAsync(ct);
        return true;
    }
}
