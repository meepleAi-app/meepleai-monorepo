using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Command to configure custom AI agent for a game in user's library.
/// Replaces any existing custom agent configuration.
/// </summary>
/// <param name="UserId">The user who owns the library entry</param>
/// <param name="GameId">The game to configure</param>
/// <param name="AgentConfig">The agent configuration to apply</param>
internal record ConfigureGameAgentCommand(
    Guid UserId,
    Guid GameId,
    AgentConfigDto AgentConfig
) : ICommand<UserLibraryEntryDto>;
