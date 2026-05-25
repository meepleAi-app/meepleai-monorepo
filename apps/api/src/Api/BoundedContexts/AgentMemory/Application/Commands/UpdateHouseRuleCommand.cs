using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.AgentMemory.Application.Commands;

/// <summary>
/// Command to update an existing house rule's description (#1464).
/// </summary>
internal record UpdateHouseRuleCommand(
    Guid GameId,
    Guid OwnerId,
    Guid RuleId,
    string Description
) : ICommand;
