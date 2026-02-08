using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// Rule aggregate root representing a game rule for move validation.
/// </summary>
/// <remarks>
/// Rules are used by Arbitro agent for real-time move validation and conflict detection.
/// Issue #3759: Rules Arbitration Engine
/// </remarks>
internal sealed class Rule : AggregateRoot<Guid>
{
    public Guid GameId { get; private set; }
    public string RuleName { get; private set; }
    public string Description { get; private set; }
    public RuleType Type { get; private set; }
    public int PrecedenceLevel { get; private set; }
    public string ApplicableContext { get; private set; }
    public string ValidationPattern { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable CS8618
    private Rule() : base()
#pragma warning restore CS8618
    {
    }

    /// <summary>
    /// Creates a new rule with specified properties.
    /// </summary>
    public Rule(
        Guid id,
        Guid gameId,
        string ruleName,
        string description,
        RuleType type,
        int precedenceLevel,
        string applicableContext,
        string validationPattern,
        bool isActive = true) : base(id)
    {
        if (string.IsNullOrWhiteSpace(ruleName))
            throw new ArgumentException("Rule name cannot be empty", nameof(ruleName));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Rule description cannot be empty", nameof(description));

        if (precedenceLevel < 0)
            throw new ArgumentOutOfRangeException(nameof(precedenceLevel), "Precedence level must be non-negative");

        ArgumentNullException.ThrowIfNull(type);

        GameId = gameId;
        RuleName = ruleName.Trim();
        Description = description.Trim();
        Type = type;
        PrecedenceLevel = precedenceLevel;
        ApplicableContext = applicableContext?.Trim() ?? string.Empty;
        ValidationPattern = validationPattern?.Trim() ?? string.Empty;
        IsActive = isActive;
        CreatedAt = DateTime.UtcNow;

        AddDomainEvent(new RuleCreatedEvent(id, gameId, ruleName, type.Value));
    }

    /// <summary>
    /// Updates rule description and validation pattern.
    /// </summary>
    public void Update(string description, string validationPattern, int? precedenceLevel = null)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description cannot be empty", nameof(description));

        Description = description.Trim();
        ValidationPattern = validationPattern?.Trim() ?? ValidationPattern;

        if (precedenceLevel.HasValue)
        {
            if (precedenceLevel.Value < 0)
                throw new ArgumentOutOfRangeException(nameof(precedenceLevel), "Must be non-negative");

            PrecedenceLevel = precedenceLevel.Value;
        }

        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new RuleUpdatedEvent(Id, GameId, RuleName));
    }

    /// <summary>
    /// Activates the rule for use in validation.
    /// </summary>
    public void Activate()
    {
        if (IsActive)
            return;

        IsActive = true;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new RuleActivatedEvent(Id, GameId));
    }

    /// <summary>
    /// Deactivates the rule (soft delete).
    /// </summary>
    public void Deactivate()
    {
        if (!IsActive)
            return;

        IsActive = false;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new RuleDeactivatedEvent(Id, GameId));
    }

    /// <summary>
    /// Checks if rule applies to given context (e.g., piece type, board position).
    /// </summary>
    public bool AppliesTo(string context)
    {
        if (string.IsNullOrWhiteSpace(ApplicableContext))
            return true; // Universal rule

        return ApplicableContext.Contains(context, StringComparison.OrdinalIgnoreCase);
    }
}
