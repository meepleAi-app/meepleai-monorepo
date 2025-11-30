using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Entities;

/// <summary>
/// PdfDocument aggregate root representing an uploaded PDF with extraction metadata.
/// </summary>
public sealed class PdfDocument : AggregateRoot<Guid>
{
    public Guid GameId { get; private set; }
    public FileName FileName { get; private set; }
    public string FilePath { get; private set; }
    public FileSize FileSize { get; private set; }
    public string ContentType { get; private set; }
    public Guid UploadedByUserId { get; private set; }
    public DateTime UploadedAt { get; private set; }
    public string ProcessingStatus { get; private set; } // "pending", "processing", "completed", "failed"
    public DateTime? ProcessedAt { get; private set; }
    public int? PageCount { get; private set; }
    public string? ProcessingError { get; private set; }

#pragma warning disable CS8618
    private PdfDocument() : base() { }
#pragma warning restore CS8618

    public PdfDocument(
        Guid id,
        Guid gameId,
        FileName fileName,
        string filePath,
        FileSize fileSize,
        Guid uploadedByUserId) : base(id)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be empty", nameof(filePath));

        GameId = gameId;
        FileName = fileName;
        FilePath = filePath;
        FileSize = fileSize;
        ContentType = "application/pdf";
        UploadedByUserId = uploadedByUserId;
        UploadedAt = DateTime.UtcNow;
        ProcessingStatus = "pending";
    }

    public void MarkAsProcessing()
    {
        if (string.Equals(ProcessingStatus, "completed", StringComparison.Ordinal))
            throw new InvalidOperationException("Cannot reprocess completed document");

        ProcessingStatus = "processing";
    }

    public void MarkAsCompleted(int pageCount)
    {
        if (pageCount < 1)
            throw new ArgumentException("Page count must be at least 1", nameof(pageCount));

        ProcessingStatus = "completed";
        ProcessedAt = DateTime.UtcNow;
        PageCount = pageCount;
        ProcessingError = null;
    }

    public void MarkAsFailed(string error)
    {
        ProcessingStatus = "failed";
        ProcessedAt = DateTime.UtcNow;
        ProcessingError = error;
    }
}
