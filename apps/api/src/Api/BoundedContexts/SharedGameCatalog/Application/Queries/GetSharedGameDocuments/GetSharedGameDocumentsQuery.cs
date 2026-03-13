using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetSharedGameDocuments;

/// <summary>
/// Query to get all documents (active and inactive) for a shared game.
/// Used by admin panel to display full document history with processing status.
/// Issue #119: Per-SharedGame Document Overview.
/// </summary>
/// <param name="SharedGameId">The ID of the shared game</param>
internal record GetSharedGameDocumentsQuery(
    Guid SharedGameId
) : IQuery<GetSharedGameDocumentsResult>;

/// <summary>
/// Result containing all documents for a shared game with PDF processing details.
/// </summary>
internal record GetSharedGameDocumentsResult(
    Guid SharedGameId,
    IReadOnlyList<SharedGameDocumentDetailDto> Documents,
    int TotalCount
);

/// <summary>
/// Extended document DTO including PDF processing state details for admin view.
/// </summary>
internal record SharedGameDocumentDetailDto(
    Guid Id,
    Guid SharedGameId,
    Guid PdfDocumentId,
    string DocumentType,
    string Version,
    bool IsActive,
    IReadOnlyList<string> Tags,
    DateTime CreatedAt,
    Guid CreatedBy,
    string ApprovalStatus,
    // PDF processing details
    string? FileName,
    string? ProcessingState,
    long? FileSizeBytes,
    DateTime? UploadedAt
);
