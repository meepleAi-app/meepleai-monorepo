using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// VectorDocument aggregate root.
/// Represents a document that has been indexed in the vector database.
/// Controls embeddings and search operations for the document.
///
/// Construction rules (#2284 / follow-up of #2244):
/// - Use <see cref="Create"/> from ingestion pipelines — raises <see cref="VectorDocumentIndexedEvent"/>.
/// - Use <see cref="Rehydrate"/> from the mapper (KnowledgeBaseMappers.ToDomain)
///   and test fixtures — does NOT raise domain events (read-side, no phantom event on every DB read).
/// - The public constructor is preserved for backward compatibility but is deprecated;
///   prefer the factory methods so the call-site intent (write vs read) is explicit.
/// </summary>
internal sealed class VectorDocument : AggregateRoot<Guid>
{
    public Guid GameId { get; private set; }
    public Guid PdfDocumentId { get; private set; }
    public string Language { get; private set; }
    public int TotalChunks { get; private set; }
    public int TotalCharacters { get; private set; }
    public DateTime IndexedAt { get; private set; }
    public DateTime? LastSearchedAt { get; private set; }
    public int SearchCount { get; private set; }

    // Cross-BC reference to SharedGameCatalog (Issue #5185, aligned with infra entity Issue #4921)
    public Guid? SharedGameId { get; private set; }

    // Metadata
    public string? Metadata { get; private set; }

    /// <summary>
    /// Private parameterless constructor for EF Core.
    /// </summary>
#pragma warning disable CS8618
    private VectorDocument() : base()
#pragma warning restore CS8618
    {
        // EF Core only.
    }

    /// <summary>
    /// Private field-init constructor used by both the public ctor (event-raising) and
    /// <see cref="Rehydrate"/> (no event). Performs no validation — callers must validate
    /// (public ctor validates language + totalChunks; Rehydrate clamps to safe defaults).
    /// </summary>
    private VectorDocument(
        Guid id,
        Guid gameId,
        Guid pdfDocumentId,
        string language,
        int totalChunks,
        int totalCharacters,
        DateTime indexedAt,
        Guid? sharedGameId) : base(id)
    {
        GameId = gameId;
        PdfDocumentId = pdfDocumentId;
        Language = language;
        TotalChunks = totalChunks;
        TotalCharacters = totalCharacters;
        IndexedAt = indexedAt;
        SearchCount = 0;
        SharedGameId = sharedGameId;
    }

    /// <summary>
    /// Creates a new vector document and raises <see cref="VectorDocumentIndexedEvent"/>.
    /// Constructor kept public for backward compatibility with existing internal callers
    /// (KnowledgeBase mappers + ingest service). New code SHOULD use <see cref="Create"/>
    /// to make the factory boundary explicit. Mapper read path SHOULD use <see cref="Rehydrate"/>
    /// to avoid enqueueing phantom events on every DB read (#2284 issue 1).
    /// </summary>
    public VectorDocument(
        Guid id,
        Guid gameId,
        Guid pdfDocumentId,
        string language,
        int totalChunks,
        Guid? sharedGameId = null,
        int totalCharacters = 0)
        : this(
            id: id,
            gameId: gameId,
            pdfDocumentId: pdfDocumentId,
            language: ValidateLanguage(language),
            totalChunks: ValidatePositiveChunks(totalChunks),
            totalCharacters: ValidateNonNegativeCharacters(totalCharacters),
            indexedAt: DateTime.UtcNow,
            sharedGameId: sharedGameId)
    {
        AddDomainEvent(new VectorDocumentIndexedEvent(id, gameId, totalChunks, sharedGameId));
    }

    /// <summary>
    /// Factory entry point for a freshly indexed vector document.
    /// Encapsulates Guid generation + parameter ordering so cross-BC callers
    /// (DocumentProcessing) don't have to know id-allocation semantics.
    /// Raises <see cref="VectorDocumentIndexedEvent"/> via the underlying constructor.
    /// </summary>
    public static VectorDocument Create(
        Guid pdfDocumentId,
        Guid gameId,
        int totalChunks,
        string language,
        Guid? sharedGameId = null,
        int totalCharacters = 0)
    {
        return new VectorDocument(
            id: Guid.NewGuid(),
            gameId: gameId,
            pdfDocumentId: pdfDocumentId,
            language: language,
            totalChunks: totalChunks,
            sharedGameId: sharedGameId,
            totalCharacters: totalCharacters);
    }

    /// <summary>
    /// Rehydrates an EXISTING VectorDocument from persistence WITHOUT raising domain events.
    /// Used by <c>KnowledgeBaseMappers.ToDomain</c> (fixes #2284 issue 1: every mapper read
    /// previously called the public ctor which enqueued a fresh VectorDocumentIndexedEvent
    /// into the outbox — phantom events on every DB read) and by test fixtures that need a
    /// pre-built aggregate without polluting the event stream.
    ///
    /// Applies lenient defaults for malformed data (empty language → "en", zero chunks → 1,
    /// negative totalCharacters → 0) — the public ctor + Create enforce strict validation
    /// for the write path; Rehydrate trusts the DB row exists for a reason.
    /// </summary>
    internal static VectorDocument Rehydrate(
        Guid id,
        Guid gameId,
        Guid pdfDocumentId,
        string language,
        int totalChunks,
        DateTime indexedAt,
        Guid? sharedGameId,
        string? metadata = null,
        int totalCharacters = 0)
    {
        var doc = new VectorDocument(
            id: id,
            gameId: gameId,
            pdfDocumentId: pdfDocumentId,
            language: string.IsNullOrWhiteSpace(language) ? "en" : language.ToLowerInvariant(),
            totalChunks: totalChunks <= 0 ? 1 : totalChunks,
            totalCharacters: totalCharacters < 0 ? 0 : totalCharacters,
            indexedAt: indexedAt,
            sharedGameId: sharedGameId);
        doc.Metadata = metadata;
        return doc;
    }

    /// <summary>
    /// Records that this document was searched.
    /// </summary>
    public void RecordSearch(string query)
    {
        LastSearchedAt = DateTime.UtcNow;
        SearchCount++;

        AddDomainEvent(new VectorDocumentSearchedEvent(Id, query));
    }

    /// <summary>
    /// Updates metadata (e.g., quality metrics, usage stats).
    /// </summary>
    public void UpdateMetadata(string metadata)
    {
        Metadata = metadata;
        AddDomainEvent(new VectorDocumentMetadataUpdatedEvent(Id, metadata));
    }

    private static string ValidateLanguage(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
            throw new ArgumentException("Language cannot be empty", nameof(language));
        return language.ToLowerInvariant();
    }

    private static int ValidatePositiveChunks(int totalChunks)
    {
        if (totalChunks <= 0)
            throw new ArgumentException("Total chunks must be positive", nameof(totalChunks));
        return totalChunks;
    }

    private static int ValidateNonNegativeCharacters(int totalCharacters)
    {
        if (totalCharacters < 0)
            throw new ArgumentException("Total characters must be non-negative", nameof(totalCharacters));
        return totalCharacters;
    }
}
