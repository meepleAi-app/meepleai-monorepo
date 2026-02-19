using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a live game session is completed.
/// </summary>
internal sealed class LiveSessionCompletedEvent : DomainEventBase
{
    public Guid SessionId { get; }
    public DateTime CompletedAt { get; }
    public int TotalTurns { get; }

    public LiveSessionCompletedEvent(Guid sessionId, DateTime completedAt, int totalTurns)
    {
        SessionId = sessionId;
        CompletedAt = completedAt;
        TotalTurns = totalTurns;
    }
}
