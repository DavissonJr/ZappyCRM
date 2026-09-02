using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhatsappCrmIA.Application.DTOs;
using WhatsappCrmIA.Application.UseCases.AiUsage;

namespace WhatsappCrmIA.Api.Controllers;

/// <summary>
/// Só leitura: mostra quantos créditos de IA o tenant já usou esse mês,
/// contra o limite do plano dele. O custo real em dinheiro é informação
/// interna, não aparece aqui — o cliente só vê "créditos".
/// </summary>
[ApiController]
[Route("api/ai-usage")]
[Authorize]
public class AiUsageController : ControllerBase
{
    private readonly IMediator _mediator;
    public AiUsageController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<AiCreditsStatusDto>> Get(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetAiUsageQuery(), ct);
        return result is null ? NotFound() : Ok(result);
    }
}
