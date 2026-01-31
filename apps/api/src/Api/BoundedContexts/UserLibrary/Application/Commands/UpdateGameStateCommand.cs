using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Command to update game state (Nuovo/InPrestito/Wishlist/Owned).
/// Emits GameStateChangedEvent on success.
/// </summary>
internal record UpdateGameStateCommand(
    Guid UserId,
    Guid GameId,
    string NewState, // "Nuovo", "InPrestito", "Wishlist", "Owned"
    string? StateNotes = null
) : ICommand;
