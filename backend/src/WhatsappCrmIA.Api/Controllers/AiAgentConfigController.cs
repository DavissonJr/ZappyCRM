using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhatsappCrmIA.Application.DTOs;
using WhatsappCrmIA.Application.UseCases.AiAgent;

namespace WhatsappCrmIA.Api.Controllers;

public record UpdateAiAgentConfigRequest(
    string AgentName,
    string SystemPrompt,
    bool AutoReplyEnabled,
    bool RequireHumanApproval,
    string BusinessHours,
    string? FallbackMessage);

[ApiController]
[Route("api/ai-agent-config")]
[Authorize]
public class AiAgentConfigController : ControllerBase
{
    private readonly IMediator _mediator;
    public AiAgentConfigController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<AiAgentConfigDto>> Get(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetAiAgentConfigQuery(), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateAiAgentConfigRequest request, CancellationToken ct)
    {
        var success = await _mediator.Send(new UpdateAiAgentConfigCommand(
            request.AgentName, request.SystemPrompt, request.AutoReplyEnabled,
            request.RequireHumanApproval, request.BusinessHours, request.FallbackMessage), ct);
        return success ? Ok() : NotFound();
    }
}
