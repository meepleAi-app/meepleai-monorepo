using MediatR;

namespace Api.BoundedContexts.Administration.Domain.Events;

/// <summary>
/// Issue #3974: Domain event raised when a game is added to a user's library.
/// Triggers dashboard and activity timeline cache invalidation.
/// </summary>
public record UserLibraryGameAddedEvent(Guid UserId, Guid GameId) : INotification;

/// <summary>
/// Issue #3974: Domain event raised when a user's game session is completed.
/// Triggers dashboard and activity timeline cache invalidation.
/// </summary>
public record UserGameSessionCompletedEvent(Guid UserId, Guid SessionId) : INotification;

/// <summary>
/// Issue #3974: Domain event raised when a chat conversation is saved.
/// Triggers dashboard and activity timeline cache invalidation.
/// </summary>
public record UserChatSavedEvent(Guid UserId, Guid ChatId) : INotification;

/// <summary>
/// Issue #3974: Domain event raised when a user's wishlist is updated.
/// Triggers dashboard and activity timeline cache invalidation.
/// </summary>
public record UserWishlistUpdatedEvent(Guid UserId, Guid GameId) : INotification;
