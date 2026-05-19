using System.Text.Json;
using Api.Infrastructure.Entities.DomainEventLog;
using Api.SharedKernel.Domain.Interfaces;

namespace Api.Infrastructure.DomainEventLog;

/// <summary>
/// Builds <see cref="DomainEventLogEntity"/> rows from in-flight
/// <see cref="IDomainEvent"/> instances. Issue #661.
///
/// Returns <c>null</c> when the event is not registered for log persistence —
/// caller (the DbContext) should skip the log step.
/// </summary>
public static class DomainEventLogMapper
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Maps a domain event to a log entity. Returns null when the event has no
    /// registered alias in <see cref="EventTypeRegistry"/>.
    /// </summary>
    public static DomainEventLogEntity? Map(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var alias = EventTypeRegistry.TryResolve(domainEvent);
        if (alias is null)
        {
            return null;
        }

        // Reflect a small set of well-known properties off the event so the
        // log row carries enough denormalized data to be self-describing.
        // PayloadJson is the full payload; UserId/AggregateId/AggregateType
        // are convenience columns for the activity-feed query path.
        var (userId, aggregateId, aggregateType) = ExtractWellKnown(domainEvent);

        return new DomainEventLogEntity
        {
            Id = Guid.NewGuid(),
            EventId = domainEvent.EventId,
            EventType = alias,
            UserId = userId,
            AggregateId = aggregateId,
            AggregateType = aggregateType,
            PayloadJson = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), PayloadJsonOptions),
            OccurredAt = domainEvent.OccurredAt,
            LoggedAt = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Best-effort extraction of common event-payload fields. The activity
    /// feed query reads these convenience columns instead of round-tripping
    /// through JSON for every row.
    /// </summary>
    private static (Guid? userId, Guid? aggregateId, string? aggregateType) ExtractWellKnown(IDomainEvent ev)
    {
        var type = ev.GetType();
        Guid? userId = TryGetGuid(ev, type, "UserId");
        Guid? aggregateId = TryGetGuid(ev, type, "AggregateId")
            ?? TryGetGuid(ev, type, "Id");

        // Convention: event class name without the "Event" suffix is the
        // aggregate type (e.g. LibraryEntryRemovedEvent → LibraryEntry).
        var clsName = type.Name;
        var aggregateType = clsName.EndsWith("Event", StringComparison.Ordinal)
            ? clsName[..^"Event".Length]
            : clsName;

        return (userId, aggregateId, aggregateType);
    }

    private static Guid? TryGetGuid(object instance, Type type, string propertyName)
    {
        var prop = type.GetProperty(propertyName);
        if (prop?.PropertyType != typeof(Guid) && prop?.PropertyType != typeof(Guid?))
            return null;
        var value = prop.GetValue(instance);
        return value is Guid g && g != Guid.Empty ? g : null;
    }
}
