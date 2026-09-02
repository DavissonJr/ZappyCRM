using WhatsappCrmIA.Domain.Common;

namespace WhatsappCrmIA.Domain.Entities;

/// <summary>
/// Configuração do agente de IA por tenant: persona, tom, regras de negócio,
/// e se a resposta deve ser enviada automaticamente ou aguardar aprovação humana.
/// A chave da Anthropic é global (do .env) — quem administra a plataforma
/// paga por todo mundo, embutido na mensalidade de cada plano.
/// </summary>
public class AiAgentConfig : BaseEntity
{
    public string AgentName { get; set; } = "Assistente Virtual";
    public string SystemPrompt { get; set; } = default!;
    public bool AutoReplyEnabled { get; set; } = true;
    public bool RequireHumanApproval { get; set; } = false;
    public string BusinessHours { get; set; } = "08:00-18:00"; // simplificado para o MVP
    public string? FallbackMessage { get; set; } = "Já te chamo, um momento!";
}
