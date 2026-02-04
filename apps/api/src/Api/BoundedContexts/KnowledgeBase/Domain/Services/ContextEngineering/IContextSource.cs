namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.ContextEngineering;

/// <summary>
/// Abstraction for a context source that can contribute to AI agent context assembly.
/// Issue #3491: Context Engineering Framework Implementation.
/// </summary>
/// <remarks>
/// Context sources include:
/// - Static Knowledge (game rules RAG from Qdrant)
/// - Dynamic Memory (conversation history from PostgreSQL)
/// - Agent State (game board snapshots)
/// - Tool Metadata (available actions registry)
/// </remarks>
public interface IContextSource
{
    /// <summary>
    /// Gets the unique identifier for this context source.
    /// </summary>
    string SourceId { get; }

    /// <summary>
    /// Gets the display name of this context source.
    /// </summary>
    string SourceName { get; }

    /// <summary>
    /// Gets the default priority for this source (higher = more important).
    /// Range: 0-100
    /// </summary>
    int DefaultPriority { get; }

    /// <summary>
    /// Retrieves context from this source based on the given request.
    /// </summary>
    /// <param name="request">The retrieval request with query and constraints.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Retrieved context items with relevance scores.</returns>
    Task<ContextRetrievalResult> RetrieveAsync(
        ContextRetrievalRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Estimates the token count for the given content without actually retrieving.
    /// Used for budget planning.
    /// </summary>
    Task<int> EstimateTokensAsync(
        ContextRetrievalRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if this source is available and healthy.
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Request for context retrieval from a source.
/// </summary>
public sealed record ContextRetrievalRequest
{
    /// <summary>
    /// The user's query or question.
    /// </summary>
    public required string Query { get; init; }

    /// <summary>
    /// Optional game ID for game-specific context.
    /// </summary>
    public Guid? GameId { get; init; }

    /// <summary>
    /// Optional user ID for user-specific context.
    /// </summary>
    public Guid? UserId { get; init; }

    /// <summary>
    /// Optional session ID for session-specific context.
    /// </summary>
    public Guid? SessionId { get; init; }

    /// <summary>
    /// Maximum number of tokens to retrieve.
    /// </summary>
    public int MaxTokens { get; init; } = 2000;

    /// <summary>
    /// Maximum number of items to retrieve.
    /// </summary>
    public int MaxItems { get; init; } = 10;

    /// <summary>
    /// Minimum relevance score (0.0-1.0).
    /// </summary>
    public double MinRelevance { get; init; } = 0.5;

    /// <summary>
    /// Optional embedding vector for similarity search.
    /// </summary>
    public float[]? QueryEmbedding { get; init; }

    /// <summary>
    /// Additional metadata for source-specific filtering.
    /// </summary>
    public IDictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Result of context retrieval from a source.
/// </summary>
public sealed record ContextRetrievalResult
{
    /// <summary>
    /// ID of the source that produced this result.
    /// </summary>
    public required string SourceId { get; init; }

    /// <summary>
    /// Retrieved context items.
    /// </summary>
    public required IReadOnlyList<RetrievedContextItem> Items { get; init; }

    /// <summary>
    /// Total tokens used by the retrieved items.
    /// </summary>
    public int TotalTokens { get; init; }

    /// <summary>
    /// Time taken for retrieval in milliseconds.
    /// </summary>
    public long RetrievalDurationMs { get; init; }

    /// <summary>
    /// Whether the retrieval was successful.
    /// </summary>
    public bool IsSuccess { get; init; } = true;

    /// <summary>
    /// Error message if retrieval failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates an empty successful result.
    /// </summary>
    public static ContextRetrievalResult Empty(string sourceId) => new()
    {
        SourceId = sourceId,
        Items = [],
        TotalTokens = 0,
        RetrievalDurationMs = 0,
        IsSuccess = true
    };

    /// <summary>
    /// Creates a failed result with error message.
    /// </summary>
    public static ContextRetrievalResult Failure(string sourceId, string errorMessage) => new()
    {
        SourceId = sourceId,
        Items = [],
        TotalTokens = 0,
        RetrievalDurationMs = 0,
        IsSuccess = false,
        ErrorMessage = errorMessage
    };
}

/// <summary>
/// A single item of retrieved context.
/// </summary>
public sealed record RetrievedContextItem
{
    /// <summary>
    /// Unique identifier for this item.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The actual content text.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Relevance score (0.0-1.0).
    /// </summary>
    public double Relevance { get; init; }

    /// <summary>
    /// Estimated token count for this content.
    /// </summary>
    public int TokenCount { get; init; }

    /// <summary>
    /// Type of content (e.g., "rule", "conversation", "game_state", "tool").
    /// </summary>
    public required string ContentType { get; init; }

    /// <summary>
    /// Timestamp when this content was created/updated.
    /// </summary>
    public DateTime? Timestamp { get; init; }

    /// <summary>
    /// Additional metadata for the item.
    /// </summary>
    public IDictionary<string, object>? Metadata { get; init; }
}
