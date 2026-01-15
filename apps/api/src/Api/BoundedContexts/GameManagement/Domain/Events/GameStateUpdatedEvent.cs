using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Event raised when game session state is updated.
/// </summary>
internal sealed class GameStateUpdatedEvent : DomainEventBase
{
    public Guid StateId { get; }
    public Guid GameSessionId { get; }
    public int NewVersion { get; }
    public DateTime UpdatedAt { get; }

    public GameStateUpdatedEvent(Guid stateId, Guid gameSessionId, int newVersion, DateTime updatedAt)
    {
        StateId = stateId;
        GameSessionId = gameSessionId;
        NewVersion = newVersion;
        UpdatedAt = updatedAt;
    }
}
