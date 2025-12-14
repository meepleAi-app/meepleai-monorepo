using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Entities;

/// <summary>
/// PdfDocument aggregate root representing an uploaded PDF with extraction metadata.
/// Issue #2029: Added Language support for PDF language filtering
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

    // Issue #2029: Language detection for PDF filtering
    public LanguageCode Language { get; private set; }

    // Issue #2051: Multi-document collection support
    public Guid? CollectionId { get; private set; }
    public DocumentType DocumentType { get; private set; }
    public int SortOrder { get; private set; }

#pragma warning disable CS8618
    private PdfDocument() : base() { }
#pragma warning restore CS8618

    public PdfDocument(
        Guid id,
        Guid gameId,
        FileName fileName,
        string filePath,
        FileSize fileSize,
        Guid uploadedByUserId,
        LanguageCode? language = null,
        Guid? collectionId = null,
        DocumentType? documentType = null,
        int sortOrder = 0) : base(id)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be empty", nameof(filePath));

        if (sortOrder < 0)
            throw new ArgumentException("Sort order cannot be negative", nameof(sortOrder));

        GameId = gameId;
        FileName = fileName;
        FilePath = filePath;
        FileSize = fileSize;
        ContentType = "application/pdf";
        UploadedByUserId = uploadedByUserId;
        UploadedAt = DateTime.UtcNow;
        ProcessingStatus = "pending";
        Language = language ?? LanguageCode.English; // Default to English
        CollectionId = collectionId;
        DocumentType = documentType ?? ValueObjects.DocumentType.Base; // Default to base
        SortOrder = sortOrder;
    }

    /// <summary>
    /// Reconstitutes a PdfDocument from persistence.
    /// Issue #2140: Replaces reflection-based property mutation
    /// </summary>
    public static PdfDocument Reconstitute(
        Guid id,
        Guid gameId,
        FileName fileName,
        string filePath,
        FileSize fileSize,
        Guid uploadedByUserId,
        DateTime uploadedAt,
        string processingStatus,
        DateTime? processedAt,
        int? pageCount,
        string? processingError,
        LanguageCode language,
        Guid? collectionId = null,
        DocumentType? documentType = null,
        int sortOrder = 0)
    {
        var document = new PdfDocument
        {
            Id = id,
            GameId = gameId,
            FileName = fileName,
            FilePath = filePath,
            FileSize = fileSize,
            ContentType = "application/pdf",
            UploadedByUserId = uploadedByUserId,
            UploadedAt = uploadedAt,
            ProcessingStatus = processingStatus,
            ProcessedAt = processedAt,
            PageCount = pageCount,
            ProcessingError = processingError,
            Language = language,
            CollectionId = collectionId,
            DocumentType = documentType ?? ValueObjects.DocumentType.Base,
            SortOrder = sortOrder
        };

        return document;
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

    // Issue #2029: Update detected language after processing
    public void UpdateLanguage(LanguageCode languageCode)
    {
        ArgumentNullException.ThrowIfNull(languageCode);
        Language = languageCode;
    }

    // Issue #2051: Assign document to collection
    public void AssignToCollection(Guid collectionId, DocumentType documentType, int sortOrder)
    {
        if (collectionId == Guid.Empty)
            throw new ArgumentException("Collection ID cannot be empty", nameof(collectionId));

        ArgumentNullException.ThrowIfNull(documentType);

        if (sortOrder < 0)
            throw new ArgumentException("Sort order cannot be negative", nameof(sortOrder));

        CollectionId = collectionId;
        DocumentType = documentType;
        SortOrder = sortOrder;
    }

    // Issue #2051: Remove from collection
    public void RemoveFromCollection()
    {
        CollectionId = null;
        DocumentType = ValueObjects.DocumentType.Base; // Reset to default
        SortOrder = 0;
    }
}
