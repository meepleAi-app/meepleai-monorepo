namespace Api.BoundedContexts.KnowledgeBase.Application.ContextEngineering.DTOs;

/// <summary>
/// DTO for assembled context result.
/// Issue #3491: Context Engineering Framework Implementation.
/// </summary>
public sealed record AssembledContextDto
{
    /// <summary>
    /// The original query.
    /// </summary>
    public required string Query { get; init; }

    /// <summary>
    /// Formatted context string ready for prompt injection.
    /// </summary>
    public required string FormattedContext { get; init; }

    /// <summary>
    /// Individual context items with metadata.
    /// </summary>
    public required IReadOnlyList<ContextItemDto> Items { get; init; }

    /// <summary>
    /// Total tokens used in the assembled context.
    /// </summary>
    public int TotalTokens { get; init; }

    /// <summary>
    /// Budget allocation details.
    /// </summary>
    public required BudgetSnapshotDto Budget { get; init; }

    /// <summary>
    /// Performance and quality metrics.
    /// </summary>
    public required MetricsDto Metrics { get; init; }

    /// <summary>
    /// When the context was assembled.
    /// </summary>
    public DateTime AssembledAt { get; init; }
}

/// <summary>
/// DTO for a single context item.
/// </summary>
public sealed record ContextItemDto
{
    /// <summary>
    /// Source that provided this item.
    /// </summary>
    public required string SourceId { get; init; }

    /// <summary>
    /// Display name of the source.
    /// </summary>
    public required string SourceName { get; init; }

    /// <summary>
    /// The actual content.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Relevance score (0.0-1.0).
    /// </summary>
    public double Relevance { get; init; }

    /// <summary>
    /// Token count for this item.
    /// </summary>
    public int TokenCount { get; init; }

    /// <summary>
    /// Content type identifier.
    /// </summary>
    public required string ContentType { get; init; }

    /// <summary>
    /// Priority assigned to this item's source.
    /// </summary>
    public int Priority { get; init; }

    /// <summary>
    /// When this content was created/updated.
    /// </summary>
    public DateTime? Timestamp { get; init; }
}

/// <summary>
/// DTO for budget allocation snapshot.
/// </summary>
public sealed record BudgetSnapshotDto
{
    /// <summary>
    /// Total available token budget.
    /// </summary>
    public int TotalBudget { get; init; }

    /// <summary>
    /// Tokens allocated across sources.
    /// </summary>
    public int AllocatedTokens { get; init; }

    /// <summary>
    /// Tokens actually used.
    /// </summary>
    public int UsedTokens { get; init; }

    /// <summary>
    /// Remaining budget after usage.
    /// </summary>
    public int RemainingBudget { get; init; }

    /// <summary>
    /// Per-source allocation details.
    /// </summary>
    public required IReadOnlyDictionary<string, SourceAllocationDto> Sources { get; init; }
}

/// <summary>
/// DTO for a single source's allocation.
/// </summary>
public sealed record SourceAllocationDto
{
    /// <summary>
    /// Priority level of this source.
    /// </summary>
    public int Priority { get; init; }

    /// <summary>
    /// Tokens allocated to this source.
    /// </summary>
    public int Allocated { get; init; }

    /// <summary>
    /// Tokens actually used by this source.
    /// </summary>
    public int Used { get; init; }

    /// <summary>
    /// Remaining allocation.
    /// </summary>
    public int Remaining { get; init; }
}

/// <summary>
/// DTO for assembly performance metrics.
/// </summary>
public sealed record MetricsDto
{
    /// <summary>
    /// Total assembly duration in milliseconds.
    /// </summary>
    public long TotalDurationMs { get; init; }

    /// <summary>
    /// Number of items retrieved.
    /// </summary>
    public int ItemsRetrieved { get; init; }

    /// <summary>
    /// Number of sources queried.
    /// </summary>
    public int SourcesQueried { get; init; }

    /// <summary>
    /// Number of sources that succeeded.
    /// </summary>
    public int SourcesSucceeded { get; init; }

    /// <summary>
    /// Per-source metrics.
    /// </summary>
    public required IReadOnlyDictionary<string, SourceMetricsDto> Sources { get; init; }
}

/// <summary>
/// DTO for a single source's metrics.
/// </summary>
public sealed record SourceMetricsDto
{
    /// <summary>
    /// Retrieval duration in milliseconds.
    /// </summary>
    public long DurationMs { get; init; }

    /// <summary>
    /// Number of items retrieved from this source.
    /// </summary>
    public int ItemCount { get; init; }

    /// <summary>
    /// Whether the source was available.
    /// </summary>
    public bool IsAvailable { get; init; }

    /// <summary>
    /// Error message if retrieval failed.
    /// </summary>
    public string? Error { get; init; }
}
