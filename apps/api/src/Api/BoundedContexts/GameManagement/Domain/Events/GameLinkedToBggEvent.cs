using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a game is linked to BoardGameGeek.
/// </summary>
internal sealed class GameLinkedToBggEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the game.
    /// </summary>
    public Guid GameId { get; }

    /// <summary>
    /// Gets the BoardGameGeek ID.
    /// </summary>
    public int BggId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GameLinkedToBggEvent"/> class.
    /// </summary>
    public GameLinkedToBggEvent(Guid gameId, int bggId)
    {
        GameId = gameId;
        BggId = bggId;
    }
}
