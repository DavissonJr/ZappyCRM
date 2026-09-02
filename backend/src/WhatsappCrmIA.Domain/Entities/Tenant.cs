using WhatsappCrmIA.Domain.Enums;

namespace WhatsappCrmIA.Domain.Entities;

/// <summary>
/// Representa a empresa cliente (clínica, oficina, escritório, imobiliária...).
/// Nota: Tenant não herda de BaseEntity pois ele é a raiz do isolamento multi-tenant.
/// Empresas são criadas pelo admin da plataforma (não existe auto-cadastro público) —
/// o plano é ajustado manualmente por quem administra o sistema, sem cobrança automática.
/// </summary>
public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = default!;
    public string Segment { get; set; } = default!; // ex: "clinica", "oficina", "advocacia", "imobiliaria"
    public PlanTier Plan { get; set; } = PlanTier.Starter;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public decimal AiCreditsBalanceUsd { get; set; } = 5.00m;

    /// <summary>
    /// Fuso horário do tenant (formato IANA, ex: "America/Sao_Paulo"), usado
    /// pra saber se está dentro do horário de atendimento configurado no
    /// agente de IA. Assume horário de Brasília por padrão.
    /// </summary>
    public string TimeZoneId { get; set; } = "America/Sao_Paulo";

    // Config do agente de IA para este tenant
    public AiAgentConfig? AiAgentConfig { get; set; }

    // Um tenant pode conectar vários números de WhatsApp
    public ICollection<WhatsAppConnection> WhatsAppConnections { get; set; } = new List<WhatsAppConnection>();
}
