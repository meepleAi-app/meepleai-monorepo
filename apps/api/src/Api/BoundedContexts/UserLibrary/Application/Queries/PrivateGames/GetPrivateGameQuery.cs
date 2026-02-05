using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Queries.PrivateGames;

/// <summary>
/// Query to get a single private game by ID.
/// Issue #3663: Phase 2 - Private Game CRUD Operations.
/// </summary>
/// <param name="PrivateGameId">ID of the private game to retrieve</param>
/// <param name="UserId">ID of the requesting user (must be owner)</param>
internal record GetPrivateGameQuery(
    Guid PrivateGameId,
    Guid UserId
) : IQuery<PrivateGameDto>;
