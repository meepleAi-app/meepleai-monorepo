using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Command to reset AI agent to system default configuration.
/// Removes any custom agent configuration for a game in user's library.
/// </summary>
/// <param name="UserId">The user who owns the library entry</param>
/// <param name="GameId">The game to reset</param>
internal record ResetGameAgentCommand(
    Guid UserId,
    Guid GameId
) : ICommand<UserLibraryEntryDto>;
