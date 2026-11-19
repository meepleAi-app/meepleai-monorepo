using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;

/// <summary>
/// Proposes a private game for the shared catalog: a <c>NewGameProposal</c> share request.
/// Issue #3665 (Phase 4); removed with #1423 and restored by #5074, where the game must have a description.
/// </summary>
/// <param name="UserId">ID of the user proposing the game; must own it.</param>
/// <param name="PrivateGameId">ID of the private game to propose.</param>
/// <param name="Notes">Optional notes for the reviewing admin.</param>
internal record ProposePrivateGameCommand(
    Guid UserId,
    Guid PrivateGameId,
    string? Notes = null
) : ICommand<CreateShareRequestResponse>;
