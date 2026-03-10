using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when the 24-hour reminder is sent for a game night.
/// Issue #42: GameNightEvent + GameNightRsvp domain entities.
/// </summary>
internal sealed class GameNightReminder24hEvent : DomainEventBase
{
    public Guid GameNightEventId { get; }
    public string Title { get; }
    public DateTimeOffset ScheduledAt { get; }
    public string? Location { get; }

    public GameNightReminder24hEvent(Guid gameNightEventId, string title, DateTimeOffset scheduledAt, string? location)
    {
        GameNightEventId = gameNightEventId;
        Title = title;
        ScheduledAt = scheduledAt;
        Location = location;
    }
}
