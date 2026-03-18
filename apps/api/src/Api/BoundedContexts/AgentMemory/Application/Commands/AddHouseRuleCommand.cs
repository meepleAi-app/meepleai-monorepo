using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.AgentMemory.Application.Commands;

/// <summary>
/// Command to add a house rule to a game's memory. Creates GameMemory if none exists.
/// </summary>
internal record AddHouseRuleCommand(
    Guid GameId,
    Guid OwnerId,
    string Description
) : ICommand;
