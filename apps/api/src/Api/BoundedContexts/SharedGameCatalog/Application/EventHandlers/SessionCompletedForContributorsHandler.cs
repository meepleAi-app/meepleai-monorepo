using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetTopContributors;
using Api.Services;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.EventHandlers;

/// <summary>
/// Listens to GameManagement session-completion events and invalidates the
/// <c>top-contributors</c> HybridCache tag so the public
/// <c>/shared-games/top-contributors</c> leaderboard reflects the new score
/// on the next read instead of waiting for the L1/L2 TTL (15 min / 1 h).
///
/// Issue #593 (Wave A.3a) — spec §6.5.
///
/// <para>
/// Listens to:
/// <list type="bullet">
/// <item><see cref="GameSessionCompletedEvent"/> — bumps
/// <c>TotalSessions</c> for the session creator.</item>
/// <item><see cref="GameCompletedInNightEvent"/> — bumps <c>TotalWins</c>
/// for the declared winner (when present).</item>
/// </list>
/// Both contribute to the score formula <c>TotalSessions + TotalWins * 2</c>
/// (spec §5.4).
/// </para>
///
/// <para>
/// Tag <c>top-contributors</c> is kept in sync with
/// <see cref="GetTopContributorsQueryHandler.CacheTag"/>.
/// </para>
/// </summary>
internal sealed class SessionCompletedForContributorsHandler
    : INotificationHandler<GameSessionCompletedEvent>,
      INotificationHandler<GameCompletedInNightEvent>
{
    private readonly IHybridCacheService _cache;
    private readonly ILogger<SessionCompletedForContributorsHandler> _logger;

    public SessionCompletedForContributorsHandler(
        IHybridCacheService cache,
        ILogger<SessionCompletedForContributorsHandler> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(GameSessionCompletedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        await InvalidateAsync(
                nameof(GameSessionCompletedEvent),
                notification.SessionId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task Handle(GameCompletedInNightEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        await InvalidateAsync(
                nameof(GameCompletedInNightEvent),
                notification.SessionId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task InvalidateAsync(string eventName, Guid sessionId, CancellationToken ct)
    {
        var removed = await _cache
            .RemoveByTagAsync(GetTopContributorsQueryHandler.CacheTag, ct)
            .ConfigureAwait(false);
        _logger.LogInformation(
            "Invalidated {Removed} top-contributors cache entries after {Event} ({SessionId})",
            removed,
            eventName,
            sessionId);
    }
}
