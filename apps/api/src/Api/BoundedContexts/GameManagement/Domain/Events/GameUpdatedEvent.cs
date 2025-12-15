using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a game is updated.
/// </summary>
internal sealed class GameUpdatedEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the updated game.
    /// </summary>
    public Guid GameId { get; }

    /// <summary>
    /// Gets the updated game name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GameUpdatedEvent"/> class.
    /// </summary>
    public GameUpdatedEvent(Guid gameId, string name)
    {
        GameId = gameId;
        Name = name;
    }
}
