using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.RuleConflictFAQs;

/// <summary>
/// Command to create a new RuleConflictFAQ entry.
/// Issue #3966: CQRS commands for conflict FAQ management.
/// </summary>
internal record CreateRuleConflictFaqCommand(
    Guid GameId,
    ConflictType ConflictType,
    string Pattern,
    string Resolution,
    int Priority
) : ICommand<Guid>;
