using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using WhatsappCrmIA.Application.Interfaces;
using WhatsappCrmIA.Domain.Common;
using WhatsappCrmIA.Domain.Enums;

namespace WhatsappCrmIA.Infrastructure.Services;

/// <summary>
/// Integração com a API de assinaturas (Preapproval) do Mercado Pago.
/// Docs: https://www.mercadopago.com.br/developers/pt/reference/subscriptions/_preapproval/post
/// </summary>
public class MercadoPagoGateway : IPaymentGateway
{
    private readonly HttpClient _http;

    public MercadoPagoGateway(HttpClient http, IConfiguration config)
    {
        _http = http;
        _http.BaseAddress = new Uri("https://api.mercadopago.com/");
        _config = config;

        var accessToken = config["MercadoPago:AccessToken"];
        if (!string.IsNullOrEmpty(accessToken))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    private readonly IConfiguration _config;

    public async Task<CreateSubscriptionResult> CreateSubscriptionAsync(
        Guid tenantId, string payerEmail, PlanTier plan, string backUrl, CancellationToken ct = default)
    {
        var planDef = PlanCatalog.Get(plan);
        var webhookBaseUrl = _config["MercadoPago:WebhookBaseUrl"]?.TrimEnd('/');

        var payload = new
        {
            reason = $"Zappy CRM — Plano {planDef.DisplayName}",
            auto_recurring = new
            {
                frequency = 1,
                frequency_type = "months",
                transaction_amount = planDef.PriceMonthly,
                currency_id = "BRL"
            },
            back_url = backUrl,
            payer_email = payerEmail,
            status = "pending",
            external_reference = $"{tenantId}:{plan}",
            // Manda a notificação direto pra cá — não precisa configurar nada
            // manualmente no painel do Mercado Pago. Se WebhookBaseUrl não
            // estiver configurado, o Mercado Pago simplesmente não notifica
            // (você teria que consultar o status manualmente).
            notification_url = string.IsNullOrEmpty(webhookBaseUrl)
                ? null
                : $"{webhookBaseUrl}/webhook/mercadopago"
        };

        var response = await _http.PostAsJsonAsync("preapproval", payload, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Mercado Pago respondeu {(int)response.StatusCode}: {body}");

        var parsed = JsonSerializer.Deserialize<PreapprovalResponse>(body)
            ?? throw new InvalidOperationException("Resposta inesperada do Mercado Pago ao criar assinatura.");

        return new CreateSubscriptionResult(parsed.Id!, parsed.InitPoint!);
    }

    public async Task CancelSubscriptionAsync(string preapprovalId, CancellationToken ct = default)
    {
        var payload = new { status = "cancelled" };
        var response = await _http.PutAsJsonAsync($"preapproval/{preapprovalId}", payload, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Mercado Pago respondeu {(int)response.StatusCode}: {body}");
        }
    }

    public async Task<RemoteSubscriptionInfo> GetSubscriptionAsync(string preapprovalId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"preapproval/{preapprovalId}", ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Mercado Pago respondeu {(int)response.StatusCode}: {body}");

        var parsed = JsonSerializer.Deserialize<PreapprovalResponse>(body)
            ?? throw new InvalidOperationException("Resposta inesperada do Mercado Pago ao consultar assinatura.");

        var status = parsed.Status switch
        {
            "authorized" => RemoteSubscriptionStatus.Authorized,
            "paused" => RemoteSubscriptionStatus.Paused,
            "cancelled" => RemoteSubscriptionStatus.Cancelled,
            "pending" => RemoteSubscriptionStatus.Pending,
            _ => RemoteSubscriptionStatus.Unknown
        };

        return new RemoteSubscriptionInfo(parsed.Id!, status, parsed.ExternalReference, parsed.NextPaymentDate);
    }

    private class PreapprovalResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("init_point")]
        public string? InitPoint { get; set; }

        [JsonPropertyName("external_reference")]
        public string? ExternalReference { get; set; }

        [JsonPropertyName("next_payment_date")]
        public DateTime? NextPaymentDate { get; set; }
    }
}
