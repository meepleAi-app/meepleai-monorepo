using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;

/// <summary>
/// Command to propose a private game for inclusion in the shared catalog.
/// Issue #3665: Phase 4 - Proposal System.
/// </summary>
/// <param name="UserId">ID of the user proposing the game</param>
/// <param name="PrivateGameId">ID of the private game to propose</param>
/// <param name="Notes">Optional notes explaining why this game should be added to catalog</param>
/// <param name="AttachedDocumentIds">Optional list of document IDs to attach (e.g., rulebook PDFs)</param>
internal record ProposePrivateGameCommand(
    Guid UserId,
    Guid PrivateGameId,
    string? Notes = null,
    List<Guid>? AttachedDocumentIds = null
) : ICommand<CreateShareRequestResponse>;
