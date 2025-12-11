using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Query with Result record
namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Query to get PDF processing progress.
/// Includes ownership info for authorization.
/// PDF-08: Get PDF processing progress
/// </summary>
public sealed record GetPdfProgressQuery(
    Guid PdfId
) : IQuery<PdfProgressResult?>;

/// <summary>
/// Result containing PDF progress data and ownership.
/// </summary>
public record PdfProgressResult(
    Guid Id,
    Guid UploadedByUserId,
    string? ProcessingProgressJson
);
