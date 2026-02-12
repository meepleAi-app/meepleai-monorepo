using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to unlink an AI agent from a shared game.
/// Issue #4228: SharedGame and PrivateGame → AgentDefinition relationship
/// </summary>
internal record UnlinkAgentFromSharedGameCommand(
    Guid GameId
) : ICommand<Unit>;
