using Api.BoundedContexts.GameToolkit.Domain.Events;
using Api.Services;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.EventHandlers;

/// <summary>
/// Listens to GameToolkit lifecycle events and invalidates the
/// SharedGameCatalog search-games cache so that the
/// <c>ToolkitsCount</c> aggregate (and the <c>HasToolkit</c> filter
/// projection) reflect the new state on the next read.
///
/// Issue #593 (Wave A.3a) — spec §6.5.
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

    private readonly IHybridCacheService _cache;
    private readonly ILogger<ToolkitChangedForCatalogAggregatesHandler> _logger;

    public ToolkitChangedForCatalogAggregatesHandler(
        IHybridCacheService cache,
        ILogger<ToolkitChangedForCatalogAggregatesHandler> logger)
    {
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
        var removed = await _cache.RemoveByTagAsync(SearchGamesTag, ct).ConfigureAwait(false);
        _logger.LogInformation(
            "Invalidated {Removed} search-games cache entries after {Event} ({ToolkitId})",
            removed,
            eventName,
            toolkitId);
    }
}
