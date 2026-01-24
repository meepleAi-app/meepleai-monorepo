using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

/// <summary>
/// Query to get complete game detail with statistics, sessions, and checklist.
/// Returns full game data for the detail page in user library.
/// </summary>
internal record GetGameDetailQuery(
    Guid UserId,
    Guid GameId
) : IQuery<GameDetailDto>;
