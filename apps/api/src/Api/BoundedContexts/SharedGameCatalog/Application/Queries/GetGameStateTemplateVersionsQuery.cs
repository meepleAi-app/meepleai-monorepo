using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Query to get all game state template versions for a shared game.
/// Returns versions sorted by version number (descending).
/// Issue #2400: GameStateTemplate Entity + AI Generation
/// </summary>
/// <param name="SharedGameId">The ID of the shared game</param>
internal record GetGameStateTemplateVersionsQuery(
    Guid SharedGameId
) : IQuery<IReadOnlyList<GameStateTemplateDto>>;
