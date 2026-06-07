using Api.Infrastructure.Entities.DomainEventOutbox;

namespace Api.BoundedContexts.Administration.Application.DTOs;

/// <summary>
/// Aggregate snapshot of the <c>domain_event_outbox</c> queue, returned by
/// <c>GET /api/v1/admin/event-outbox/stats</c>. Powers the future
/// <c>/admin/monitor?tab=events</c> dashboard.
///
/// Issue #1535 T6.
/// </summary>
internal sealed record DomainEventOutboxStatsDto(
    long PendingCount,
    long FailedCount,
    long SentLast24h,
    double OldestPendingAgeSeconds);

/// <summary>
/// Flat projection of a <see cref="DomainEventOutboxEntity"/> row, returned by
/// the <c>/failed</c> and <c>/pending</c> admin endpoints. Excludes the raw
/// JSON payload by default to keep response sizes bounded — operators inspect
/// payloads through the dashboard's drill-down (future work).
///
/// Issue #1535 T6.
/// </summary>
internal sealed record DomainEventOutboxRowDto(
    Guid Id,
    string EventType,
    DomainEventOutboxStatus Status,
    int Attempts,
    string? LastError,
    DateTimeOffset OccurredAt,
    DateTimeOffset EnqueuedAt,
    DateTimeOffset? DispatchedAt,
    DateTimeOffset? NextAttemptAt,
    string? CorrelationId);
