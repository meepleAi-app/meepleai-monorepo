using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Issue #2055: Handler for acquiring editor locks.
/// </summary>
internal class AcquireEditorLockCommandHandler : ICommandHandler<AcquireEditorLockCommand, EditorLockResult>
{
    private readonly IEditorLockService _lockService;

    public AcquireEditorLockCommandHandler(IEditorLockService lockService)
    {
        _lockService = lockService;
    }

    public Task<EditorLockResult> Handle(AcquireEditorLockCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return _lockService.AcquireLockAsync(
            command.GameId,
            command.UserId,
            command.UserEmail,
            cancellationToken);
    }
}
