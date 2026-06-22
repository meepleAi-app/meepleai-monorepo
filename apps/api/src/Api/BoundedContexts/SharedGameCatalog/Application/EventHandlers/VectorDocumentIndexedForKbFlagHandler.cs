using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.Infrastructure;
using Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

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
/// Epic #2242 Sub #6 Block C (ADR-062, this PR):
///   - Cache invalidation now uses <see cref="IHybridCacheService.RemoveByTagAcrossReplicasAsync"/>
///     so the eviction propagates to other API replicas via Redis Pub/Sub within sub-second
///     latency. Previously, only the calling replica's L1 cleared while other replicas kept
///     serving stale entries until LocalCacheExpiration (15-30 min) — the bug ADR-062 closes.
///
/// Flow:
///   1. If notification.SharedGameId is null → noop.
///   2. Load the SharedGame row, flip HasKnowledgeBase to true if needed, save.
///   3. Invalidate cache entries tagged "search-games" + "shared-game:{id}" across all replicas.
///
/// Idempotent: early-exit when already true. Single-row update.
///
/// Reference: docs/superpowers/specs/2026-04-09-library-to-game-epic-design.md §4.2
/// Plan: docs/superpowers/plans/2026-04-09-s2-kb-filter.md Task 7
/// ADR: docs/for-claude/architecture/adr/adr-062-kb-flag-cache-strategy.md
/// </summary>
internal sealed class VectorDocumentIndexedForKbFlagHandler
    : INotificationHandler<VectorDocumentIndexedEvent>
{
    private readonly MeepleAiDbContext _context;
    private readonly IHybridCacheService _cache;
    private readonly ICacheInvalidationRetryPolicy _retryPolicy;
    private readonly ILogger<VectorDocumentIndexedForKbFlagHandler> _logger;

    public VectorDocumentIndexedForKbFlagHandler(
        MeepleAiDbContext context,
        IHybridCacheService cache,
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
        // RemoveByTagAcrossReplicasAsync with the "search-games" tag evicts every entry in the
        // SearchSharedGamesQueryHandler cache namespace AND broadcasts the tag via Redis
        // Pub/Sub so other API replicas evict their L1 entries within sub-second.
        //
        // Cross-replica L1 invariant (ADR-062, epic #2242 Sub #6 Block C):
        // The wrapper performs local L1+L2 eviction then publishes to channel
        // "meepleai:cache-invalidate:tag". HybridCacheInvalidationSubscriber on
        // each replica receives the tag and evicts L1 locally. Net latency for
        // multi-replica propagation: ~1ms (Redis intra-DC Pub/Sub).
        // Failure mode: if Redis Pub/Sub is unreachable, only the broadcast step
        // is skipped — local eviction still completes; other replicas fall back to
        // LocalCacheExpiration (15-30 min). Defence-in-depth: KbFlagDriftAuditJob
        // (Block B) catches any residual drift within 10 minutes.
        //
        // Issue #613: bounded retry guards against transient Redis failures
        // that would otherwise leave the read-model serving a stale flag.
        await _retryPolicy.ExecuteAsync(
            token => new ValueTask(_cache.RemoveByTagAcrossReplicasAsync("search-games", token)),
            "shared-games.list",
            cancellationToken).ConfigureAwait(false);

        // Issue #603 (Wave A.4): also evict the per-game detail cache so the
        // next /shared-games/{id} read sees the new HasKnowledgeBase flag and
        // refreshed KbsCount/Kbs preview list.
        await _retryPolicy.ExecuteAsync(
            token => new ValueTask(_cache.RemoveByTagAcrossReplicasAsync($"shared-game:{sharedGameId.Value}", token)),
            "shared-games.detail",
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Set HasKnowledgeBase=true for SharedGame {SharedGameId} triggered by VectorDocument {DocumentId}",
            sharedGameId.Value,
            notification.DocumentId);
    }
}
