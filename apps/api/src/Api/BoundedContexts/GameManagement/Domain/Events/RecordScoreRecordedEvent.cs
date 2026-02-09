using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a score is recorded for a player.
/// </summary>
internal sealed class RecordScoreRecordedEvent : DomainEventBase
{
    public Guid RecordId { get; }
    public Guid PlayerId { get; }
    public string Dimension { get; }
    public int Value { get; }

    public RecordScoreRecordedEvent(Guid recordId, Guid playerId, string dimension, int value)
    {
        RecordId = recordId;
        PlayerId = playerId;
        Dimension = dimension;
        Value = value;
    }
}
