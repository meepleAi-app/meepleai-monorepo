using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Issue #2055: Command to release an editor lock for a RuleSpec.
/// </summary>
internal record ReleaseEditorLockCommand(
    Guid GameId,
    Guid UserId
) : ICommand<bool>;
