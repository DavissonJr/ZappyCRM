using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhatsappCrmIA.Application.DTOs;
using WhatsappCrmIA.Application.UseCases.Subscription;
using WhatsappCrmIA.Domain.Enums;

namespace WhatsappCrmIA.Api.Controllers;

public record CreateSubscriptionRequest(PlanTier Plan);

[ApiController]
[Route("api/subscription")]
[Authorize]
public class SubscriptionController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IConfiguration _config;

    public SubscriptionController(IMediator mediator, IConfiguration config)
    {
        _mediator = mediator;
        _config = config;
    }

    [HttpGet]
    public async Task<ActionResult<SubscriptionStatusDto>> Get(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetSubscriptionStatusQuery(), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("checkout")]
    public async Task<IActionResult> CreateCheckout([FromBody] CreateSubscriptionRequest request, CancellationToken ct)
    {
        // back_url precisa ser uma URL pública com HTTPS (o Mercado Pago rejeita
        // localhost) — por isso usa uma config própria, não o Cors:AllowedOrigin
        // (que continua sendo localhost de propósito, pra funcionar o CORS local).
        var publicUrl = _config["App:PublicUrl"];
        var backUrl = string.IsNullOrEmpty(publicUrl)
            ? _config["Cors:AllowedOrigin"] ?? "https://example.com"
            : $"{publicUrl.TrimEnd('/')}/configuracoes";

        var (checkoutUrl, error) = await _mediator.Send(new CreateSubscriptionCommand(request.Plan, backUrl), ct);
        return checkoutUrl is null ? BadRequest(new { message = error }) : Ok(new { checkoutUrl });
    }

    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel(CancellationToken ct)
    {
        var (success, error) = await _mediator.Send(new CancelSubscriptionCommand(), ct);
        return success ? Ok() : BadRequest(new { message = error });
    }
}
