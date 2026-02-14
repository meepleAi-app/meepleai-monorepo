using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.UserLibrary.Domain.Events;

internal sealed class WishlistItemRemovedEvent : DomainEventBase
{
    public Guid WishlistItemId { get; }
    public Guid UserId { get; }
    public Guid GameId { get; }

    public WishlistItemRemovedEvent(Guid wishlistItemId, Guid userId, Guid gameId)
    {
        WishlistItemId = wishlistItemId;
        UserId = userId;
        GameId = gameId;
    }
}
