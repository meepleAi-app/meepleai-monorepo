namespace Api.Infrastructure.Entities.DomainEventLog;

/// <summary>
/// Append-only persistence record of every <c>IDomainEvent</c> that flows
/// through <see cref="Api.Infrastructure.MeepleAiDbContext.SaveChangesAsync"/>.
///
/// Issue #661: durable log that closes the gap between in-memory MediatR
/// dispatch and downstream consumers (e.g. activity feed, audit views).
/// Written atomically alongside aggregate state in a SINGLE
/// <c>SaveChangesAsync</c> call — see spec §3.2 hardened.
/// </summary>
public class DomainEventLogEntity
{
    /// <summary>Surrogate primary key (auto-generated).</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The event's own identifier from <see cref="Api.SharedKernel.Domain.Interfaces.IDomainEvent.EventId"/>.
    /// UNIQUE — guards against double-write on retry (AC-11).
    /// </summary>
    public Guid EventId { get; set; }

    /// <summary>
    /// Stable alias resolved by <c>EventTypeRegistry.Resolve</c>. NOT a CLR
    /// type name — class renames must not orphan log rows (P0-2 in spec).
    /// </summary>
    public string EventType { get; set; } = default!;

    /// <summary>The user associated with the event, when applicable.</summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// PascalCase aggregate name. Logical FK pointer to a specific aggregate table:
    ///   - "Session"     → sessions_tracking.id
    ///   - "Agent"       → agent_definitions.id
    ///   - "ChatSession" → chat_sessions.id
    ///   - "PdfDocument" → pdf_documents.id
    ///   - "UserLibraryEntry" → user_library_entries.id (existing library.* events)
    /// Not enforced at DB level — append-only audit log policy (AC9, #1590).
    /// </summary>
    public string? AggregateType { get; set; }

    /// <summary>
    /// Logical FK to the aggregate root. See <see cref="AggregateType"/> for the mapping
    /// from PascalCase aggregate name to physical table.
    /// </summary>
    public Guid? AggregateId { get; set; }

    /// <summary>JSON-serialized event payload.</summary>
    public string PayloadJson { get; set; } = default!;

    /// <summary>
    /// Schema version of the event payload, used for forward-compatible migrations.
    /// v1 events: PayloadVersion = 1. New payload schemas (with breaking changes) increment this.
    /// </summary>
    public int PayloadVersion { get; set; } = 1;

    /// <summary>
    /// From <see cref="Api.SharedKernel.Domain.Interfaces.IDomainEvent.OccurredAt"/>.
    /// The aggregate's wall-clock when the event was raised.
    /// </summary>
    public DateTime OccurredAt { get; set; }

    /// <summary>
    /// Server timestamp at persist time. The activity feed and any future
    /// cleanup job order/filter by this column (it's monotonic; OccurredAt
    /// can drift on aggregate timestamps).
    /// </summary>
    public DateTime LoggedAt { get; set; } = DateTime.UtcNow;
}
