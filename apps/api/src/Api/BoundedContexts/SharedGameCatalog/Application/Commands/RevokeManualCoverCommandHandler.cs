using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Covers;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Epic #3470 Slice 3a-3 — revokes a game's admin-set manual cover. Flow: load game (404 if
/// missing) → clear the manual columns on the aggregate (idempotent 204 no-op if nothing was
/// set) → persist → best-effort delete the R2 object → evict the read-model caches.
/// <para>
/// Persist-first mirrors the write path (<see cref="SetManualCoverCommandHandler"/> writes R2
/// then persists the key; revoke persists the null then deletes R2): the DB null is the
/// authoritative revoke, so a failed R2 delete only leaves an unreferenced orphan object rather
/// than a DB row pointing at a deleted object.
/// </para>
/// </summary>
internal sealed class RevokeManualCoverCommandHandler : ICommandHandler<RevokeManualCoverCommand, Unit>
{
    private readonly ISharedGameRepository _repository;
    private readonly IBlobStorageService _blobStorage;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHybridCacheService _cache;
    private readonly ICacheInvalidationRetryPolicy _cacheRetryPolicy;
    private readonly ILogger<RevokeManualCoverCommandHandler> _logger;

    public RevokeManualCoverCommandHandler(
        ISharedGameRepository repository,
        IBlobStorageService blobStorage,
        IUnitOfWork unitOfWork,
        IHybridCacheService cache,
        ICacheInvalidationRetryPolicy cacheRetryPolicy,
        ILogger<RevokeManualCoverCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _blobStorage = blobStorage ?? throw new ArgumentNullException(nameof(blobStorage));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _cacheRetryPolicy = cacheRetryPolicy ?? throw new ArgumentNullException(nameof(cacheRetryPolicy));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Unit> Handle(RevokeManualCoverCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var game = await _repository.GetByIdAsync(command.GameId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("SharedGame", command.GameId.ToString());

        // Capture the key BEFORE clearing so we can best-effort delete the object afterwards.
        var keyToDelete = game.ManualCoverR2Key;

        // Idempotent: nothing was set → no persistence, no R2 delete, no cache churn (204).
        if (!game.RevokeManualCover())
        {
            return Unit.Value;
        }

        _repository.Update(game);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Best-effort R2 cleanup AFTER the authoritative DB revoke. The raw-key delete (#3384)
        // targets the deterministic manual physical key; a failure leaves only an unreferenced
        // orphan (the DB no longer points at it), so it must not fail the request.
        if (!string.IsNullOrWhiteSpace(keyToDelete))
        {
            try
            {
                await _blobStorage.DeleteRawKeyAsync(
                    CoverKeyBuilder.PhysicalKeyFor(CoverKind.Manual, keyToDelete),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to delete manual cover R2 key {Key}; DB is already revoked", keyToDelete);
            }
        }

        // The cover columns the read-model resolves changed → evict the list + detail caches (AC-6).
        await CoverCacheInvalidation
            .EvictReadModelAsync(_cache, _cacheRetryPolicy, game.Id, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Manual cover revoked for game {GameId} by {AdminId}", game.Id, command.AdminId);

        return Unit.Value;
    }
}
