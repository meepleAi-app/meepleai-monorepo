using Api.Infrastructure.Entities;

namespace Api.Infrastructure.Entities.SharedGameCatalog;

/// <summary>
/// Persistence entity for SharedGameDocument.
/// Associates PDF documents with shared games with versioning support.
/// </summary>
public class SharedGameDocumentEntity
{
    /// <summary>
    /// Unique identifier for the document association.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// FK to the shared game.
    /// </summary>
    public Guid SharedGameId { get; set; }

    /// <summary>
    /// FK to the PDF document.
    /// </summary>
    public Guid PdfDocumentId { get; set; }

    /// <summary>
    /// Type of document (0=Rulebook, 1=Errata, 2=Homerule).
    /// </summary>
    public int DocumentType { get; set; }

    /// <summary>
    /// Version string (e.g., "1.0", "2.1").
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// Whether this is the active version for its type.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// JSON array of tags (only for Homerule type).
    /// </summary>
    public string? TagsJson { get; set; }

    /// <summary>
    /// Creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// User who created this association.
    /// </summary>
    public Guid CreatedBy { get; set; }

    // Navigation properties
    public SharedGameEntity SharedGame { get; set; } = default!;
    public PdfDocumentEntity PdfDocument { get; set; } = default!;
}
