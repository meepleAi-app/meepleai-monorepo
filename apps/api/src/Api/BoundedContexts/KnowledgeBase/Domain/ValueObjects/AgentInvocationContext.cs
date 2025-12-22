using Api.BoundedContexts.KnowledgeBase.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Value object representing the execution context for an agent invocation.
/// Encapsulates all input data needed for agent execution.
/// </summary>
/// <remarks>
/// Immutable context passed to agent invocation methods.
/// Contains query, embeddings, and optional domain filters (game, chat thread).
/// </remarks>
internal sealed record AgentInvocationContext
{
    /// <summary>
    /// Unique identifier for this invocation (for tracking and auditing).
    /// </summary>
    public Guid InvocationId { get; init; }

    /// <summary>
    /// User's natural language query.
    /// </summary>
    public string Query { get; init; }

    /// <summary>
    /// Vector embedding of the query.
    /// </summary>
    public Vector QueryVector { get; init; }

    /// <summary>
    /// Candidate embeddings to search against.
    /// </summary>
    public List<Embedding> CandidateEmbeddings { get; init; }

    /// <summary>
    /// Optional game context for filtering results.
    /// </summary>
    public Guid? GameId { get; init; }

    /// <summary>
    /// Optional chat thread context for conversation continuity.
    /// </summary>
    public Guid? ChatThreadId { get; init; }

    /// <summary>
    /// Optional user ID for personalization and access control.
    /// </summary>
    public Guid? UserId { get; init; }

    /// <summary>
    /// Timestamp when this context was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Creates a new agent invocation context.
    /// </summary>
    public AgentInvocationContext(
        string query,
        Vector queryVector,
        List<Embedding> candidateEmbeddings,
        Guid? gameId = null,
        Guid? chatThreadId = null,
        Guid? userId = null)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be empty", nameof(query));

        ArgumentNullException.ThrowIfNull(queryVector);
        ArgumentNullException.ThrowIfNull(candidateEmbeddings);

        InvocationId = Guid.NewGuid();
        Query = query.Trim();
        QueryVector = queryVector;
        CandidateEmbeddings = candidateEmbeddings;
        GameId = gameId;
        ChatThreadId = chatThreadId;
        UserId = userId;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Checks if this invocation has game context.
    /// </summary>
    public bool HasGameContext => GameId.HasValue;

    /// <summary>
    /// Checks if this invocation has chat thread context.
    /// </summary>
    public bool HasChatContext => ChatThreadId.HasValue;

    /// <summary>
    /// Checks if this invocation has user context.
    /// </summary>
    public bool HasUserContext => UserId.HasValue;

    /// <summary>
    /// Gets the number of candidate embeddings available for search.
    /// </summary>
    public int CandidateCount => CandidateEmbeddings.Count;
}
