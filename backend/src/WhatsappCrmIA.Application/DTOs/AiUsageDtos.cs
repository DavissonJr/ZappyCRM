namespace WhatsappCrmIA.Application.DTOs;

/// <summary>
/// Uso de créditos de IA do mês atual, contra o limite do plano da empresa.
/// Não mostra custo em dinheiro pro cliente — isso é informação interna,
/// só de quem administra a plataforma.
/// </summary>
public record AiCreditsStatusDto(
    string PlanName,
    int CreditsUsedThisMonth,
    int CreditsBudgetThisMonth,
    DateTime MonthStartUtc);
