using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a live game session state is saved.
/// </summary>
internal sealed class LiveSessionSavedEvent : DomainEventBase
{
    public Guid SessionId { get; }
    public DateTime SavedAt { get; }

    public LiveSessionSavedEvent(Guid sessionId, DateTime savedAt)
    {
        SessionId = sessionId;
        SavedAt = savedAt;
    }
}
