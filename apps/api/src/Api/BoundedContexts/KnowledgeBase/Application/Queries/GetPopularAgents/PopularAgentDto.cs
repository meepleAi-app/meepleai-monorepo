namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetPopularAgents;

/// <summary>
/// Lightweight DTO for the /api/v1/agents/popular endpoint
/// (Wave 3 Phase 1, PR #732 §4.3.3 / Issue #805).
/// </summary>
/// <remarks>
/// Powers the SP4 /discover route's "Popular agents" rail (frontend
/// <c>useDiscoverPopularAgents</c>).
///
/// Schema reality v1 carryover: there is no per-user installation tracking
/// entity (no <c>AgentInstallation</c> aggregate); <c>InstallCount</c> is
/// always returned as <c>0</c> until that surface lands. Sort therefore
/// effectively collapses to <c>InvocationCount DESC</c> in v1. This is
/// documented as Gate B in the implementation note for the FE Phase 0.5
/// contract — the response shape is stable so the FE rail can render today
/// and adopt the real install metric without a re-fetch shape change.
/// </remarks>
internal sealed record PopularAgentDto(
    Guid Id,
    string Name,
    Guid? GameId,
    string? GameName,
    int InstallCount,
    int InvocationCount
);
