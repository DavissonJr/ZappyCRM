using WhatsappCrmIA.Domain.Enums;

namespace WhatsappCrmIA.Domain.Common;

public record PlanDefinition(
    PlanTier Tier,
    string DisplayName,
    decimal PriceMonthly,
    int MaxWhatsAppConnections,
    int MaxUsers,
    int AiCreditsPerUserPerMonth,
    string[] Highlights);

/// <summary>
/// Fonte única de verdade dos planos — preço, limites e créditos de IA por
/// usuário/mês. Pra mudar preço ou limite de um plano, é só editar aqui,
/// nada mais no sistema depende de outro lugar.
///
/// 1 crédito de IA = 1 resposta automática gerada pela IA pro cliente
/// (contando o "tool use" de agendamento como uma resposta só, mesmo que
/// internamente sejam duas chamadas à Anthropic). O custo da IA sai da sua
/// conta — os créditos existem pra manter isso previsível e sustentável.
///
/// Preços calibrados com base em pesquisa de mercado (CRMs com WhatsApp+IA
/// pra PME no Brasil giram entre R$99 e R$649/mês) — ajuste livremente aqui
/// conforme sua estratégia comercial.
/// </summary>
public static class PlanCatalog
{
    public const int UnlimitedMarker = int.MaxValue;

    public static readonly Dictionary<PlanTier, PlanDefinition> Plans = new()
    {
        [PlanTier.Starter] = new PlanDefinition(
            PlanTier.Starter, "Basic", 149.00m, 1, 2, 300,
            ["1 número de WhatsApp", "Até 2 usuários", "300 créditos de IA por usuário/mês",
             "IA que responde e agenda sozinha", "Dashboard e relatórios"]),

        [PlanTier.Pro] = new PlanDefinition(
            PlanTier.Pro, "Pro", 289.00m, 3, 6, 600,
            ["Até 3 números de WhatsApp", "Até 6 usuários", "600 créditos de IA por usuário/mês",
             "IA que responde e agenda sozinha", "Dashboard e relatórios completos",
             "Propostas comerciais geradas por IA"]),

        [PlanTier.Business] = new PlanDefinition(
            PlanTier.Business, "Business", 549.00m, UnlimitedMarker, UnlimitedMarker, 1000,
            ["Números de WhatsApp ilimitados", "Usuários ilimitados", "1000 créditos de IA por usuário/mês",
             "IA que responde e agenda sozinha", "Dashboard e relatórios completos",
             "Propostas comerciais geradas por IA", "Suporte prioritário"]),
    };

    public static PlanDefinition Get(PlanTier tier) => Plans.TryGetValue(tier, out var p) ? p : Plans[PlanTier.Starter];

    /// <summary>Todos os planos vendáveis — não existe mais plano trial/grátis.</summary>
    public static IEnumerable<PlanDefinition> Sellable => Plans.Values;
}
