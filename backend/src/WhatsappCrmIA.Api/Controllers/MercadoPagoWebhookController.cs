using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhatsappCrmIA.Application.UseCases.Subscription;

namespace WhatsappCrmIA.Api.Controllers;

/// <summary>
/// Recebe as notificações do Mercado Pago quando o status de uma assinatura
/// muda (pagamento autorizado, pausado, cancelado...). Precisa estar
/// configurada no painel do Mercado Pago como a URL de notificação —
/// só funciona com uma URL pública de verdade (não localhost).
/// </summary>
[ApiController]
[Route("webhook/mercadopago")]
[AllowAnonymous]
public class MercadoPagoWebhookController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<MercadoPagoWebhookController> _logger;

    public MercadoPagoWebhookController(IMediator mediator, ILogger<MercadoPagoWebhookController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        // O Mercado Pago manda o formato variando: às vezes via query string
        // (?type=subscription_preapproval&data.id=xxx), às vezes no corpo.
        // Aceita os dois pra não depender de qual integração eles escolheram.
        var type = Request.Query["type"].FirstOrDefault() ?? Request.Query["topic"].FirstOrDefault();
        var preapprovalId = Request.Query["data.id"].FirstOrDefault();

        if (string.IsNullOrEmpty(preapprovalId) && Request.ContentLength > 0)
        {
            try
            {
                using var doc = await System.Text.Json.JsonDocument.ParseAsync(Request.Body, cancellationToken: ct);
                var root = doc.RootElement;

                if (type is null && root.TryGetProperty("type", out var typeEl))
                    type = typeEl.GetString();

                if (root.TryGetProperty("data", out var dataEl) && dataEl.TryGetProperty("id", out var idEl))
                    preapprovalId = idEl.GetString();
            }
            catch (System.Text.Json.JsonException)
            {
                // corpo vazio ou não-JSON — segue só com o que veio na query string mesmo
            }
        }

        _logger.LogInformation("Webhook do Mercado Pago recebido: type={Type} preapprovalId={Id}", type, preapprovalId);

        // Só nos interessa notificação de assinatura (preapproval). Notificação
        // de pagamento avulso (topic=payment) a gente ignora por enquanto.
        if (!string.IsNullOrEmpty(preapprovalId) &&
            (type is null || type.Contains("preapproval", StringComparison.OrdinalIgnoreCase)))
        {
            await _mediator.Send(new ProcessMercadoPagoWebhookCommand(preapprovalId), ct);
        }

        // Sempre 200 — o Mercado Pago reenvia (e desativa a URL depois de muitas
        // falhas seguidas) se não receber sucesso, mesmo pra notificações que a
        // gente decidiu ignorar de propósito.
        return Ok();
    }
}
