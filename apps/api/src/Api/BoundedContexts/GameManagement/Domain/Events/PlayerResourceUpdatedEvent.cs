using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a player's resource is updated.
/// Issue #2405: Ledger Mode - Full State Tracking.
/// </summary>
internal sealed class PlayerResourceUpdatedEvent : DomainEventBase
{
    public Guid StateId { get; }
    public Guid SessionId { get; }
    public string PlayerName { get; }
    public string ResourceName { get; }
    public int OldValue { get; }
    public int NewValue { get; }

    public PlayerResourceUpdatedEvent(
        Guid stateId,
        Guid sessionId,
        string playerName,
        string resourceName,
        int oldValue,
        int newValue)
    {
        StateId = stateId;
        SessionId = sessionId;
        PlayerName = playerName;
        ResourceName = resourceName;
        OldValue = oldValue;
        NewValue = newValue;
    }
}
