using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.BoundedContexts.SharedGameCatalog.Application.EventHandlers;

/// <summary>
/// Listens to KnowledgeBase AgentDefinition lifecycle events and invalidates
/// the SharedGameCatalog search-games cache so that the <c>AgentsCount</c>
/// aggregate (and the <c>HasAgent</c> filter projection) reflect the new
/// state on the next read.
///
/// Issue #593 (Wave A.3a) — spec §6.5.
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

    private readonly HybridCache _cache;
    private readonly ILogger<AgentDefinitionChangedForCatalogAggregatesHandler> _logger;

    public AgentDefinitionChangedForCatalogAggregatesHandler(
        HybridCache cache,
        ILogger<AgentDefinitionChangedForCatalogAggregatesHandler> logger)
    {
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
        await _cache.RemoveByTagAsync(SearchGamesTag, ct).ConfigureAwait(false);
        _logger.LogInformation(
            "Invalidated search-games cache entries after {Event} ({AgentDefinitionId})",
            eventName,
            agentDefinitionId);
    }
}
