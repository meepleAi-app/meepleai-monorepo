using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a new play record is created.
/// </summary>
internal sealed class PlayRecordCreatedEvent : DomainEventBase
{
    public Guid RecordId { get; }
    public Guid UserId { get; }
    public Guid? GameId { get; }
    public string GameName { get; }

    public PlayRecordCreatedEvent(Guid recordId, Guid userId, Guid? gameId, string gameName)
    {
        RecordId = recordId;
        UserId = userId;
        GameId = gameId;
        GameName = gameName;
    }
}
