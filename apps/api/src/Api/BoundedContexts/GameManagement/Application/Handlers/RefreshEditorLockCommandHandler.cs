using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Issue #2055: Handler for refreshing editor locks.
/// </summary>
public class RefreshEditorLockCommandHandler : ICommandHandler<RefreshEditorLockCommand, bool>
{
    private readonly IEditorLockService _lockService;

    public RefreshEditorLockCommandHandler(IEditorLockService lockService)
    {
        _lockService = lockService ?? throw new ArgumentNullException(nameof(lockService));
    }

    public Task<bool> Handle(RefreshEditorLockCommand command, CancellationToken cancellationToken)
    {
        return _lockService.RefreshLockAsync(
            command.GameId,
            command.UserId,
            cancellationToken);
    }
}
