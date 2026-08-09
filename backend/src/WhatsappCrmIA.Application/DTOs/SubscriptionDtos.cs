namespace WhatsappCrmIA.Application.DTOs;

public record PlanDto(
    string Tier, string DisplayName, decimal PriceMonthly,
    int MaxWhatsAppConnections, int MaxUsers, string[] Highlights, bool IsCurrent);

public record SubscriptionStatusDto(
    string CurrentPlan,
    string SubscriptionStatus,
    DateTime? TrialEndsAtUtc,
    DateTime? CurrentPeriodEndUtc,
    int DaysLeftInTrial,
    int CurrentWhatsAppConnections,
    int CurrentUsers,
    IReadOnlyList<PlanDto> AvailablePlans);
