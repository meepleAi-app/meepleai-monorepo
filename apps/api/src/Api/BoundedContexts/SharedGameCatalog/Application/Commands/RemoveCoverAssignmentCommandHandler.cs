using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Epic #3470 — removes a context's cover assignment (resetting it to the resolver's
/// implicit precedence) through the aggregate + reconcile path. Removing an absent
/// assignment is an idempotent no-op; only a missing game is a 404.
///
/// Slice 2 (AC-6): evicts the read-model caches (list + detail) across replicas after
/// the change so the reverted cover renders immediately instead of waiting for the TTL.
/// </summary>
internal sealed class RemoveCoverAssignmentCommandHandler : ICommandHandler<RemoveCoverAssignmentCommand, Unit>
{
    private readonly ISharedGameRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHybridCacheService _cache;
    private readonly ICacheInvalidationRetryPolicy _cacheRetryPolicy;
    private readonly IBlobStorageService _blobStorage;
    private readonly ILogger<RemoveCoverAssignmentCommandHandler> _logger;

    public RemoveCoverAssignmentCommandHandler(
        ISharedGameRepository repository,
        IUnitOfWork unitOfWork,
        IHybridCacheService cache,
        ICacheInvalidationRetryPolicy cacheRetryPolicy,
        IBlobStorageService blobStorage,
        ILogger<RemoveCoverAssignmentCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _cacheRetryPolicy = cacheRetryPolicy ?? throw new ArgumentNullException(nameof(cacheRetryPolicy));
        _blobStorage = blobStorage ?? throw new ArgumentNullException(nameof(blobStorage));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Unit> Handle(RemoveCoverAssignmentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var game = await _repository.GetByIdAsync(command.GameId, cancellationToken).ConfigureAwait(false);
        if (game is null)
        {
            throw new NotFoundException("SharedGame", command.GameId.ToString());
        }

        // #3615: capture the rendered crop's key BEFORE removing the assignment — afterwards the
        // aggregate no longer knows it existed, and the R2 object would be orphaned with nothing
        // left pointing at it. Mirrors RevokeManualCoverCommandHandler, which already does this.
        var cropKeyToDelete = game.CoverAssignments
            .FirstOrDefault(a => a.Context == command.Context)?.GeneratedR2Key;

        game.RemoveCoverAssignment(command.Context);

        await _repository.ReconcileCoverAssignmentsAsync(game, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Best-effort cleanup AFTER the authoritative DB removal, same ordering as the revoke path:
        // the removed row is what makes the crop unreachable, so a failed delete leaves only an
        // unreferenced object rather than a row pointing at a deleted one. Never fails the request.
        if (!string.IsNullOrWhiteSpace(cropKeyToDelete))
        {
            try
            {
                await _blobStorage.DeleteRawKeyAsync(cropKeyToDelete, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex,
                    "Failed to delete rendered crop {Key} for {Context} on game {GameId}; the assignment is already removed",
                    cropKeyToDelete, command.Context, command.GameId);
            }
        }

        await CoverCacheInvalidation
            .EvictReadModelAsync(_cache, _cacheRetryPolicy, command.GameId, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Removed cover assignment for {Context} on game {GameId} by {AdminId}",
            command.Context, command.GameId, command.AdminId);

        return Unit.Value;
    }
}
