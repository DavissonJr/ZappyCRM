using WhatsappCrmIA.Domain.Enums;

namespace WhatsappCrmIA.Domain.Entities;

/// <summary>
/// Representa a empresa cliente do SaaS (clínica, oficina, escritório, imobiliária...).
/// Nota: Tenant não herda de BaseEntity pois ele é a raiz do isolamento multi-tenant.
/// </summary>
public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = default!;
    public string Segment { get; set; } = default!; // ex: "clinica", "oficina", "advocacia", "imobiliaria"
    public PlanTier Plan { get; set; } = PlanTier.Trial;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public decimal AiCreditsBalanceUsd { get; set; } = 5.00m;

    /// <summary>
    /// Fuso horário do tenant (formato IANA, ex: "America/Sao_Paulo"), usado
    /// pra saber se está dentro do horário de atendimento configurado no
    /// agente de IA. Assume horário de Brasília por padrão.
    /// </summary>
    public string TimeZoneId { get; set; } = "America/Sao_Paulo";

    // ---- Assinatura (Mercado Pago) ----
    public SubscriptionStatus SubscriptionStatus { get; set; } = SubscriptionStatus.TrialActive;
    public DateTime TrialEndsAtUtc { get; set; } = DateTime.UtcNow.AddDays(14);
    public string? MercadoPagoPreapprovalId { get; set; }
    public DateTime? CurrentPeriodEndUtc { get; set; }
    public DateTime? SubscriptionCancelledAtUtc { get; set; }

    // Config do agente de IA para este tenant
    public AiAgentConfig? AiAgentConfig { get; set; }

    // Um tenant pode conectar vários números de WhatsApp
    public ICollection<WhatsAppConnection> WhatsAppConnections { get; set; } = new List<WhatsAppConnection>();
}
