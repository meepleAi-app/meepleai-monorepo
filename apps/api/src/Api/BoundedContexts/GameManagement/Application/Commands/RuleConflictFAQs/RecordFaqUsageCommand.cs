using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.RuleConflictFAQs;

/// <summary>
/// Internal command to record FAQ usage when ArbitroAgent uses a resolution.
/// Issue #3966: CQRS commands for conflict FAQ management.
/// </summary>
internal record RecordFaqUsageCommand(
    Guid Id
) : ICommand;
