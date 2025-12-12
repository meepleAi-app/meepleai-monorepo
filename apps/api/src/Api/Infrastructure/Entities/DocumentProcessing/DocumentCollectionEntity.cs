using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Infrastructure.Entities;

/// <summary>
/// EF Core entity for DocumentCollection persistence.
/// Issue #2051: Multi-document collection storage
/// </summary>
[Table("document_collections")]
public class DocumentCollectionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // JSON storage for collection documents (embedded value objects)
    public string DocumentsJson { get; set; } = "[]";

    // Navigation properties
    public GameEntity Game { get; set; } = default!;
    public UserEntity CreatedBy { get; set; } = default!;

    // One-to-many: Collection has many PDFs
    public ICollection<PdfDocumentEntity> PdfDocuments { get; set; } = new List<PdfDocumentEntity>();
}
