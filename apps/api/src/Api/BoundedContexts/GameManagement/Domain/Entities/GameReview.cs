using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.GameManagement.Domain.Entities;

/// <summary>
/// Aggregate for user-submitted game reviews.
/// Each user may submit at most one review per game.
/// Issue #4904: Game reviews API endpoint.
/// </summary>
internal sealed class GameReview : AggregateRoot<Guid>
{
    public Guid SharedGameId { get; private set; }
    public Guid UserId { get; private set; }
    public string AuthorName { get; private set; }
    public int Rating { get; private set; }
    public string Content { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

#pragma warning disable CS8618
    private GameReview() : base()
#pragma warning restore CS8618
    {
    }

    /// <summary>
    /// Creates a new game review.
    /// </summary>
    public static GameReview Create(
        Guid id,
        Guid sharedGameId,
        Guid userId,
        string authorName,
        int rating,
        string content,
        TimeProvider? timeProvider = null)
    {
        if (sharedGameId == Guid.Empty)
            throw new ArgumentException("SharedGameId cannot be empty", nameof(sharedGameId));

        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty", nameof(userId));

        if (string.IsNullOrWhiteSpace(authorName))
            throw new ArgumentException("AuthorName cannot be empty", nameof(authorName));

        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content cannot be empty", nameof(content));

        if (rating < 1 || rating > 10)
            throw new ArgumentException("Rating must be between 1 and 10", nameof(rating));

        var provider = timeProvider ?? TimeProvider.System;
        var now = provider.GetUtcNow().UtcDateTime;

        var review = new GameReview
        {
            Id = id,
            SharedGameId = sharedGameId,
            UserId = userId,
            AuthorName = authorName.Trim(),
            Rating = rating,
            Content = content.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };

        review.AddDomainEvent(new GameReviewCreatedEvent(id, sharedGameId, userId, rating));

        return review;
    }

    /// <summary>
    /// Reconstructs a GameReview from persistence (no domain events).
    /// </summary>
    internal static GameReview Reconstitute(
        Guid id,
        Guid sharedGameId,
        Guid userId,
        string authorName,
        int rating,
        string content,
        DateTime createdAt,
        DateTime updatedAt)
        => new GameReview
        {
            Id = id,
            SharedGameId = sharedGameId,
            UserId = userId,
            AuthorName = authorName,
            Rating = rating,
            Content = content,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
}
