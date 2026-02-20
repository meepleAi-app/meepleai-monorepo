using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// VectorDocument aggregate root.
/// Represents a document that has been indexed in the vector database.
/// Controls embeddings and search operations for the document.
/// Either GameId (user library) or SharedGameId (admin-owned content) must be set.
/// </summary>
internal sealed class VectorDocument : AggregateRoot<Guid>
{
    public Guid? GameId { get; private set; }
    /// <summary>Issue #4921: For admin-owned shared game content.</summary>
    public Guid? SharedGameId { get; private set; }
    public Guid PdfDocumentId { get; private set; }
    public string Language { get; private set; }
    public int TotalChunks { get; private set; }
    public DateTime IndexedAt { get; private set; }
    public DateTime? LastSearchedAt { get; private set; }
    public int SearchCount { get; private set; }

    // Metadata
    public string? Metadata { get; private set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable CS8618
    private VectorDocument() : base()
#pragma warning restore CS8618
    {
    }

    /// <summary>
    /// Creates a new vector document for a user library game.
    /// </summary>
    public VectorDocument(
        Guid id,
        Guid gameId,
        Guid pdfDocumentId,
        string language,
        int totalChunks) : base(id)
    {
        if (string.IsNullOrWhiteSpace(language))
            throw new ArgumentException("Language cannot be empty", nameof(language));

        if (totalChunks <= 0)
            throw new ArgumentException("Total chunks must be positive", nameof(totalChunks));

        GameId = gameId;
        PdfDocumentId = pdfDocumentId;
        Language = language.ToLowerInvariant();
        TotalChunks = totalChunks;
        IndexedAt = DateTime.UtcNow;
        SearchCount = 0;

        AddDomainEvent(new VectorDocumentIndexedEvent(id, gameId, null, totalChunks));
    }

    /// <summary>
    /// Creates a new vector document for admin-owned shared game content (Issue #4921).
    /// </summary>
    public static VectorDocument CreateForSharedGame(
        Guid id,
        Guid sharedGameId,
        Guid pdfDocumentId,
        string language,
        int totalChunks)
    {
        if (string.IsNullOrWhiteSpace(language))
            throw new ArgumentException("Language cannot be empty", nameof(language));

        if (totalChunks <= 0)
            throw new ArgumentException("Total chunks must be positive", nameof(totalChunks));

        var doc = new VectorDocument
        {
            Id = id,
            SharedGameId = sharedGameId,
            PdfDocumentId = pdfDocumentId,
            Language = language.ToLowerInvariant(),
            TotalChunks = totalChunks,
            IndexedAt = DateTime.UtcNow,
            SearchCount = 0
        };

        doc.AddDomainEvent(new VectorDocumentIndexedEvent(id, null, sharedGameId, totalChunks));

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

    /// <summary>
    /// Sets metadata value (internal for mapper use only).
    /// </summary>
    internal void SetMetadata(string? metadata)
    {
        Metadata = metadata;
    }

    /// <summary>
    /// Sets SharedGameId value (internal for mapper reconstitution only, Issue #4921).
    /// </summary>
    internal void SetSharedGameId(Guid? sharedGameId)
    {
        SharedGameId = sharedGameId;
    }
}
