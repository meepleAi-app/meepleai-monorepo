namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetTopAgents;

/// <summary>
/// DTO for a top agent entry on the Discover dashboard.
/// Name, GameName and AgentType are empty in the MVP because there is no
/// AgentDefinition entity directly joinable in AgentSession.
/// Follow-up when the KB BC introduces a proper install model.
/// Issue #728.
/// </summary>
internal sealed record TopAgentDto(
    Guid Id,
    string Name,
    string GameName,
    string AgentType,
    int InstallCount,
    DateTime CreatedAt
);
