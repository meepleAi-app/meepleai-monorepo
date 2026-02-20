using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a user submits a game review.
/// Issue #4904: Game reviews API endpoint.
/// </summary>
internal sealed class GameReviewCreatedEvent : DomainEventBase
{
    public Guid ReviewId { get; }
    public Guid SharedGameId { get; }
    public Guid UserId { get; }
    public int Rating { get; }

    public GameReviewCreatedEvent(Guid reviewId, Guid sharedGameId, Guid userId, int rating)
    {
        ReviewId = reviewId;
        SharedGameId = sharedGameId;
        UserId = userId;
        Rating = rating;
    }
}
