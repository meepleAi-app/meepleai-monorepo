using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetGameContributors;

/// <summary>
/// Query to retrieve all contributors for a specific shared game.
/// ISSUE-2735: API - Endpoints Contributor Stats
/// </summary>
/// <param name="SharedGameId">The ID of the shared game.</param>
public sealed record GetGameContributorsQuery(
    Guid SharedGameId
) : IQuery<List<GameContributorDto>>;
