namespace WhatsappCrmIA.Application.DTOs;

public record AdminPlanOptionDto(
    string Tier, string DisplayName, int MaxWhatsAppConnections, int MaxUsers);

public record AdminTenantSummaryDto(
    Guid Id,
    string Name,
    string Segment,
    string Plan,
    bool IsActive,
    DateTime CreatedAtUtc,
    string? OwnerName,
    string? OwnerEmail,
    int UserCount,
    int WhatsAppConnectionCount,
    int ConnectedWhatsAppCount,
    int ContactCount,
    int ConversationCount,
    int MessageCount,
    int AppointmentCount,
    int ProposalCount,
    long TotalAiInputTokens,
    long TotalAiOutputTokens,
    decimal TotalAiEstimatedCostUsd,
    DateTime? LastActivityUtc);
