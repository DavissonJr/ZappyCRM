using WhatsappCrmIA.Domain.Enums;

namespace WhatsappCrmIA.Domain.Common;

public record PlanDefinition(
    PlanTier Tier,
    string DisplayName,
    decimal PriceMonthly,
    int MaxWhatsAppConnections,
    int MaxUsers,
    string[] Highlights);

/// <summary>
/// Fonte única de verdade dos planos do SaaS — preço e limites. Pra mudar
/// preço ou limite de um plano, é só editar aqui, nada mais no sistema
/// depende de outro lugar.
/// </summary>
public static class PlanCatalog
{
    public const int UnlimitedMarker = int.MaxValue;

    public static readonly Dictionary<PlanTier, PlanDefinition> Plans = new()
    {
        [PlanTier.Trial] = new PlanDefinition(
            PlanTier.Trial, "Trial (14 dias grátis)", 0m, 1, 2,
            ["1 número de WhatsApp", "2 usuários", "14 dias grátis"]),

        [PlanTier.Starter] = new PlanDefinition(
            PlanTier.Starter, "Starter", 5.00m, 1, 2, // TODO: preço temporário de teste — volte pra 97.00m depois!
            ["1 número de WhatsApp", "Até 2 usuários", "IA com agendamento automático"]),

        [PlanTier.Pro] = new PlanDefinition(
            PlanTier.Pro, "Pro", 197.00m, 3, 8,
            ["Até 3 números de WhatsApp", "Até 8 usuários", "Campanhas em lote", "Dashboard completo"]),

        [PlanTier.Business] = new PlanDefinition(
            PlanTier.Business, "Business", 397.00m, UnlimitedMarker, UnlimitedMarker,
            ["Números ilimitados", "Usuários ilimitados", "Suporte prioritário"]),
    };

    public static PlanDefinition Get(PlanTier tier) => Plans[tier];

    /// <summary>Planos que podem ser assinados de verdade (Trial não tem preço, não aparece pra assinar).</summary>
    public static IEnumerable<PlanDefinition> Sellable => Plans.Values.Where(p => p.Tier != PlanTier.Trial);
}
