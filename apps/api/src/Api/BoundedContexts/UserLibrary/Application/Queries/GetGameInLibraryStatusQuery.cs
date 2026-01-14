using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

/// <summary>
/// Query to check if a game is in user's library.
/// </summary>
internal record GetGameInLibraryStatusQuery(
    Guid UserId,
    Guid GameId
) : IQuery<GameInLibraryStatusDto>;
