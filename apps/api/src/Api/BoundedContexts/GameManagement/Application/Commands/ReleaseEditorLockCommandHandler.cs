using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Issue #2055: Handler for releasing editor locks.
/// </summary>
internal class ReleaseEditorLockCommandHandler : ICommandHandler<ReleaseEditorLockCommand, bool>
{
    private readonly IEditorLockService _lockService;

    public ReleaseEditorLockCommandHandler(IEditorLockService lockService)
    {
        _lockService = lockService ?? throw new ArgumentNullException(nameof(lockService));
    }

    public Task<bool> Handle(ReleaseEditorLockCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return _lockService.ReleaseLockAsync(
            command.GameId,
            command.UserId,
            cancellationToken);
    }
}
