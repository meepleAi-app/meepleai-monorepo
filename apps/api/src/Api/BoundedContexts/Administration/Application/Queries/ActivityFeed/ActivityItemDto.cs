namespace Api.BoundedContexts.Administration.Application.Queries.ActivityFeed;

/// <summary>
/// Represents a single activity item in the cross-entity feed.
/// Returned by <see cref="GetActivityFeedQuery"/> (BE-3 #1590 Task 8).
/// </summary>
/// <param name="Id">Row id of the <c>domain_event_logs</c> entry.</param>
/// <param name="EventId">Idempotency-safe event id (deduplicated per event source).</param>
/// <param name="EventType">Stable event type alias (e.g. "agent.created", "session.finalized").</param>
/// <param name="UserId">User that caused the event.</param>
/// <param name="EntityType">
/// Clean entity type name derived from EventType:
/// "Agent" | "ChatSession" | "PdfDocument" | "Session" | "UserLibraryEntry".
/// </param>
/// <param name="EntityId">The aggregate / entity id extracted from the event payload or <c>AggregateId</c>.</param>
/// <param name="Title">Human-readable title extracted from the event payload (e.g. agent name, game name).</param>
/// <param name="Timestamp">When the event occurred (domain clock).</param>
/// <param name="LoggedAt">When the row was persisted to <c>domain_event_logs</c>.</param>
/// <param name="PayloadVersion">Schema version of the event payload (currently always 1).</param>
internal sealed record ActivityItemDto(
    Guid Id,
    Guid EventId,
    string EventType,
    Guid UserId,
    string EntityType,
    Guid EntityId,
    string? Title,
    DateTime Timestamp,
    DateTime LoggedAt,
    int PayloadVersion
);
