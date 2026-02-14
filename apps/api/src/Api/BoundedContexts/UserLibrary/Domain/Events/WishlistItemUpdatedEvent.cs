using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.UserLibrary.Domain.Events;

internal sealed class WishlistItemUpdatedEvent : DomainEventBase
{
    public Guid WishlistItemId { get; }
    public Guid UserId { get; }
    public Guid GameId { get; }
    public int NewPriority { get; }

    public WishlistItemUpdatedEvent(Guid wishlistItemId, Guid userId, Guid gameId, int newPriority)
    {
        WishlistItemId = wishlistItemId;
        UserId = userId;
        GameId = gameId;
        NewPriority = newPriority;
    }
}
