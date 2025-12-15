using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Issue #2055: Command to refresh an editor lock's TTL.
/// </summary>
internal record RefreshEditorLockCommand(
    Guid GameId,
    Guid UserId
) : ICommand<bool>;
