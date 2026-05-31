using Api.SharedKernel.Domain.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Events;

/// <summary>
/// Raised by the <c>UpdateKbDocMetadataCommandHandler</c> when at least one editable
/// metadata field on the <c>PdfDocument</c> aggregate actually changed. Issue #1687.
///
/// Registered in <see cref="Api.Infrastructure.DomainEventLog.EventTypeRegistry"/>
/// with alias <c>"pdf.metadata.changed"</c>.
///
/// <para><b>Why <see cref="IDomainEvent"/> not <c>DomainEventBase</c>?</b>
/// P116 from session memory: <c>record:IDomainEvent</c> preserves the record's
/// immutability and value-based equality without inheriting the mutable
/// <c>DomainEventBase</c> base class. The mapper convention (<c>Name.Replace("Event", "")</c>
/// → AggregateType) still applies because it is reflection-based.</para>
///
/// <para><b>Mapper conventions (DomainEventLogMapper):</b>
/// <list type="bullet">
///   <item><c>AggregateId</c> → <c>domain_event_logs.aggregate_id</c> (reflection lookup)</item>
///   <item><c>UserId</c> → <c>domain_event_logs.user_id</c> (reflection lookup)</item>
///   <item>Class name minus "Event" suffix → <c>domain_event_logs.aggregate_type = "PdfMetadataChanged"</c></item>
///   <item>Payload serialized with <c>JsonNamingPolicy.CamelCase</c></item>
/// </list></para>
///
/// <para><b>D-12 deferred re-indexing:</b> <see cref="RequiresReindex"/> is a forward-compat
/// hint computed from the change set (true when documentType or language changed). No consumer
/// subscribes in v1; the property exists so a future opt-in re-indexing worker can pick up
/// the signal without an event schema migration.</para>
/// </summary>
/// <param name="AggregateId">PDF document id. Reflected to <c>domain_event_logs.aggregate_id</c>.</param>
/// <param name="UserId">The user who performed the edit. Reflected to <c>domain_event_logs.user_id</c>.</param>
/// <param name="EditorRole">Editor's role at edit time — "Owner", "Admin", or "SuperAdmin".</param>
/// <param name="Changes">List of changed fields with old/new value pairs. Never empty (handler skips emit when no real change).</param>
/// <param name="GameId">The PDF's <c>SharedGameId</c> (null for orphaned / private-game docs). Used by the cache-invalidation handler.</param>
public sealed record PdfMetadataChangedEvent(
    Guid AggregateId,
    Guid UserId,
    string EditorRole,
    IReadOnlyList<MetadataChange> Changes,
    Guid? GameId) : IDomainEvent
{
    /// <summary>IDomainEvent contract — unique event instance id.</summary>
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <summary>IDomainEvent contract — emission timestamp (UTC).</summary>
    public DateTime OccurredAt { get; init; } = TimeProvider.System.GetUtcNow().UtcDateTime;

    /// <summary>
    /// Computed flag indicating whether the change set requires the document to be re-indexed
    /// in the RAG pipeline. True iff <see cref="Changes"/> contains a documentType or language edit.
    /// D-12 forward-compat hint; no consumer subscribes in v1.
    /// </summary>
    public bool RequiresReindex => Changes.Any(c =>
        string.Equals(c.Field, "documentType", StringComparison.Ordinal)
        || string.Equals(c.Field, "language", StringComparison.Ordinal));
}
