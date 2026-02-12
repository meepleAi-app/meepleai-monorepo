using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Query to generate preview data for uploaded PDF during game creation wizard.
/// Extracts game metadata, fetches BGG suggestions, and checks for duplicates.
/// Issue #4139: Backend - API Endpoints PDF Wizard
/// </summary>
/// <param name="FilePath">Path to uploaded PDF file from UploadPdfForGameExtractionCommand</param>
/// <param name="UserId">ID of user requesting preview (for audit trail)</param>
internal record GetPdfPreviewForWizardQuery(
    string FilePath,
    Guid UserId
) : IQuery<PdfGamePreviewDto>;
