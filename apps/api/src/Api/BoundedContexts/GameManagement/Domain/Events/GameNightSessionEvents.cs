using Api.SharedKernel.Domain.Interfaces;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a game is started within a game night.
/// </summary>
internal record GameStartedInNightEvent(
    Guid GameNightId,
    Guid SessionId,
    Guid GameId,
    string GameTitle,
    int PlayOrder) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

/// <summary>
/// Domain event raised when a game is completed within a game night.
/// </summary>
internal record GameCompletedInNightEvent(
    Guid GameNightId,
    Guid SessionId,
    Guid GameId,
    string GameTitle,
    Guid? WinnerId) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

/// <summary>
/// Domain event raised when a game night begins (first session starts).
/// </summary>
internal record NightStartedEvent(
    Guid GameNightId,
    Guid OrganizerId,
    string Title) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

/// <summary>
/// Domain event raised when a game night is finalized (all games done).
/// </summary>
internal record NightFinalizedEvent(
    Guid GameNightId,
    Guid OrganizerId,
    string Title,
    int TotalGamesPlayed) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

/// <summary>
/// Domain event raised when player resources are updated during a game night.
/// </summary>
internal record GameNightResourceUpdateEvent(
    Guid GameNightId,
    Guid SessionId,
    Guid PlayerId,
    string ResourceType,
    string Payload) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
