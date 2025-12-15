using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a player is added to a game session.
/// </summary>
internal sealed class PlayerAddedToSessionEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the game session.
    /// </summary>
    public Guid SessionId { get; }

    /// <summary>
    /// Gets the player name.
    /// </summary>
    public string PlayerName { get; }

    /// <summary>
    /// Gets the player number in the session.
    /// </summary>
    public int PlayerNumber { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayerAddedToSessionEvent"/> class.
    /// </summary>
    public PlayerAddedToSessionEvent(Guid sessionId, string playerName, int playerNumber)
    {
        SessionId = sessionId;
        PlayerName = playerName;
        PlayerNumber = playerNumber;
    }
}
