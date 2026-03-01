namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// Response DTO for agent slot information per user.
/// Issue #4771: Agent Slots Endpoint + Quota System.
/// </summary>
internal record UserAgentSlotsDto(
    int Total,
    int Used,
    int Available,
    IReadOnlyList<AgentSlotDto> Slots
);

/// <summary>
/// Individual slot details. An occupied slot links to its agent; empty slots have null fields.
/// </summary>
internal record AgentSlotDto(
    int SlotIndex,
    Guid? AgentId,
    string? AgentName,
    Guid? GameId,
    string Status
);
