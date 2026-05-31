using Api.BoundedContexts.Administration.Application.Queries.AdminEvents;

namespace Api.Infrastructure.EventBroadcasting;

/// <summary>
/// Per-subscriber filter for <see cref="IEventBroadcaster"/>.
///
/// Shape mirrors <c>GetAdminEventsQuery</c> (Task 1.1) for consistency —
/// both filter on the same dimensions.
///
/// All parameters are optional (null = "do not restrict on this dimension").
/// An event passes if ALL non-null filters are satisfied simultaneously.
/// </summary>
/// <param name="EventTypes">
/// If set, the event's <see cref="DomainEventDto.EventType"/> must be IN this list.
/// Case-sensitive exact match (event types are stable registry aliases, e.g. "agent.created").
/// </param>
/// <param name="AggregateTypes">
/// If set, the event's <see cref="DomainEventDto.AggregateType"/> must be IN this list.
/// Case-sensitive. Null aggregate type on the event never matches a non-null filter.
/// </param>
/// <param name="UserId">
/// If set, the event's <see cref="DomainEventDto.UserId"/> must equal this value.
/// Null UserId on the event never matches a non-null filter.
/// </param>
/// <param name="AggregateId">
/// If set, the event's <see cref="DomainEventDto.AggregateId"/> must equal this value.
/// Null AggregateId on the event never matches a non-null filter.
/// </param>
internal sealed record EventBroadcastFilter(
    IReadOnlyList<string>? EventTypes = null,
    IReadOnlyList<string>? AggregateTypes = null,
    Guid? UserId = null,
    Guid? AggregateId = null)
{
    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="evt"/> passes all
    /// non-null filter dimensions; <see langword="false"/> if any dimension
    /// is set and the event does not satisfy it.
    /// </summary>
    internal bool Matches(DomainEventDto evt)
    {
        ArgumentNullException.ThrowIfNull(evt);

        if (EventTypes is { Count: > 0 } && !EventTypes.Contains(evt.EventType, StringComparer.Ordinal))
            return false;

        if (AggregateTypes is { Count: > 0 })
        {
            // Null aggregate type on event never satisfies an explicit type filter
            if (evt.AggregateType is null || !AggregateTypes.Contains(evt.AggregateType, StringComparer.Ordinal))
                return false;
        }

        if (UserId.HasValue && evt.UserId != UserId)
            return false;

        if (AggregateId.HasValue && evt.AggregateId != AggregateId)
            return false;

        return true;
    }
}
