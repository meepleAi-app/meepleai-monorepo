using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// VectorDocument aggregate root.
/// Represents a document that has been indexed in the vector database.
/// Controls embeddings and search operations for the document.
/// </summary>
public sealed class VectorDocument : AggregateRoot<Guid>
{
    public Guid GameId { get; private set; }
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
    /// Creates a new vector document.
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

        // TODO: Add domain event VectorDocumentIndexed
    }

    /// <summary>
    /// Records that this document was searched.
    /// </summary>
    public void RecordSearch()
    {
        LastSearchedAt = DateTime.UtcNow;
        SearchCount++;

        // TODO: Add domain event VectorDocumentSearched
    }

    /// <summary>
    /// Updates metadata (e.g., quality metrics, usage stats).
    /// </summary>
    public void UpdateMetadata(string metadata)
    {
        Metadata = metadata;
        // TODO: Add domain event VectorDocumentMetadataUpdated
    }
}
