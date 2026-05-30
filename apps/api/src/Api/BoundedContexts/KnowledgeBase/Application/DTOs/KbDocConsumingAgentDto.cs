namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// Read DTO for the "Used by" tab in KbDocDetailPanel.
/// Identifies an agent that explicitly consumes a given PDF document via AgentDefinition.KbCardIds.
/// Issue #1651: F3-FU-2 — Used-by tab.
/// </summary>
internal sealed record KbDocConsumingAgentDto(
    Guid Id,
    string Name,
    string Type,
    bool IsActive,
    string Status,
    bool IsSystemDefined,
    string? TypologySlug,
    Guid? GameId,
    string? GameName,
    int InvocationCount,
    DateTime? LastInvokedAt
);
