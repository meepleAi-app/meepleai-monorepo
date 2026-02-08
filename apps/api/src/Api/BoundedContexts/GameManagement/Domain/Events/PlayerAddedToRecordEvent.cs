using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a player is added to a play record.
/// </summary>
internal sealed class PlayerAddedToRecordEvent : DomainEventBase
{
    public Guid RecordId { get; }
    public Guid PlayerId { get; }
    public Guid? UserId { get; }
    public string DisplayName { get; }

    public PlayerAddedToRecordEvent(Guid recordId, Guid playerId, Guid? userId, string displayName)
    {
        RecordId = recordId;
        PlayerId = playerId;
        UserId = userId;
        DisplayName = displayName;
    }
}
