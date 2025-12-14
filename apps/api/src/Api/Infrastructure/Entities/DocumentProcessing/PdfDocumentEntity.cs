using System.Text.Json;
using System.ComponentModel.DataAnnotations.Schema;
using Api.Models;

namespace Api.Infrastructure.Entities;

public class PdfDocumentEntity
{
    // DDD-PHASE2: Converted to Guid for domain alignment
    public Guid Id { get; set; } = Guid.NewGuid();
    // DDD-PHASE2: Converted to Guid for domain alignment
    public Guid GameId { get; set; }
    public string FileName { get; set; } = default!;
    public string FilePath { get; set; } = default!;
    public long FileSizeBytes { get; set; }
    public string ContentType { get; set; } = "application/pdf";
    // DDD-PHASE2: Converted to Guid for domain alignment
    public Guid UploadedByUserId { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public string? Metadata { get; set; } // JSON metadata

    // PDF-02: Text extraction fields
    public string? ExtractedText { get; set; }
    public string ProcessingStatus { get; set; } = "pending"; // pending, processing, completed, failed
    public DateTime? ProcessedAt { get; set; }
    public int? PageCount { get; set; }
    public int? CharacterCount { get; set; }
    public string? ProcessingError { get; set; }

    // PDF-03: Structured data extraction fields
    public string? ExtractedTables { get; set; } // JSON array of tables
    public string? ExtractedDiagrams { get; set; } // JSON array of diagram metadata
    public string? AtomicRules { get; set; } // JSON array of atomic rules from tables
    public int? TableCount { get; set; }
    public int? DiagramCount { get; set; }
    public int? AtomicRuleCount { get; set; }

    // PDF-08: Progress tracking
    public string? ProcessingProgressJson { get; set; }

    // AI-09: Multi-language support
    public string Language { get; set; } = "en"; // ISO 639-1 code: en, it, de, fr, es

    // AI-14: Hybrid search - PostgreSQL full-text search vector (automatically maintained by trigger)
    // This column is populated by the tsvector_update_pdf_documents trigger
    [Column("search_vector")]
    public string? SearchVector { get; set; }

    // Issue #2051: Multi-document collection support
    public Guid? CollectionId { get; set; }
    public string DocumentType { get; set; } = "base"; // base, expansion, errata, homerule
    public int SortOrder { get; set; } = 0;

    // Admin Wizard: Public library visibility (visible to all registered users)
    public bool IsPublic { get; set; } = false;

    [NotMapped]
    public ProcessingProgress? ProcessingProgress
    {
        get => ProcessingProgressJson == null
            ? null
            : JsonSerializer.Deserialize<ProcessingProgress>(ProcessingProgressJson);
        set => ProcessingProgressJson = value == null
            ? null
            : JsonSerializer.Serialize(value);
    }

    public GameEntity Game { get; set; } = default!;
    public UserEntity UploadedBy { get; set; } = default!;

    // Issue #2051: Navigation to collection
    public DocumentCollectionEntity? Collection { get; set; }
}
