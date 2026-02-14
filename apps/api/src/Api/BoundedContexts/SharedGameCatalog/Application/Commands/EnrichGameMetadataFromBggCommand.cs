using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to enrich PDF-extracted game metadata with BGG data.
/// Merges data from both sources, detects conflicts, and returns enriched result.
/// Issue #4156: BGG Match and Enrichment Command
/// </summary>
/// <param name="ExtractedMetadata">Game metadata extracted from PDF (Issue #4155)</param>
/// <param name="BggId">BoardGameGeek game ID to fetch additional data</param>
/// <param name="UserId">User ID for audit trail</param>
public record EnrichGameMetadataFromBggCommand(
    GameMetadataDto ExtractedMetadata,
    int BggId,
    Guid UserId
) : ICommand<EnrichedGameDto>;
