#pragma warning disable MA0048 // File name must match type name - Contains related Request/Response DTOs

namespace Api.BoundedContexts.SystemConfiguration.Application.DTOs;

/// <summary>
/// Response DTO for PDF upload limits configuration.
/// Issue #3072: PDF Upload Limits - Admin API
/// </summary>
internal record PdfUploadLimitsDto(
    long MaxFileSizeBytes,
    int MaxPagesPerDocument,
    int MaxDocumentsPerGame,
    string[] AllowedMimeTypes,
    DateTime LastUpdatedAt,
    string? LastUpdatedByUserId
);

/// <summary>
/// Request DTO for updating PDF upload limits.
/// </summary>
internal record UpdatePdfUploadLimitsRequest(
    long MaxFileSizeBytes,
    int MaxPagesPerDocument,
    int MaxDocumentsPerGame,
    string[] AllowedMimeTypes
);
