using Api.BoundedContexts.Administration.Application.Services;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands.DeleteRagSnapshot;

/// <summary>
/// Handles <see cref="DeleteRagSnapshotCommand"/>.
/// Deletes the specified RAG backup snapshot from storage.
/// </summary>
internal sealed class DeleteRagSnapshotCommandHandler : IRequestHandler<DeleteRagSnapshotCommand>
{
    private readonly IRagBackupStorageService _storage;
    private readonly ILogger<DeleteRagSnapshotCommandHandler> _logger;

    public DeleteRagSnapshotCommandHandler(
        IRagBackupStorageService storage,
        ILogger<DeleteRagSnapshotCommandHandler> logger)
    {
        _storage = storage;
        _logger = logger;
    }

    public async Task Handle(DeleteRagSnapshotCommand request, CancellationToken cancellationToken)
    {
        await _storage.DeleteSnapshotAsync(request.SnapshotId, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Admin deleted RAG snapshot {SnapshotId}", request.SnapshotId);
    }
}
