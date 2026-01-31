using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.Models;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Query to check if a game with the given BGG ID already exists in the catalog.
/// Returns both existing game data and fresh BGG data for comparison/diff display.
/// Issue: Admin Add Shared Game from BGG flow
/// </summary>
/// <param name="BggId">BoardGameGeek game ID to check</param>
public record CheckBggDuplicateQuery(int BggId) : IRequest<BggDuplicateCheckResult>;

/// <summary>
/// Result of duplicate check containing both existing and BGG data for diff.
/// </summary>
/// <param name="IsDuplicate">Whether a game with this BGG ID already exists</param>
/// <param name="ExistingGameId">ID of existing game if duplicate</param>
/// <param name="ExistingGame">Current game data from database (for diff comparison)</param>
/// <param name="BggData">Fresh data from BGG API (for diff comparison)</param>
public record BggDuplicateCheckResult(
    bool IsDuplicate,
    Guid? ExistingGameId,
    SharedGameDetailDto? ExistingGame,
    BggGameDetailsDto? BggData);
