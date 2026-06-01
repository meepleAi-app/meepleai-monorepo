namespace Api.BoundedContexts.Administration.Application.Queries.AdminEvents;

/// <summary>
/// Result envelope returned by <see cref="GetEventTypeStatsQuery"/>.
/// </summary>
/// <param name="Types">
/// Per-type statistics for every alias registered in <c>EventTypeRegistry</c>,
/// ordered alphabetically by <c>EventType</c>. Types with no activity in the
/// 24-hour window appear with <c>Count = 0</c> and <c>LastSeenAt = null</c>.
/// </param>
internal sealed record GetEventTypeStatsResult(IReadOnlyList<EventTypeStat> Types);

/// <summary>
/// Aggregated statistics for a single event type alias within the last 24 hours.
/// </summary>
/// <param name="EventType">
/// Stable type alias as registered in <c>EventTypeRegistry</c>
/// (e.g. "agent.created", "kb.doc.indexed").
/// </param>
/// <param name="Count">
/// Number of events with this <c>EventType</c> logged in the last 24 hours.
/// Zero when no events occurred in the window.
/// </param>
/// <param name="LastSeenAt">
/// <c>MAX(LoggedAt)</c> across all events of this type in the 24-hour window.
/// <c>null</c> when <c>Count = 0</c>.
/// </param>
internal sealed record EventTypeStat(
    string EventType,
    int Count,
    DateTime? LastSeenAt
);
