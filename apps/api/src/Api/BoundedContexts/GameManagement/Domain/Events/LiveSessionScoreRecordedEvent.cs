using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a score is recorded in a live game session.
/// </summary>
internal sealed class LiveSessionScoreRecordedEvent : DomainEventBase
{
    public Guid SessionId { get; }
    public Guid PlayerId { get; }
    public int Round { get; }
    public string Dimension { get; }
    public int Value { get; }

    public LiveSessionScoreRecordedEvent(
        Guid sessionId,
        Guid playerId,
        int round,
        string dimension,
        int value)
    {
        SessionId = sessionId;
        PlayerId = playerId;
        Round = round;
        Dimension = dimension;
        Value = value;
    }
}
