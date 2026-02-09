using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.GameManagement.Domain.Entities;

/// <summary>
/// Aggregate for frequently asked questions about rule conflicts.
/// Stores pre-defined resolutions for common conflict patterns.
/// Issue #3761: Arbitro Agent Conflict Resolution and Edge Cases.
/// </summary>
internal sealed class RuleConflictFAQ : AggregateRoot<Guid>
{
    public Guid GameId { get; private set; }
    public ConflictType ConflictType { get; private set; }
    public string Pattern { get; private set; }
    public string Resolution { get; private set; }
    public int Priority { get; private set; }
    public int UsageCount { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable CS8618
    private RuleConflictFAQ() : base()
#pragma warning restore CS8618
    {
    }

    /// <summary>
    /// Creates a new conflict FAQ entry.
    /// </summary>
    public static RuleConflictFAQ Create(
        Guid id,
        Guid gameId,
        ConflictType conflictType,
        string pattern,
        string resolution,
        int priority,
        TimeProvider? timeProvider = null)
    {
        if (gameId == Guid.Empty)
            throw new ArgumentException("GameId cannot be empty", nameof(gameId));

        if (string.IsNullOrWhiteSpace(pattern))
            throw new ArgumentException("Pattern cannot be empty", nameof(pattern));

        if (string.IsNullOrWhiteSpace(resolution))
            throw new ArgumentException("Resolution cannot be empty", nameof(resolution));

        if (priority < 1 || priority > 10)
            throw new ArgumentException("Priority must be between 1 and 10", nameof(priority));

        var provider = timeProvider ?? TimeProvider.System;
        var now = provider.GetUtcNow().UtcDateTime;

        var faq = new RuleConflictFAQ
        {
            Id = id,
            GameId = gameId,
            ConflictType = conflictType,
            Pattern = pattern.Trim().ToLowerInvariant(),
            Resolution = resolution.Trim(),
            Priority = priority,
            UsageCount = 0,
            CreatedAt = now,
            UpdatedAt = now
        };

        faq.AddDomainEvent(new RuleConflictFAQCreatedEvent(id, gameId, pattern));

        return faq;
    }

    /// <summary>
    /// Increments usage counter when FAQ resolution is applied.
    /// </summary>
    public void RecordUsage(TimeProvider? timeProvider = null)
    {
        UsageCount++;
        var provider = timeProvider ?? TimeProvider.System;
        UpdatedAt = provider.GetUtcNow().UtcDateTime;

        AddDomainEvent(new RuleConflictFAQUsedEvent(Id, GameId, UsageCount));
    }

    /// <summary>
    /// Updates the resolution text.
    /// </summary>
    public void UpdateResolution(string newResolution, TimeProvider? timeProvider = null)
    {
        if (string.IsNullOrWhiteSpace(newResolution))
            throw new ArgumentException("Resolution cannot be empty", nameof(newResolution));

        Resolution = newResolution.Trim();
        var provider = timeProvider ?? TimeProvider.System;
        UpdatedAt = provider.GetUtcNow().UtcDateTime;

        AddDomainEvent(new RuleConflictFAQUpdatedEvent(Id, GameId, Pattern));
    }
}
