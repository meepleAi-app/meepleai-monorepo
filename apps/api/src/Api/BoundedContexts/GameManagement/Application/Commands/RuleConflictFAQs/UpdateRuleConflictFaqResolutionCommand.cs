using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.RuleConflictFAQs;

/// <summary>
/// Command to update FAQ resolution text.
/// Issue #3966: CQRS commands for conflict FAQ management.
/// </summary>
internal record UpdateRuleConflictFaqResolutionCommand(
    Guid Id,
    string Resolution
) : ICommand;
