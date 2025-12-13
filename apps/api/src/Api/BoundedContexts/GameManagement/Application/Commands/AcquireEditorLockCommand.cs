using Api.BoundedContexts.GameManagement.Application.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Issue #2055: Command to acquire an editor lock for a RuleSpec.
/// </summary>
public record AcquireEditorLockCommand(
    Guid GameId,
    Guid UserId,
    string UserEmail
) : ICommand<EditorLockResult>;
