using Api.BoundedContexts.KnowledgeBase.Domain.Chunking;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services.Chunking;

/// <summary>
/// ADR-016 Phase 1: Interface for advanced hierarchical document chunking.
/// Creates parent/child chunk relationships with metadata enrichment.
/// </summary>
public interface IAdvancedChunkingService
{
    /// <summary>
    /// Chunks a document into hierarchical chunks with parent/child relationships.
    /// </summary>
    /// <param name="document">The extracted document content.</param>
    /// <param name="config">Chunking configuration (optional, will auto-select if null).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of hierarchical chunks with parent/child relationships.</returns>
    Task<List<HierarchicalChunk>> ChunkDocumentAsync(
        ExtractedDocument document,
        ChunkingConfiguration? config = null,
        CancellationToken ct = default);

    /// <summary>
    /// Chunks plain text into hierarchical chunks.
    /// </summary>
    /// <param name="text">The text content to chunk.</param>
    /// <param name="documentId">The source document identifier.</param>
    /// <param name="config">Chunking configuration (optional).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of hierarchical chunks.</returns>
    Task<List<HierarchicalChunk>> ChunkTextAsync(
        string text,
        Guid documentId,
        ChunkingConfiguration? config = null,
        CancellationToken ct = default);
}

/// <summary>
/// Represents an extracted document with content and metadata.
/// Used as input to the advanced chunking service.
/// </summary>
public sealed record ExtractedDocument
{
    /// <summary>
    /// Document identifier.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Associated game identifier (optional).
    /// </summary>
    public Guid? GameId { get; init; }

    /// <summary>
    /// Full text content of the document.
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// Document sections with their metadata.
    /// </summary>
    public List<DocumentSection> Sections { get; init; } = new();

    /// <summary>
    /// Total page count.
    /// </summary>
    public int PageCount { get; init; }
}

/// <summary>
/// Represents a section within an extracted document.
/// </summary>
public sealed record DocumentSection
{
    /// <summary>
    /// Section heading/title.
    /// </summary>
    public string? Heading { get; init; }

    /// <summary>
    /// Section content.
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// Starting page number.
    /// </summary>
    public int Page { get; init; }

    /// <summary>
    /// Element type from extraction (text, table, list, heading).
    /// </summary>
    public string ElementType { get; init; } = "text";

    /// <summary>
    /// Character start position in original document.
    /// </summary>
    public int CharStart { get; init; }

    /// <summary>
    /// Character end position in original document.
    /// </summary>
    public int CharEnd { get; init; }

    /// <summary>
    /// Optional bounding box for spatial information.
    /// </summary>
    public BoundingBox? BBox { get; init; }
}
