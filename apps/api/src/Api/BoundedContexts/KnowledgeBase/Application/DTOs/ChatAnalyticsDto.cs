namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// DTO for chat analytics response.
/// Issue #3714: System-wide chat analytics for admin dashboard.
/// </summary>
public record ChatAnalyticsDto
{
    public required int TotalThreads { get; init; }
    public required int ActiveThreads { get; init; }
    public required int ClosedThreads { get; init; }
    public required int TotalMessages { get; init; }
    public required double AvgMessagesPerThread { get; init; }
    public required int UniqueUsers { get; init; }
    public required Dictionary<string, int> ThreadsByAgentType { get; init; }
    public required List<DailyChatStats> ThreadsByDay { get; init; }
}

/// <summary>
/// Daily chat thread stats for time series.
/// </summary>
public record DailyChatStats
{
    public required DateOnly Date { get; init; }
    public required int TotalCount { get; init; }
    public required int ActiveCount { get; init; }
    public required int ClosedCount { get; init; }
    public required int MessageCount { get; init; }
}
