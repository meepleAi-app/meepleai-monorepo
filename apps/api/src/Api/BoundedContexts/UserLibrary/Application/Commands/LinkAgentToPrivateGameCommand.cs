using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Command to link an AI agent to a private game.
/// Issue #4228: SharedGame and PrivateGame → AgentDefinition relationship
/// </summary>
internal record LinkAgentToPrivateGameCommand(
    Guid GameId,
    Guid AgentId,
    Guid UserId
) : ICommand<Unit>;
