namespace Api.Infrastructure.Entities;

public class PdfDocumentEntity
{
    public string Id { get; set; } = default!;
    public string TenantId { get; set; } = default!;
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

    public TenantEntity Tenant { get; set; } = default!;
    public GameEntity Game { get; set; } = default!;
    public UserEntity UploadedBy { get; set; } = default!;
}