using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Events;

/// <summary>
/// Raised by <see cref="Api.BoundedContexts.DocumentProcessing.Domain.Entities.PdfDocument"/>
/// when <c>TransitionTo(Ready)</c> succeeds — the PDF is now fully indexed and searchable via RAG.
///
/// Registered in <see cref="Api.Infrastructure.DomainEventLog.EventTypeRegistry"/>
/// with alias <c>"kb.doc.indexed"</c>. BE-3 #1590.
///
/// <para><b>Why NOT <c>PdfStateChangedEvent</c>?</b>
/// <c>PdfStateChangedEvent</c> fires on EVERY state transition (internal: cache invalidation,
/// metrics, UI real-time updates). Registering it in EventTypeRegistry would produce one log
/// row per pipeline step = log explosion. <c>KbDocIndexedEvent</c> fires ONLY on the Ready
/// terminal transition, representing the single user-meaningful "doc indexed" milestone for
/// the activity rail. Decision B3 from #1590 spec panel.</para>
///
/// <para><b>gameName resolution strategy (design decision opt-a):</b>
/// <c>PdfDocument.TransitionTo</c> is a pure domain method with no repository access.
/// The event carries <c>GameName = null</c>. The Task 8 activity endpoint resolves the
/// game name at query time via <c>SharedGameRepository.GetNamesByIds</c> using the
/// persisted <c>GameId</c> value. <c>FileName</c> is the PRIMARY rail title because it
/// is available on the aggregate and uniquely identifies the indexed document to the user.</para>
///
/// <para><b>Mapper conventions (DomainEventLogMapper):</b>
/// <list type="bullet">
///   <item><c>AggregateId</c> → <c>domain_event_logs.aggregate_id</c> (reflection lookup)</item>
///   <item><c>UserId</c>      → <c>domain_event_logs.user_id</c> (reflection lookup)</item>
///   <item>Class name minus "Event" suffix → <c>domain_event_logs.aggregate_type = "KbDocIndexed"</c></item>
///   <item>Payload serialized with <c>JsonNamingPolicy.CamelCase</c> → keys: fileName, gameId, gameName, pageCount</item>
/// </list></para>
/// </summary>
internal sealed class KbDocIndexedEvent : DomainEventBase
{
    /// <summary>
    /// The <c>PdfDocument</c> id. Named <c>AggregateId</c> so
    /// <see cref="Api.Infrastructure.DomainEventLog.DomainEventLogMapper"/> reflection
    /// populates <c>domain_event_logs.aggregate_id</c> without a custom mapper override.
    /// </summary>
    public Guid AggregateId { get; }

    /// <summary>
    /// The user who uploaded the document. Reflected to <c>domain_event_logs.user_id</c>.
    /// Sourced from <c>PdfDocument.UploadedByUserId</c>.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// The PDF file name (e.g. "catan-rulebook.pdf"). Primary rail title — available
    /// directly on the aggregate, no repository access needed.
    /// </summary>
    public string FileName { get; }

    /// <summary>
    /// The shared game this document belongs to (<c>PdfDocument.SharedGameId</c>).
    /// Null for private-game documents (<c>PdfDocument.PrivateGameId</c> is set instead).
    /// </summary>
    public Guid? GameId { get; }

    /// <summary>
    /// Always <c>null</c> at emit time. Resolved at query time by the Task 8 activity
    /// endpoint via <c>SharedGameRepository.GetNamesByIds</c>. Included in the payload
    /// schema so forward-compatible consumers can expect the field without a v2 migration.
    /// </summary>
    public string? GameName { get; }

    /// <summary>
    /// Total pages in the PDF, if the extraction pipeline populated it.
    /// May be null if the processing worker did not set <c>PdfDocument.PageCount</c>
    /// before emitting the Ready transition.
    /// </summary>
    public int? PageCount { get; }

    /// <param name="aggregateId">The <c>PdfDocument</c> id.</param>
    /// <param name="userId">The uploading user's id (<c>PdfDocument.UploadedByUserId</c>).</param>
    /// <param name="fileName">The file name string (<c>PdfDocument.FileName.Value</c>).</param>
    /// <param name="gameId">Shared game id, if any (<c>PdfDocument.SharedGameId</c>).</param>
    /// <param name="gameName">Always null at emit — resolved query-time.</param>
    /// <param name="pageCount">Page count if available (<c>PdfDocument.PageCount</c>).</param>
    public KbDocIndexedEvent(
        Guid aggregateId,
        Guid userId,
        string fileName,
        Guid? gameId,
        string? gameName,
        int? pageCount)
    {
        AggregateId = aggregateId;
        UserId = userId;
        FileName = fileName;
        GameId = gameId;
        GameName = gameName;
        PageCount = pageCount;
    }
}
