using MediatR;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a game is started within a game night.
/// </summary>
internal record GameStartedInNightEvent(
    Guid GameNightId,
    Guid SessionId,
    Guid GameId,
    string GameTitle,
    int PlayOrder) : INotification;

/// <summary>
/// Domain event raised when a game is completed within a game night.
/// </summary>
internal record GameCompletedInNightEvent(
    Guid GameNightId,
    Guid SessionId,
    Guid GameId,
    string GameTitle,
    Guid? WinnerId) : INotification;

/// <summary>
/// Domain event raised when a game night begins (first session starts).
/// </summary>
internal record NightStartedEvent(
    Guid GameNightId,
    Guid OrganizerId,
    string Title) : INotification;

/// <summary>
/// Domain event raised when a game night is finalized (all games done).
/// </summary>
internal record NightFinalizedEvent(
    Guid GameNightId,
    Guid OrganizerId,
    string Title,
    int TotalGamesPlayed) : INotification;

/// <summary>
/// Domain event raised when player resources are updated during a game night.
/// </summary>
internal record GameNightResourceUpdateEvent(
    Guid GameNightId,
    Guid SessionId,
    Guid PlayerId,
    string ResourceType,
    string Payload) : INotification;
