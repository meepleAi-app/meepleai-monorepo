using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.EventHandlers;

/// <summary>
/// Issue #1852 (Gap A): propagates the PDF-extracted cover key from the
/// DocumentProcessing BC onto the corresponding SharedGame so library queries
/// can compute a presigned R2 URL without a cross-BC join at query time.
/// </summary>
internal sealed class PdfCoverGeneratedEventHandler : INotificationHandler<PdfCoverGeneratedEvent>
{
    private readonly ISharedGameRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHybridCacheService _cache;
    private readonly ICacheInvalidationRetryPolicy _cacheRetryPolicy;
    private readonly ILogger<PdfCoverGeneratedEventHandler> _logger;

    public PdfCoverGeneratedEventHandler(
        ISharedGameRepository repository,
        IUnitOfWork unitOfWork,
        IHybridCacheService cache,
        ICacheInvalidationRetryPolicy cacheRetryPolicy,
        ILogger<PdfCoverGeneratedEventHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _cacheRetryPolicy = cacheRetryPolicy ?? throw new ArgumentNullException(nameof(cacheRetryPolicy));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(PdfCoverGeneratedEvent notification, CancellationToken cancellationToken)
    {
        if (notification.SharedGameId is null)
        {
            _logger.LogDebug(
                "PdfCoverGenerated event for PDF {PdfDocumentId} has no SharedGameId, skipping.",
                notification.PdfDocumentId);
            return;
        }

        var sharedGameId = notification.SharedGameId.Value;
        var game = await _repository.GetByIdAsync(sharedGameId, cancellationToken).ConfigureAwait(false);

        if (game is null)
        {
            _logger.LogWarning(
                "SharedGame {SharedGameId} not found for PdfCoverGenerated event (PDF {PdfDocumentId}); game may have been deleted.",
                sharedGameId, notification.PdfDocumentId);
            return;
        }

        if (string.Equals(game.PdfCoverR2Key, notification.CoverR2Key, StringComparison.Ordinal))
        {
            _logger.LogDebug(
                "SharedGame {SharedGameId} already has PdfCoverR2Key={CoverR2Key}; skipping save.",
                sharedGameId, notification.CoverR2Key);
            return;
        }

        game.SetPdfCoverR2Key(notification.CoverR2Key);
        _repository.Update(game);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Issue #3143: the PDF-extracted cover (which OUTRANKS the Wikidata cover in
        // CoverUrlResolver precedence) changed on an existing game — evict the catalog
        // read-model caches so /shared-games and /shared-games/{id} reflect the new
        // coverUrl immediately instead of waiting for the 15min/1h TTL. Cross-replica
        // L1+L2 eviction via Redis Pub/Sub, mirroring #3138 / VectorDocumentIndexedForKbFlagHandler
        // (ADR-062). Only reached after the real key change (the three early returns above are no-ops).
        await _cacheRetryPolicy.ExecuteAsync(
            token => new ValueTask(_cache.RemoveByTagAcrossReplicasAsync("search-games", token)),
            "shared-games.list",
            cancellationToken).ConfigureAwait(false);

        await _cacheRetryPolicy.ExecuteAsync(
            token => new ValueTask(_cache.RemoveByTagAcrossReplicasAsync($"shared-game:{sharedGameId}", token)),
            "shared-games.detail",
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Propagated cover key to SharedGame {SharedGameId} from PDF {PdfDocumentId}.",
            sharedGameId, notification.PdfDocumentId);
    }
}
