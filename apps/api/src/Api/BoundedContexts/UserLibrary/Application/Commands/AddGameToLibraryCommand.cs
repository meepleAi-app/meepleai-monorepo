using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Command to add a game to user's library.
/// </summary>
internal record AddGameToLibraryCommand(
    Guid UserId,
    Guid GameId,
    string? Notes = null,
    bool IsFavorite = false
) : ICommand<UserLibraryEntryDto>;
