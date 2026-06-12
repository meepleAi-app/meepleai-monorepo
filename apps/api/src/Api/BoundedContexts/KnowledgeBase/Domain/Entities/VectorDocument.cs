using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// VectorDocument aggregate root.
/// Represents a document that has been indexed in the vector database.
/// Controls embeddings and search operations for the document.
///
/// Construction rules (#2244 / epic #2242 Sub #2):
/// - Use <see cref="Create"/> from ingestion pipelines — raises <see cref="VectorDocumentIndexedEvent"/>.
/// - Use <see cref="Rehydrate"/> from the mapper (KnowledgeBaseMappers.ToDomain)
///   and test fixtures — does NOT raise domain events (read-side, no event re-fire on every DB read).
/// - Public constructor is intentionally absent to enforce the factory pattern.
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
    /// Private full constructor used by both <see cref="Create"/> and <see cref="Rehydrate"/>.
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
    /// Factory: builds a NEW VectorDocument and raises <see cref="VectorDocumentIndexedEvent"/>.
    /// Use this from ingestion pipelines (Sub #2 of epic #2242).
    /// </summary>
    public static VectorDocument Create(
        Guid id,
        Guid gameId,
        Guid pdfDocumentId,
        string language,
        int totalChunks,
        Guid? sharedGameId = null,
        int totalCharacters = 0)
    {
        if (string.IsNullOrWhiteSpace(language))
            throw new ArgumentException("Language cannot be empty", nameof(language));
        if (totalChunks <= 0)
            throw new ArgumentException("Total chunks must be positive", nameof(totalChunks));
        if (totalCharacters < 0)
            throw new ArgumentException("Total characters must be non-negative", nameof(totalCharacters));

        var doc = new VectorDocument(
            id: id,
            gameId: gameId,
            pdfDocumentId: pdfDocumentId,
            language: language.ToLowerInvariant(),
            totalChunks: totalChunks,
            totalCharacters: totalCharacters,
            indexedAt: DateTime.UtcNow,
            sharedGameId: sharedGameId);

        doc.AddDomainEvent(new VectorDocumentIndexedEvent(id, gameId, totalChunks, sharedGameId));
        return doc;
    }

    /// <summary>
    /// Rehydrates an EXISTING VectorDocument from persistence WITHOUT raising domain events.
    /// Used by KnowledgeBaseMappers.ToDomain and test fixtures.
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
        var domain = new VectorDocument(
            id: id,
            gameId: gameId,
            pdfDocumentId: pdfDocumentId,
            language: string.IsNullOrWhiteSpace(language) ? "en" : language.ToLowerInvariant(),
            totalChunks: totalChunks <= 0 ? 1 : totalChunks,
            totalCharacters: totalCharacters < 0 ? 0 : totalCharacters,
            indexedAt: indexedAt,
            sharedGameId: sharedGameId);
        domain.Metadata = metadata;
        return domain;
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


}
