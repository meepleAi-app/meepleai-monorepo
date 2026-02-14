using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to link an AI agent to a shared game.
/// Issue #4228: SharedGame and PrivateGame → AgentDefinition relationship
/// </summary>
internal record LinkAgentToSharedGameCommand(
    Guid GameId,
    Guid AgentId
) : ICommand<Unit>;
