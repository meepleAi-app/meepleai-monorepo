using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.BoundedContexts.SharedGameCatalog.Application.EventHandlers;

/// <summary>
/// Listens to KnowledgeBase AgentDefinition lifecycle events and invalidates
/// the SharedGameCatalog caches so that the <c>AgentsCount</c> aggregate
/// (and the <c>HasAgent</c> filter projection) reflect the new state on the
/// next read.
///
/// <para>
/// Two cache namespaces are evicted:
/// <list type="bullet">
/// <item><c>search-games</c> — index/list query cache (Issue #593, Wave A.3a).</item>
/// <item><c>shared-game:{id}</c> — per-game detail cache (Issue #603, Wave A.4):
/// looks up <c>AgentDefinition.GameId → Game.SharedGameId</c> to invalidate
/// only the affected detail entry.</item>
/// </list>
/// </para>
///
/// <para>
/// Listens to:
/// <list type="bullet">
/// <item><see cref="AgentDefinitionCreatedEvent"/></item>
/// <item><see cref="AgentDefinitionUpdatedEvent"/></item>
/// <item><see cref="AgentDefinitionActivatedEvent"/></item>
/// <item><see cref="AgentDefinitionDeactivatedEvent"/></item>
/// </list>
/// All four can change whether the game is surfaced under the
/// <c>hasAgent=true</c> filter.
/// </para>
/// </summary>
internal sealed class AgentDefinitionChangedForCatalogAggregatesHandler
    : INotificationHandler<AgentDefinitionCreatedEvent>,
      INotificationHandler<AgentDefinitionUpdatedEvent>,
      INotificationHandler<AgentDefinitionActivatedEvent>,
      INotificationHandler<AgentDefinitionDeactivatedEvent>
{
    private const string SearchGamesTag = "search-games";

    private readonly MeepleAiDbContext _context;
    private readonly HybridCache _cache;
    private readonly ILogger<AgentDefinitionChangedForCatalogAggregatesHandler> _logger;

    public AgentDefinitionChangedForCatalogAggregatesHandler(
        MeepleAiDbContext context,
        HybridCache cache,
        ILogger<AgentDefinitionChangedForCatalogAggregatesHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task Handle(AgentDefinitionCreatedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        return InvalidateAsync(nameof(AgentDefinitionCreatedEvent), notification.AgentDefinitionId, cancellationToken);
    }

    public Task Handle(AgentDefinitionUpdatedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        return InvalidateAsync(nameof(AgentDefinitionUpdatedEvent), notification.AgentDefinitionId, cancellationToken);
    }

    public Task Handle(AgentDefinitionActivatedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        return InvalidateAsync(nameof(AgentDefinitionActivatedEvent), notification.AgentDefinitionId, cancellationToken);
    }

    public Task Handle(AgentDefinitionDeactivatedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        return InvalidateAsync(nameof(AgentDefinitionDeactivatedEvent), notification.AgentDefinitionId, cancellationToken);
    }

    private async Task InvalidateAsync(string eventName, Guid agentDefinitionId, CancellationToken ct)
    {
        // Always invalidate the catalog list cache.
        await _cache.RemoveByTagAsync(SearchGamesTag, ct).ConfigureAwait(false);

        // Look up the SharedGameId via the Game intermediate so we can scope
        // the per-game detail invalidation. AgentDefinition.GameId is exposed
        // through a private backing field (`_gameId`) that EF maps to the
        // `game_id` column — we use EF.Property to access it in the query so
        // the expression is translatable to SQL.
        var sharedGameId = await (
            from a in _context.AgentDefinitions
            where a.Id == agentDefinitionId
                  && EF.Property<Guid?>(a, "_gameId") != null
            join g in _context.Games on EF.Property<Guid?>(a, "_gameId") equals g.Id
            where g.SharedGameId != null
            select g.SharedGameId
        ).FirstOrDefaultAsync(ct).ConfigureAwait(false);

        if (sharedGameId is { } sgId && sgId != Guid.Empty)
        {
            await _cache.RemoveByTagAsync($"shared-game:{sgId}", ct).ConfigureAwait(false);
            _logger.LogInformation(
                "Invalidated search-games + shared-game:{SharedGameId} after {Event} ({AgentDefinitionId})",
                sgId,
                eventName,
                agentDefinitionId);
        }
        else
        {
            _logger.LogInformation(
                "Invalidated search-games cache after {Event} ({AgentDefinitionId}); no SharedGame linkage",
                eventName,
                agentDefinitionId);
        }
    }
}
