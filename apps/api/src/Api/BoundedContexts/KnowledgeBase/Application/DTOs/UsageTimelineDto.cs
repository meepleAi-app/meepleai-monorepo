namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// Timeline bucket containing per-source request counts and cost.
/// Issue #5078: Admin usage page — request timeline chart.
/// </summary>
public sealed record TimelineBucketDto(
    DateTime Bucket,
    int Manual,
    int RagPipeline,
    int EventDriven,
    int AutomatedTest,
    int AgentTask,
    int AdminOperation,
    decimal TotalCostUsd
);

/// <summary>
/// Timeline response for the usage dashboard chart.
/// Issue #5078: Admin usage page — request timeline chart.
/// </summary>
public sealed record UsageTimelineDto(
    IReadOnlyList<TimelineBucketDto> Buckets,
    string Period,
    bool GroupedByHour,
    int TotalRequests,
    decimal TotalCostUsd
);
