namespace Api.BoundedContexts.Administration.Application.Queries.AdminEvents;

/// <summary>
/// Result envelope returned by <see cref="GetAdminEventsQuery"/>.
/// </summary>
/// <param name="Events">
/// Domain event rows ordered by <c>LoggedAt DESC</c>, limited to the requested page size.
/// </param>
internal sealed record GetAdminEventsResult(IReadOnlyList<DomainEventDto> Events);

/// <summary>
/// Admin-facing projection of a <c>domain_event_logs</c> row.
/// All fields are exposed (no user-scoped data masking) — admin gate applied at routing layer.
/// </summary>
/// <param name="Id">Surrogate primary key of the log row.</param>
/// <param name="EventId">
/// Domain event identifier (idempotency-safe; unique per event source).
/// </param>
/// <param name="EventType">
/// Stable type alias (e.g. "agent.created"). Never a CLR type name.
/// </param>
/// <param name="AggregateType">
/// PascalCase aggregate name (e.g. "Agent", "PdfDocument"). Nullable.
/// </param>
/// <param name="AggregateId">Logical FK to the aggregate root. Nullable.</param>
/// <param name="UserId">User that caused the event. Nullable.</param>
/// <param name="PayloadJson">JSON-serialized event payload.</param>
/// <param name="PayloadVersion">Schema version of the payload (1 for all current events).</param>
/// <param name="OccurredAt">Domain clock when the event was raised.</param>
/// <param name="LoggedAt">Server timestamp when the row was persisted.</param>
internal sealed record DomainEventDto(
    Guid Id,
    Guid EventId,
    string EventType,
    string? AggregateType,
    Guid? AggregateId,
    Guid? UserId,
    string PayloadJson,
    int PayloadVersion,
    DateTime OccurredAt,
    DateTime LoggedAt
);
