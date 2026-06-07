namespace Api.Infrastructure.DomainEventOutbox;

/// <summary>
/// Dispatch mode for domain events flushed through <c>MeepleAiDbContext.SaveChangesAsync</c>
/// (issue #1535). Defaults to <see cref="Hybrid"/> in production for the rollout window;
/// flipped to <see cref="OutboxOnly"/> at Phase B cutover.
/// </summary>
public enum DomainEventDispatchMode
{
    /// <summary>
    /// Dual-write: insert the outbox row AND inline-Publish via MediatR in the same
    /// SaveChangesAsync. Consumers see 2× dispatch — verifies the idempotency contract
    /// in staging before flipping to <see cref="OutboxOnly"/>. Default for the Phase A
    /// rollout window.
    /// </summary>
    Hybrid = 0,

    /// <summary>
    /// Outbox-only: insert the outbox row, never inline-Publish. Phase B target state —
    /// fixes the rollback race that motivated #1535. Requires all consumers to be
    /// idempotent (verified in the Task 0 audit).
    /// </summary>
    OutboxOnly = 1,

    /// <summary>
    /// Legacy behaviour: inline-Publish only, no outbox row. Used as the rollback path
    /// during Phase A if a regression is observed. NOT a safe long-term state —
    /// preserves the original #1535 bug.
    /// </summary>
    InlineOnly = 2,
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
    /// <see cref="DomainEventDispatchMode.Hybrid"/> — safe to ship, no behaviour
    /// change vs pre-#1535.
    /// </summary>
    public DomainEventDispatchMode Mode { get; init; } = DomainEventDispatchMode.Hybrid;

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
}
