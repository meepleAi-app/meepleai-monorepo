namespace Api.BoundedContexts.GameManagement.Domain.ValueObjects;

/// <summary>
/// Value object representing a rule dispute arbitration entry during a live board game session.
/// Stores the player's question, the AI agent's verdict, and any relevant rule references.
/// Intended to be stored as JSONB on LiveGameSession and PauseSnapshot entities.
/// </summary>
public sealed record RuleDisputeEntry
{
    /// <summary>
    /// Unique identifier for this dispute entry.
    /// Auto-generated if Guid.Empty is provided.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// The question or dispute raised by the player (e.g., "Can I play 2 cards per turn?").
    /// </summary>
    public string Description { get; init; }

    /// <summary>
    /// The AI agent's ruling or verdict on the dispute.
    /// </summary>
    public string Verdict { get; init; }

    /// <summary>
    /// References to specific rulebook pages or sections that support the verdict.
    /// </summary>
    public List<string> RuleReferences { get; init; }

    /// <summary>
    /// Name of the player who raised the dispute.
    /// </summary>
    public string RaisedByPlayerName { get; init; }

    /// <summary>
    /// When the dispute was raised and resolved.
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Creates a new RuleDisputeEntry value object with validation.
    /// </summary>
    /// <param name="id">Unique identifier; auto-generated if Guid.Empty.</param>
    /// <param name="description">The rule question raised by the player. Cannot be empty.</param>
    /// <param name="verdict">The agent's ruling. Cannot be empty.</param>
    /// <param name="ruleReferences">Rulebook references supporting the verdict. Defaults to empty list if null.</param>
    /// <param name="raisedByPlayerName">Name of the player who raised the dispute. Cannot be empty.</param>
    /// <param name="timestamp">When the dispute occurred.</param>
    public RuleDisputeEntry(
        Guid id,
        string description,
        string verdict,
        List<string> ruleReferences,
        string raisedByPlayerName,
        DateTime timestamp)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required.", nameof(description));

        if (string.IsNullOrWhiteSpace(verdict))
            throw new ArgumentException("Verdict is required.", nameof(verdict));

        if (string.IsNullOrWhiteSpace(raisedByPlayerName))
            throw new ArgumentException("Player name is required.", nameof(raisedByPlayerName));

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        Description = description;
        Verdict = verdict;
        RuleReferences = ruleReferences ?? new List<string>();
        RaisedByPlayerName = raisedByPlayerName;
        Timestamp = timestamp;
    }
}
