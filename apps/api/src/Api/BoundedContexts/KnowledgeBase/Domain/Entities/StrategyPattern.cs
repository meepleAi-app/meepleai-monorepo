using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// Represents a cached strategy pattern for AI agent decision-making.
/// Issue #3493: PostgreSQL Schema Extensions for Multi-Agent System.
/// </summary>
/// <remarks>
/// Stores pre-computed strategy patterns with relevance scoring
/// for context-aware move recommendations.
/// </remarks>
internal sealed class StrategyPattern : Entity<Guid>
{
    public Guid GameId { get; private set; }
    public string PatternName { get; private set; }
    public string? ApplicablePhase { get; private set; }
    public string? Description { get; private set; }
    public double? EvaluationScore { get; private set; }
    public string? BoardConditionsJson { get; private set; }
    public string? MoveSequenceJson { get; private set; }
    public string? Source { get; private set; }
    public Vector? Embedding { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable CS8618
    private StrategyPattern() : base()
#pragma warning restore CS8618
    {
    }

    /// <summary>
    /// Creates a new strategy pattern.
    /// </summary>
    public StrategyPattern(
        Guid id,
        Guid gameId,
        string patternName,
        string? applicablePhase = null,
        string? description = null,
        double? evaluationScore = null,
        string? boardConditionsJson = null,
        string? moveSequenceJson = null,
        string? source = null,
        Vector? embedding = null) : base(id)
    {
        if (gameId == Guid.Empty)
            throw new ArgumentException("Game ID cannot be empty", nameof(gameId));
        if (string.IsNullOrWhiteSpace(patternName))
            throw new ArgumentException("Pattern name cannot be empty", nameof(patternName));

        GameId = gameId;
        PatternName = patternName;
        ApplicablePhase = applicablePhase;
        Description = description;
        EvaluationScore = evaluationScore;
        BoardConditionsJson = boardConditionsJson;
        MoveSequenceJson = moveSequenceJson;
        Source = source;
        Embedding = embedding;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the evaluation score for this pattern.
    /// </summary>
    public void UpdateScore(double score)
    {
        EvaluationScore = score;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the embedding for this pattern.
    /// </summary>
    public void SetEmbedding(Vector embedding)
    {
        Embedding = embedding ?? throw new ArgumentNullException(nameof(embedding));
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates pattern details.
    /// </summary>
    public void Update(
        string? description = null,
        string? boardConditionsJson = null,
        string? moveSequenceJson = null)
    {
        if (description != null)
            Description = description;
        if (boardConditionsJson != null)
            BoardConditionsJson = boardConditionsJson;
        if (moveSequenceJson != null)
            MoveSequenceJson = moveSequenceJson;

        UpdatedAt = DateTime.UtcNow;
    }
}
