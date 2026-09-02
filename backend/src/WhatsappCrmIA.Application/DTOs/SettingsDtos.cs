namespace WhatsappCrmIA.Application.DTOs;

public record MeDto(Guid Id, string FullName, string Email, string Role);

public record TenantSettingsDto(Guid Id, string Name, string Segment, string Plan);

public record AiAgentConfigDto(
    string AgentName,
    string SystemPrompt,
    bool AutoReplyEnabled,
    bool RequireHumanApproval,
    string BusinessHours,
    string? FallbackMessage);

public record TeamMemberDto(Guid Id, string FullName, string Email, string Role, bool IsActive);
