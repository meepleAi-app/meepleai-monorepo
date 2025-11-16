using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a new game is created.
/// </summary>
public sealed class GameCreatedEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the created game.
    /// </summary>
    public Guid GameId { get; }

    /// <summary>
    /// Gets the game name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the BoardGameGeek ID if available.
    /// </summary>
    public int? BggId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GameCreatedEvent"/> class.
    /// </summary>
    public GameCreatedEvent(Guid gameId, string name, int? bggId = null)
    {
        GameId = gameId;
        Name = name;
        BggId = bggId;
    }
}
