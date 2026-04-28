using Api.BoundedContexts.GameToolkit.Domain.Events;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.BoundedContexts.SharedGameCatalog.Application.EventHandlers;

/// <summary>
/// Listens to GameToolkit lifecycle events and invalidates the
/// SharedGameCatalog caches so that the <c>ToolkitsCount</c> aggregate
/// (and the <c>HasToolkit</c> filter projection) reflect the new state on the
/// next read.
///
/// <para>
/// Two cache namespaces are evicted:
/// <list type="bullet">
/// <item><c>search-games</c> — index/list query cache (Issue #593, Wave A.3a).</item>
/// <item><c>shared-game:{id}</c> — per-game detail cache (Issue #603, Wave A.4):
/// looks up <c>Toolkit.GameId → Game.SharedGameId</c> to invalidate only the
/// affected detail entry rather than blowing away every detail cache.</item>
/// </list>
/// </para>
///
/// <para>
/// Listens to:
/// <list type="bullet">
/// <item><see cref="ToolkitCreatedEvent"/> — first toolkit on a game flips
/// <c>ToolkitsCount</c> from 0 → 1, satisfying the <c>hasToolkit</c> filter.</item>
/// <item><see cref="ToolkitPublishedEvent"/> — public visibility changes;
/// the catalog filter currently counts all toolkits regardless of publication
/// state, but invalidating here keeps us robust if §5.2 evolves to
/// "published-only" semantics.</item>
/// </list>
/// </para>
///
/// <para>
/// Multi-instance deployment caveat: <c>RemoveByTagAsync</c> evicts the L2
/// distributed entry but only the L1 of this instance — the same caveat
/// already documented in <see cref="VectorDocumentIndexedForKbFlagHandler"/>.
/// </para>
/// </summary>
internal sealed class ToolkitChangedForCatalogAggregatesHandler
    : INotificationHandler<ToolkitCreatedEvent>,
      INotificationHandler<ToolkitPublishedEvent>
{
    private const string SearchGamesTag = "search-games";

    private readonly MeepleAiDbContext _context;
    private readonly HybridCache _cache;
    private readonly ILogger<ToolkitChangedForCatalogAggregatesHandler> _logger;

    public ToolkitChangedForCatalogAggregatesHandler(
        MeepleAiDbContext context,
        HybridCache cache,
        ILogger<ToolkitChangedForCatalogAggregatesHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(ToolkitCreatedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        await InvalidateAsync(nameof(ToolkitCreatedEvent), notification.ToolkitId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task Handle(ToolkitPublishedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        await InvalidateAsync(nameof(ToolkitPublishedEvent), notification.ToolkitId, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task InvalidateAsync(string eventName, Guid toolkitId, CancellationToken ct)
    {
        // Always invalidate the catalog list cache.
        await _cache.RemoveByTagAsync(SearchGamesTag, ct).ConfigureAwait(false);

        // Look up the SharedGameId via the Game intermediate so we can scope
        // the per-game detail invalidation. Toolkits with no GameId, or with
        // a Game that has no SharedGameId, contribute nothing to detail cache.
        var sharedGameId = await (
            from t in _context.Toolkits
            where t.Id == toolkitId
            join g in _context.Games on t.GameId equals g.Id
            where g.SharedGameId != null
            select g.SharedGameId
        ).FirstOrDefaultAsync(ct).ConfigureAwait(false);

        if (sharedGameId is { } sgId && sgId != Guid.Empty)
        {
            await _cache.RemoveByTagAsync($"shared-game:{sgId}", ct).ConfigureAwait(false);
            _logger.LogInformation(
                "Invalidated search-games + shared-game:{SharedGameId} after {Event} ({ToolkitId})",
                sgId,
                eventName,
                toolkitId);
        }
        else
        {
            _logger.LogInformation(
                "Invalidated search-games cache after {Event} ({ToolkitId}); no SharedGame linkage",
                eventName,
                toolkitId);
        }
    }
}
