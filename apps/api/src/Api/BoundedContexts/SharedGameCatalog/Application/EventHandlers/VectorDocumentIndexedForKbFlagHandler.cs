using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.Infrastructure;
using Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.BoundedContexts.SharedGameCatalog.Application.EventHandlers;

/// <summary>
/// Listens to VectorDocumentIndexedEvent and maintains the denormalized
/// `has_knowledge_base` column on `shared_games`. Pure async projection update
/// inside the SharedGameCatalog bounded context.
///
/// S2 epic tech-debt revision (CR-I1, CR-M4):
///   - The event now carries SharedGameId directly, so the handler no longer
///     reads VectorDocuments → fully BC-isolated.
///   - After the update, invalidate the HybridCache for
///     SearchSharedGamesQueryHandler so catalog consumers see the new flag
///     immediately instead of waiting for the L1/L2 TTL (15 min / 1 h).
///
/// Flow:
///   1. If notification.SharedGameId is null → noop.
///   2. Load the SharedGame row, flip HasKnowledgeBase to true if needed, save.
///   3. Invalidate cache entries tagged "search-games".
///
/// Idempotent: early-exit when already true. Single-row update.
///
/// Reference: docs/superpowers/specs/2026-04-09-library-to-game-epic-design.md §4.2
/// Plan: docs/superpowers/plans/2026-04-09-s2-kb-filter.md Task 7
/// </summary>
internal sealed class VectorDocumentIndexedForKbFlagHandler
    : INotificationHandler<VectorDocumentIndexedEvent>
{
    private readonly MeepleAiDbContext _context;
    private readonly HybridCache _cache;
    private readonly ICacheInvalidationRetryPolicy _retryPolicy;
    private readonly ILogger<VectorDocumentIndexedForKbFlagHandler> _logger;

    public VectorDocumentIndexedForKbFlagHandler(
        MeepleAiDbContext context,
        HybridCache cache,
        ICacheInvalidationRetryPolicy retryPolicy,
        ILogger<VectorDocumentIndexedForKbFlagHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(VectorDocumentIndexedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        // The event payload now carries SharedGameId directly — no cross-BC read.
        var sharedGameId = notification.SharedGameId;
        if (sharedGameId is null || sharedGameId == Guid.Empty)
        {
            _logger.LogDebug(
                "VectorDocumentIndexedEvent {DocumentId} has no SharedGameId, skipping KB flag update",
                notification.DocumentId);
            return;
        }

        // Load the SharedGame row, flip the flag if needed, save.
        // Idempotent: returns early if HasKnowledgeBase is already true.
        var sharedGame = await _context.SharedGames
            .FirstOrDefaultAsync(g => g.Id == sharedGameId.Value, cancellationToken)
            .ConfigureAwait(false);

        if (sharedGame is null || sharedGame.HasKnowledgeBase)
        {
            return;
        }

        sharedGame.HasKnowledgeBase = true;
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Invalidate all cached catalog query results so the new AI-ready flag
        // appears immediately instead of waiting for the L1/L2 TTL to expire.
        // RemoveByTagAsync with the "search-games" tag evicts every entry in the
        // SearchSharedGamesQueryHandler cache namespace.
        //
        // Multi-instance deployment caveat (epic library-to-game CR-I2):
        // HybridCache tag invalidation evicts the L2 distributed entry
        // (shared across nodes) but only the L1 MemoryCache of THIS instance.
        // Other API replicas still serve their own L1 cached values until
        // `LocalCacheExpiration` (15 min) expires. Worst case: a sticky-session
        // user on another replica sees a stale AI-ready flag for up to 15 min.
        // Acceptable for staging (single node); for multi-replica prod consider
        // either shortening LocalCacheExpiration or publishing an integration
        // event that each instance subscribes to for L1 cleanup.
        // Issue #613: bounded retry guards against transient Redis failures
        // that would otherwise leave the read-model serving a stale flag.
        await _retryPolicy.ExecuteAsync(
            token => _cache.RemoveByTagAsync("search-games", token),
            "shared-games.list",
            cancellationToken).ConfigureAwait(false);

        // Issue #603 (Wave A.4): also evict the per-game detail cache so the
        // next /shared-games/{id} read sees the new HasKnowledgeBase flag and
        // refreshed KbsCount/Kbs preview list.
        await _retryPolicy.ExecuteAsync(
            token => _cache.RemoveByTagAsync($"shared-game:{sharedGameId.Value}", token),
            "shared-games.detail",
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Set HasKnowledgeBase=true for SharedGame {SharedGameId} triggered by VectorDocument {DocumentId}",
            sharedGameId.Value,
            notification.DocumentId);
    }
}
