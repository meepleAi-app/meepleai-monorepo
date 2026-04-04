using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when the 1-hour reminder is sent for a game night.
/// Issue #42: GameNightEvent + GameNightRsvp domain entities.
/// </summary>
internal sealed class GameNightReminder1hEvent : DomainEventBase
{
    public Guid GameNightEventId { get; }
    public string Title { get; }
    public DateTimeOffset ScheduledAt { get; }

    public GameNightReminder1hEvent(Guid gameNightEventId, string title, DateTimeOffset scheduledAt)
    {
        GameNightEventId = gameNightEventId;
        Title = title;
        ScheduledAt = scheduledAt;
    }
}
