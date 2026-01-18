using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a new game session state is created.
/// Issue #2403: Game Session State Tracking.
/// </summary>
internal sealed class GameSessionStateCreatedEvent : DomainEventBase
{
    public Guid StateId { get; }
    public Guid SessionId { get; }
    public int PlayerCount { get; }

    public GameSessionStateCreatedEvent(Guid stateId, Guid sessionId, int playerCount)
    {
        StateId = stateId;
        SessionId = sessionId;
        PlayerCount = playerCount;
    }
}
