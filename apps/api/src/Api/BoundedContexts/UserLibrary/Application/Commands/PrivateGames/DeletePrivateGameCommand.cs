using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;

/// <summary>
/// Command to soft-delete a private game.
/// Issue #3663: Phase 2 - Private Game CRUD Operations.
/// </summary>
/// <param name="PrivateGameId">ID of the private game to delete</param>
/// <param name="UserId">ID of the user making the deletion (must be owner)</param>
internal record DeletePrivateGameCommand(
    Guid PrivateGameId,
    Guid UserId
) : ICommand;
