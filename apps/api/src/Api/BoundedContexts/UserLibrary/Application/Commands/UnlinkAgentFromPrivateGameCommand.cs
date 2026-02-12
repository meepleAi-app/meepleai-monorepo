using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Command to unlink an AI agent from a private game.
/// Issue #4228: SharedGame and PrivateGame → AgentDefinition relationship
/// </summary>
internal record UnlinkAgentFromPrivateGameCommand(
    Guid GameId,
    Guid UserId
) : ICommand<Unit>;
