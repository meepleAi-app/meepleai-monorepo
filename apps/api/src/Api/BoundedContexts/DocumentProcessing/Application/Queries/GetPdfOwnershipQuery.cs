using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Query to get PDF ownership and status information.
/// Used for authorization and status checks (delete, cancel operations).
/// SEC-02: Row-Level Security for PDF operations
/// </summary>
public sealed record GetPdfOwnershipQuery(
    Guid PdfId
) : IQuery<PdfOwnershipResult?>;

/// <summary>
/// Result containing PDF ownership and status.
/// </summary>
public record PdfOwnershipResult(
    Guid Id,
    Guid UploadedByUserId,
    Guid GameId,
    string ProcessingStatus
);
