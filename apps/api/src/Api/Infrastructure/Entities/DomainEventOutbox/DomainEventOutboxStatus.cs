namespace Api.Infrastructure.Entities.DomainEventOutbox;

/// <summary>
/// Lifecycle status of a <see cref="DomainEventOutboxEntity"/> row (issue #1535).
///
/// <para>State transitions:</para>
/// <list type="bullet">
///   <item><c>Pending → Sent</c> via <see cref="DomainEventOutboxEntity.MarkSent"/> when
///         <c>MediatR.Publish</c> acked successfully.</item>
///   <item><c>Pending → Pending</c> (attempts++, next_attempt_at scheduled) via
///         <see cref="DomainEventOutboxEntity.MarkRetry"/> when dispatch failed and
///         <c>Attempts &lt; MaxAttempts</c>.</item>
///   <item><c>Pending → Failed</c> via <see cref="DomainEventOutboxEntity.MarkFailed"/>
///         when <c>Attempts &gt;= MaxAttempts</c> — terminal, ops-visible on the
///         /admin/event-outbox dashboard for replay/discard.</item>
/// </list>
///
/// <para>Stored as <c>SMALLINT</c> in Postgres (see EF configuration). The numeric
/// values are part of the schema contract — DO NOT renumber without a migration.</para>
/// </summary>
public enum DomainEventOutboxStatus : byte
{
    /// <summary>Row inserted in the same SaveChanges as the aggregate; awaiting dispatch.</summary>
    Pending = 0,

    /// <summary>MediatR.Publish acked; the event has been dispatched. Row may be TTL-purged after 30 days.</summary>
    Sent = 1,

    /// <summary>Exhausted retry budget (<c>DomainEventOutboxOptions.MaxAttempts</c>); ops intervention required.</summary>
    Failed = 2,
}
