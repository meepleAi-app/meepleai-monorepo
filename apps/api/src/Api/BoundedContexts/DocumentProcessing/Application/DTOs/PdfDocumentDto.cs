namespace Api.BoundedContexts.DocumentProcessing.Application.DTOs;

/// <summary>
/// Data transfer object for a PDF document.
/// Issue #5186: Added ProcessingState, ProgressPercentage, RetryCount, MaxRetries, CanRetry, ErrorCategory, ProcessingError for frontend status display.
/// </summary>
internal record PdfDocumentDto(
    Guid Id,
    Guid? GameId,
    string FileName,
    string FilePath,
    long FileSizeBytes,
    string ProcessingStatus,
    DateTime UploadedAt,
    DateTime? ProcessedAt,
    int? PageCount,
    string DocumentType = "base", // Issue #2051: base, expansion, errata, homerule
    bool IsPublic = false, // Admin Wizard: Public library visibility
    string ProcessingState = "Pending", // Issue #5186: granular 7-state pipeline (PdfProcessingState enum value)
    int ProgressPercentage = 0, // Issue #5186: 0-100 based on current state
    int RetryCount = 0, // Issue #5186: number of retries attempted
    int MaxRetries = 3, // Issue #5186: always 3 per domain model
    bool CanRetry = false,           // Issue #5186: true if state=Failed && retryCount < maxRetries
    string? ErrorCategory = null,    // Issue #5186: "Network"|"Parsing"|"Quota"|"Service"|"Unknown"
    string? ProcessingError = null   // Issue #5186: human-readable error message
);
