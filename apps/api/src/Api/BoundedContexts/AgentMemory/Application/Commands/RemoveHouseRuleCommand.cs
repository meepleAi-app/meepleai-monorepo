using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.AgentMemory.Application.Commands;

/// <summary>
/// Command to remove a house rule by id (#1464).
/// </summary>
internal record RemoveHouseRuleCommand(
    Guid GameId,
    Guid OwnerId,
    Guid RuleId
) : ICommand;
