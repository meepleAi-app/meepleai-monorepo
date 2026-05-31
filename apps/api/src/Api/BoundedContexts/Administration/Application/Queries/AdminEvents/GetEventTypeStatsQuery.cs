using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries.AdminEvents;

/// <summary>
/// Admin-scoped query that returns per-type event statistics from
/// <c>domain_event_logs</c> for the last 24 hours.
///
/// Used by the <c>GET /api/v1/admin/events/types</c> metadata endpoint that
/// powers the <c>LiveEventLog</c> filter-chip counts (F4.1 issue #1718).
///
/// The 24-hour window is fixed — no parameters. Types with zero activity in
/// the window are still returned (sourced from <c>EventTypeRegistry</c>) so
/// that the UI can show all known event type chips regardless of recent activity.
///
/// F4.1 issue #1718 — Task 1.2.
/// </summary>
internal sealed record GetEventTypeStatsQuery() : IQuery<GetEventTypeStatsResult>;
