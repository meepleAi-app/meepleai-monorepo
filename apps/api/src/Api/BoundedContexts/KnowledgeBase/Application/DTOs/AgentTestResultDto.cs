namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// DTO for agent test result.
/// Issue #3379: Agent Test Results History &amp; Persistence.
/// </summary>
public record AgentTestResultDto(
    Guid Id,
    Guid TypologyId,
    string? StrategyOverride,
    string ModelUsed,
    string Query,
    string Response,
    double ConfidenceScore,
    int TokensUsed,
    decimal CostEstimate,
    int LatencyMs,
    string? CitationsJson,
    DateTime ExecutedAt,
    Guid ExecutedBy,
    string? Notes,
    bool IsSaved
);

/// <summary>
/// DTO for a list of agent test results with pagination info.
/// </summary>
public record AgentTestResultListDto(
    List<AgentTestResultDto> Results,
    int TotalCount,
    int Skip,
    int Take
);
