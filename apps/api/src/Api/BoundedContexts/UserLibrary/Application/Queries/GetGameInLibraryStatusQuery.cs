using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

/// <summary>
/// Query to check if a game is in the user's library and retrieve associated data counts.
/// Issue #4259: Collection Quick Actions for MeepleCard
/// </summary>
internal record GetGameInLibraryStatusQuery(
    Guid UserId,
    Guid GameId
) : IQuery<GameInLibraryStatusDto>;
