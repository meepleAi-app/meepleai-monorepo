using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Command to remove a game from user's library.
/// </summary>
internal record RemoveGameFromLibraryCommand(
    Guid UserId,
    Guid GameId
) : ICommand;
