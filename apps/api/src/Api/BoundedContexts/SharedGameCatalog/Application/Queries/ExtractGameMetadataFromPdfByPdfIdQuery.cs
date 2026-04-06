using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Query to extract game metadata from an uploaded PDF identified by its PdfDocument ID.
/// Used by the 5-step import wizard where the PDF is uploaded first (orphaned),
/// then metadata is extracted in step 2 via the PdfDocument record.
/// </summary>
/// <param name="PdfDocumentId">ID of the already-uploaded PdfDocument (GameId may be null = orphaned)</param>
/// <param name="UserId">ID of the requesting user (for audit)</param>
public record ExtractGameMetadataFromPdfByPdfIdQuery(
    Guid PdfDocumentId,
    Guid UserId
) : IQuery<GameMetadataDto>;
