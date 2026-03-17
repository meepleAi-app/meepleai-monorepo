namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// DTO representing a recently processed PDF document linked to a SharedGame.
/// Used by the admin "Recently Processed" widget.
/// </summary>
public sealed record RecentlyProcessedDocumentDto(
    Guid PdfDocumentId,
    Guid? JobId,
    string FileName,
    string ProcessingState,
    DateTime Timestamp,
    string? ErrorCategory,
    bool CanRetry,
    Guid SharedGameId,
    string GameName,
    string? ThumbnailUrl);
