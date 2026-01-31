using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Query to get the active game state template for a shared game.
/// Returns null if no active template exists.
/// Issue #2400: GameStateTemplate Entity + AI Generation
/// </summary>
/// <param name="SharedGameId">The ID of the shared game</param>
internal record GetActiveGameStateTemplateQuery(
    Guid SharedGameId
) : IQuery<GameStateTemplateDto?>;
