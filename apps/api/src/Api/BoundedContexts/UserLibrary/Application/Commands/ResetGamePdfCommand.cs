using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Command to reset to SharedGame's default PDF rulebook.
/// Removes any custom PDF for a game in user's library.
/// </summary>
/// <param name="UserId">The user who owns the library entry</param>
/// <param name="GameId">The game to reset PDF for</param>
internal record ResetGamePdfCommand(
    Guid UserId,
    Guid GameId
) : ICommand<UserLibraryEntryDto>;
