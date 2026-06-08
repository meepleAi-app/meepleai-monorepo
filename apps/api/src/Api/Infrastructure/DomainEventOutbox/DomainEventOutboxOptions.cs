namespace Api.Infrastructure.DomainEventOutbox;

/// <summary>
/// Dispatch mode for domain events flushed through <c>MeepleAiDbContext.SaveChangesAsync</c>
/// (issue #1535). Post-T10 cleanup: only two values survive.
///
/// <para><b>InlineOnly was removed at T10</b> — it preserved the original #1535 race
/// (inline dispatch fires before the outer transaction commits) and was only kept as
/// a Phase A rollback path. After 7 days of production stability on OutboxOnly, the
/// rollback path is no longer needed.</para>
/// </summary>
public enum DomainEventDispatchMode
{
    /// <summary>
    /// Dual-write: insert the outbox row AND inline-Publish via MediatR in the same
    /// SaveChangesAsync. Used in Phase A as a safe-by-default rollout window, AND retained
    /// post-cutover as the documented rollback path: flipping back to Hybrid restores
    /// the legacy inline dispatch alongside the outbox while the team investigates a
    /// production issue. Consumers see 2× dispatch in this mode — the idempotency
    /// contract documented in
    /// <c>audits/2026-06-06-issue-1535-consumer-idempotency-audit.md</c> still applies.
    /// </summary>
    Hybrid = 0,

    /// <summary>
    /// Outbox-only: insert the outbox row, never inline-Publish. Steady-state behaviour
    /// post-cutover — closes the rollback race that motivated #1535. Requires all
    /// consumers to be idempotent (verified in the Task 0 audit).
    /// </summary>
    OutboxOnly = 1,
}

/// <summary>
/// Configuration for the post-commit domain-event outbox (issue #1535).
///
/// <para>Bound from configuration section <c>DomainEventOutbox</c>. Defaults match
/// the audit spec — see <c>docs/superpowers/specs/2026-06-06-issue-1535-event-outbox-design.md</c>.</para>
/// </summary>
public sealed class DomainEventOutboxOptions
{
    public const string SectionName = "DomainEventOutbox";

    /// <summary>
    /// Routing mode for events raised inside <c>SaveChangesAsync</c>. Default
    /// <see cref="DomainEventDispatchMode.OutboxOnly"/> — the steady-state behaviour
    /// post-T9 cutover. Set explicitly to <see cref="DomainEventDispatchMode.Hybrid"/>
    /// in appsettings overrides to enable the documented rollback path.
    /// </summary>
    public DomainEventDispatchMode Mode { get; init; } = DomainEventDispatchMode.OutboxOnly;

    /// <summary>Processor poll interval in seconds. Default 5s.</summary>
    public int PollIntervalSeconds { get; init; } = 5;

    /// <summary>Max rows drained per processor tick. Default 100.</summary>
    public int BatchSize { get; init; } = 100;

    /// <summary>Max retry attempts before <see cref="DomainEventDispatchMode.OutboxOnly"/>
    /// rows transition to Failed (terminal). Default 10.</summary>
    public int MaxAttempts { get; init; } = 10;

    /// <summary>Initial backoff (ms) for exponential retry. Default 1000 (1s).</summary>
    public int InitialBackoffMs { get; init; } = 1000;

    /// <summary>Cap on retry backoff (seconds). Default 64s.</summary>
    public double MaxBackoffSeconds { get; init; } = 64.0;

    /// <summary>
    /// Issue #1966 retention TTL — Sent rows with <c>DispatchedAt</c> older than this
    /// number of days are eligible for purge by <c>DomainEventOutboxRetentionService</c>.
    /// Default 30. Failed and Pending rows are NEVER auto-purged regardless of age.
    /// </summary>
    public int SentRetentionDays { get; init; } = 30;

    /// <summary>
    /// Issue #1966 retention poll interval — the
    /// <c>DomainEventOutboxRetentionService</c> wakes once per this many hours and
    /// drains the eligible set in <see cref="RetentionBatchSize"/>-sized chunks until
    /// nothing remains. Default 1 hour.
    /// </summary>
    public int RetentionIntervalHours { get; init; } = 1;

    /// <summary>
    /// Issue #1966 retention chunk size — number of rows deleted per round-trip when
    /// the retention service drains the eligible set. Bounded to avoid long-running
    /// DELETE transactions that would block writes; the service loops chunks until the
    /// eligible set is empty. Default 10000.
    /// </summary>
    public int RetentionBatchSize { get; init; } = 10_000;
}
