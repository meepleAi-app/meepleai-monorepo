using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a diary entry is appended to a live game session.
/// </summary>
internal sealed class LiveSessionDiaryEntryAddedEvent : DomainEventBase
{
    public Guid SessionId { get; }
    public Guid EntryId { get; }
    public Guid AuthorId { get; }
    public string Text { get; }
    public DateTimeOffset CreatedAt { get; }

    public LiveSessionDiaryEntryAddedEvent(
        Guid sessionId,
        Guid entryId,
        Guid authorId,
        string text,
        DateTimeOffset createdAt)
    {
        SessionId = sessionId;
        EntryId = entryId;
        AuthorId = authorId;
        Text = text;
        CreatedAt = createdAt;
    }
}
