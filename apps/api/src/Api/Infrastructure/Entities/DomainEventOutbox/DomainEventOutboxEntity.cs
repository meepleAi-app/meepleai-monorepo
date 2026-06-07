using Api.SharedKernel.Domain.Interfaces;

namespace Api.Infrastructure.Entities.DomainEventOutbox;

/// <summary>
/// Outbox row for post-commit domain-event dispatch (issue #1535).
///
/// <para>Replaces the inline <c>MediatR.Publish</c> previously done inside
/// <c>MeepleAiDbContext.SaveChangesAsync</c>. The row is INSERTed in the SAME
/// <c>SaveChangesAsync</c> as the aggregate mutation (atomic with the business
/// transaction). A <c>DomainEventOutboxProcessor</c> drains rows post-commit and
/// invokes MediatR.Publish — so a rolled-back transaction never causes a
/// side-effect to escape.</para>
///
/// <para>Idempotency: <see cref="Id"/> equals the originating
/// <see cref="IDomainEvent.EventId"/> (PK). The same logical event cannot be
/// enqueued twice — a duplicate INSERT raises a unique-violation by design.</para>
///
/// <para>Lifecycle:
/// <list type="number">
///   <item><see cref="Enqueue"/> — factory, called from <c>MeepleAiDbContext.SaveChangesAsync</c>
///         in the same EF Core change set as the aggregate.</item>
///   <item>Processor poll (every <c>PollIntervalSeconds</c>): pick rows with
///         <c>Status == Pending</c> AND (<see cref="NextAttemptAt"/> IS NULL OR &lt;= NOW).</item>
///   <item>On successful <c>MediatR.Publish</c>: <see cref="MarkSent"/> → <see cref="DomainEventOutboxStatus.Sent"/>.</item>
///   <item>On failure with retry budget remaining: <see cref="MarkRetry"/> →
///         increments <see cref="Attempts"/>, schedules <see cref="NextAttemptAt"/> via exponential backoff.</item>
///   <item>On failure with exhausted budget: <see cref="MarkFailed"/> → terminal,
///         visible on the /admin/event-outbox dashboard.</item>
/// </list></para>
/// </summary>
public sealed class DomainEventOutboxEntity
{
    // EF Core needs a parameterless constructor for materialization. The factory
    // (Enqueue) is the only legitimate construction path from application code.
#pragma warning disable CS8618
    private DomainEventOutboxEntity() { }
#pragma warning restore CS8618

    /// <summary>Primary key — equals the originating <see cref="IDomainEvent.EventId"/>.</summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Stable alias from <c>EventTypeRegistry</c> when the event is registered,
    /// otherwise the CLR type's <c>FullName</c>. The processor uses this to
    /// resolve the target type for JSON deserialization before calling
    /// MediatR.Publish.
    /// </summary>
    public string EventType { get; private set; } = string.Empty;

    /// <summary>JSON-serialized event payload (jsonb in Postgres).</summary>
    public string PayloadJson { get; private set; } = string.Empty;

    /// <summary>
    /// Schema version of <see cref="PayloadJson"/> for forward-compatible migrations.
    /// New schemas with breaking changes increment this.
    /// </summary>
    public int PayloadVersion { get; private set; } = 1;

    public DomainEventOutboxStatus Status { get; private set; }

    public int Attempts { get; private set; }

    /// <summary>Last exception message (truncated to 2048 chars) — null when row has never failed.</summary>
    public string? LastError { get; private set; }

    /// <summary>From <see cref="IDomainEvent.OccurredAt"/>, preserving the aggregate's wall-clock.</summary>
    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>Server timestamp when the row was inserted.</summary>
    public DateTimeOffset EnqueuedAt { get; private set; }

    /// <summary>Server timestamp when <see cref="MarkSent"/> committed; null when Pending or Failed.</summary>
    public DateTimeOffset? DispatchedAt { get; private set; }

    /// <summary>
    /// Earliest time the processor may re-attempt dispatch. Null = ready immediately
    /// (first attempt or after MarkSent). Set by <see cref="MarkRetry"/> via exponential backoff.
    /// </summary>
    public DateTimeOffset? NextAttemptAt { get; private set; }

    /// <summary>
    /// Optional request-scope correlation id (e.g. <c>Activity.Current?.Id</c>) propagated
    /// to logger scope when the processor dispatches the event — preserves the
    /// request → event → handler link across the async boundary.
    /// </summary>
    public string? CorrelationId { get; private set; }

    /// <summary>
    /// Factory used by <c>MeepleAiDbContext.SaveChangesAsync</c> to persist an event
    /// raised by an aggregate. Sets the lifecycle invariants for a brand-new Pending row.
    /// </summary>
    public static DomainEventOutboxEntity Enqueue(
        IDomainEvent ev,
        string eventType,
        string payloadJson,
        int payloadVersion,
        string? correlationId,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(ev);
        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException("EventType is required", nameof(eventType));
        if (string.IsNullOrWhiteSpace(payloadJson))
            throw new ArgumentException("PayloadJson is required", nameof(payloadJson));

        return new DomainEventOutboxEntity
        {
            Id = ev.EventId,
            EventType = eventType,
            PayloadJson = payloadJson,
            PayloadVersion = payloadVersion,
            Status = DomainEventOutboxStatus.Pending,
            Attempts = 0,
            LastError = null,
            OccurredAt = ev.OccurredAt,
            EnqueuedAt = now,
            DispatchedAt = null,
            NextAttemptAt = null,
            CorrelationId = correlationId,
        };
    }

    /// <summary>
    /// Marks the row Sent after a successful <c>MediatR.Publish</c>. Only callable
    /// from <see cref="DomainEventOutboxStatus.Pending"/>; terminal afterwards.
    /// </summary>
    public void MarkSent(DateTimeOffset now)
    {
        if (Status != DomainEventOutboxStatus.Pending)
            throw new InvalidOperationException(
                $"Cannot MarkSent from status {Status}.");

        Status = DomainEventOutboxStatus.Sent;
        DispatchedAt = now;
        LastError = null;
        NextAttemptAt = null;
    }

    /// <summary>
    /// Schedules a retry after a transient failure. Increments <see cref="Attempts"/>,
    /// records the error, and sets <see cref="NextAttemptAt"/> (caller computes the
    /// backoff). The row REMAINS Pending so the processor's next poll observes it.
    /// </summary>
    public void MarkRetry(string error, DateTimeOffset nextAttemptAt, DateTimeOffset now)
    {
        if (Status != DomainEventOutboxStatus.Pending)
            throw new InvalidOperationException(
                $"Cannot MarkRetry from status {Status}.");

        Attempts++;
        LastError = Truncate(error, 2048);
        NextAttemptAt = nextAttemptAt;
    }

    /// <summary>
    /// Terminates the row after exhausted retry budget. Increments <see cref="Attempts"/>,
    /// records the error, transitions to <see cref="DomainEventOutboxStatus.Failed"/>.
    /// No further state transitions are allowed.
    /// </summary>
    public void MarkFailed(string error, DateTimeOffset now)
    {
        if (Status != DomainEventOutboxStatus.Pending)
            throw new InvalidOperationException(
                $"Cannot MarkFailed from status {Status}.");

        Status = DomainEventOutboxStatus.Failed;
        Attempts++;
        LastError = Truncate(error, 2048);
        NextAttemptAt = null;
    }

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s[..max];
}
