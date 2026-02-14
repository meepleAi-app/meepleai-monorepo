using System.Diagnostics.CodeAnalysis;
using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.Administration.Domain.ValueObjects;

/// <summary>
/// Represents an AI-generated user insight with action-oriented metadata.
/// Immutable value object following DDD patterns.
/// </summary>
public sealed record AIInsight
{
    /// <summary>
    /// Unique identifier for the insight instance.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Type of insight (backlog, rules reminder, recommendation, streak nudge).
    /// </summary>
    public required InsightType Type { get; init; }

    /// <summary>
    /// Display title for the insight (max 100 characters).
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Detailed description explaining the insight (max 500 characters).
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Call-to-action button label (e.g., "Scopri →").
    /// </summary>
    public required string ActionLabel { get; init; }

    /// <summary>
    /// Target URL for the insight action (e.g., "/library?filter=unplayed").
    /// </summary>
    public required string ActionUrl { get; init; }

    /// <summary>
    /// Priority score (1-10, higher = more important).
    /// Used for sorting insights in UI.
    /// </summary>
    public required int Priority { get; init; }

    /// <summary>
    /// Timestamp when the insight was generated.
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// Factory method to create a new AI insight with validation.
    /// </summary>
    /// <param name="type">Type of insight</param>
    /// <param name="title">Display title (max 100 chars)</param>
    /// <param name="description">Description (max 500 chars)</param>
    /// <param name="actionLabel">CTA button label</param>
    /// <param name="actionUrl">Target URL</param>
    /// <param name="priority">Priority score (1-10)</param>
    /// <returns>Validated AIInsight instance</returns>
    /// <exception cref="ArgumentException">Thrown when validation fails</exception>
    public static AIInsight Create(
        InsightType type,
        string title,
        string description,
        string actionLabel,
        string actionUrl,
        int priority)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionUrl);

        if (title.Length > 100)
            throw new ValidationException(nameof(title), "Title must not exceed 100 characters");

        if (description.Length > 500)
            throw new ValidationException(nameof(description), "Description must not exceed 500 characters");

        if (priority is < 1 or > 10)
            throw new ValidationException(nameof(priority), "Priority must be between 1 and 10");

        if (!actionUrl.StartsWith('/'))
            throw new ValidationException(nameof(actionUrl), "ActionUrl must be a relative path starting with /");

        return new AIInsight
        {
            Id = Guid.NewGuid().ToString(),
            Type = type,
            Title = title,
            Description = description,
            ActionLabel = actionLabel,
            ActionUrl = actionUrl,
            Priority = priority,
            CreatedAt = DateTime.UtcNow
        };
    }
}
