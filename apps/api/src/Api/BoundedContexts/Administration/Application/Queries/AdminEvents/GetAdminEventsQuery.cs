using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries.AdminEvents;

/// <summary>
/// Admin-scoped query that returns domain events from <c>domain_event_logs</c>
/// across all users (cross-user, no mandatory UserId filter).
///
/// Used by the <c>GET /api/v1/admin/events</c> polling/backfill endpoint that
/// powers the <c>LiveEventLog</c> component (F4.1 issue #1718).
///
/// Filters are applied additive (AND):
/// <list type="bullet">
///   <item>Retention — always: <c>LoggedAt &gt;= UtcNow - 90 days</c></item>
///   <item><see cref="Since"/> cursor — if set: <c>LoggedAt &lt; Since</c></item>
///   <item><see cref="EventTypes"/> — if non-empty: <c>EventType IN (...)</c></item>
///   <item><see cref="AggregateTypes"/> — if non-empty: <c>AggregateType IN (...)</c></item>
///   <item><see cref="UserId"/> — if set: <c>UserId = @value</c></item>
///   <item><see cref="AggregateId"/> — if set: <c>AggregateId = @value</c></item>
/// </list>
/// </summary>
/// <param name="Since">
/// Cursor: return only events with <c>LoggedAt &lt; Since</c>.
/// Pass the oldest <c>LoggedAt</c> from the previous page to paginate backward in time.
/// </param>
/// <param name="Limit">
/// Maximum rows to return. Clamped to [1, 1000] by the handler. Default 100.
/// </param>
/// <param name="EventTypes">
/// Optional allow-list of <c>EventType</c> aliases (e.g. "agent.created", "kb.doc.indexed").
/// Null or empty = no filter.
/// </param>
/// <param name="AggregateTypes">
/// Optional allow-list of <c>AggregateType</c> names (e.g. "Agent", "PdfDocument").
/// Null or empty = no filter.
/// </param>
/// <param name="UserId">
/// Optional equality filter on <c>UserId</c>.
/// </param>
/// <param name="AggregateId">
/// Optional equality filter on <c>AggregateId</c>.
/// </param>
internal sealed record GetAdminEventsQuery(
    DateTime? Since = null,
    int Limit = 100,
    IReadOnlyList<string>? EventTypes = null,
    IReadOnlyList<string>? AggregateTypes = null,
    Guid? UserId = null,
    Guid? AggregateId = null
) : IQuery<GetAdminEventsResult>;
