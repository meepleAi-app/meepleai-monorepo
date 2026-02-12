using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Query to extract game metadata from uploaded PDF using SmolDocling + AI parsing.
/// Returns structured game information (title, year, players, etc.) with confidence score.
/// Issue #4155: Extract Game Metadata Query
/// </summary>
/// <param name="FilePath">Path to uploaded PDF file from UploadPdfForGameExtractionCommand</param>
/// <param name="UserId">ID of user performing extraction (for audit trail)</param>
public record ExtractGameMetadataFromPdfQuery(
    string FilePath,
    Guid UserId
) : IQuery<GameMetadataDto>;
