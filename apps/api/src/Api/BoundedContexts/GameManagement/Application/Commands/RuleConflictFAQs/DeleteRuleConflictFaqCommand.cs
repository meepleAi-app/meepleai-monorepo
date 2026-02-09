using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.RuleConflictFAQs;

/// <summary>
/// Command to delete a RuleConflictFAQ entry.
/// Issue #3966: CQRS commands for conflict FAQ management.
/// </summary>
internal record DeleteRuleConflictFaqCommand(
    Guid Id
) : ICommand;
