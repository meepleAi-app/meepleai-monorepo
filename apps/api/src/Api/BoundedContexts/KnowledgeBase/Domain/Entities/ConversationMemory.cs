using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// Represents a conversation memory entry for temporal context retrieval.
/// Issue #3493: PostgreSQL Schema Extensions for Multi-Agent System.
/// </summary>
/// <remarks>
/// Stores conversation history with embeddings for semantic search
/// and temporal scoring for recency-based context assembly.
/// </remarks>
internal sealed class ConversationMemory : Entity<Guid>
{
    public Guid SessionId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? GameId { get; private set; }
    public string Content { get; private set; }
    public string MessageType { get; private set; }
    public DateTime Timestamp { get; private set; }
    public Vector? Embedding { get; private set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable CS8618
    private ConversationMemory() : base()
#pragma warning restore CS8618
    {
    }

    /// <summary>
    /// Creates a new conversation memory entry.
    /// </summary>
    public ConversationMemory(
        Guid id,
        Guid sessionId,
        Guid userId,
        Guid? gameId,
        string content,
        string messageType,
        DateTime? timestamp = null,
        Vector? embedding = null) : base(id)
    {
        if (sessionId == Guid.Empty)
            throw new ArgumentException("Session ID cannot be empty", nameof(sessionId));
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty", nameof(userId));
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content cannot be empty", nameof(content));
        if (string.IsNullOrWhiteSpace(messageType))
            throw new ArgumentException("Message type cannot be empty", nameof(messageType));

        SessionId = sessionId;
        UserId = userId;
        GameId = gameId;
        Content = content;
        MessageType = messageType;
        Timestamp = timestamp ?? DateTime.UtcNow;
        Embedding = embedding;
    }

    /// <summary>
    /// Calculates a temporal decay score based on how recent the memory is.
    /// </summary>
    /// <param name="referenceTime">The reference time point (typically now).</param>
    /// <param name="decayWindow">The time window for decay (older items get lower scores).</param>
    /// <returns>A score between 0 and 1, where 1 is most recent.</returns>
    public double CalculateTemporalScore(DateTime referenceTime, TimeSpan decayWindow)
    {
        var age = referenceTime - Timestamp;
        if (age < TimeSpan.Zero)
            return 1.0;

        if (age >= decayWindow)
            return 0.0;

        // Exponential decay: score = e^(-age/halfLife)
        var halfLife = decayWindow.TotalHours / 3; // Decay to ~5% at end of window
        var score = Math.Exp(-age.TotalHours / halfLife);
        return Math.Max(0.0, Math.Min(1.0, score));
    }

    /// <summary>
    /// Updates the embedding for this memory entry.
    /// </summary>
    public void SetEmbedding(Vector embedding)
    {
        Embedding = embedding ?? throw new ArgumentNullException(nameof(embedding));
    }
}
