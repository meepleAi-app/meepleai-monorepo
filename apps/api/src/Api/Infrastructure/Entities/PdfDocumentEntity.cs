namespace Api.Infrastructure.Entities;

public class PdfDocumentEntity
{
    public string Id { get; set; } = default!;
    public string GameId { get; set; } = default!;
    public string FileName { get; set; } = default!;
    public string FilePath { get; set; } = default!;
    public long FileSizeBytes { get; set; }
    public string ContentType { get; set; } = "application/pdf";
    public string UploadedByUserId { get; set; } = default!;
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

    public GameEntity Game { get; set; } = default!;
    public UserEntity UploadedBy { get; set; } = default!;
}