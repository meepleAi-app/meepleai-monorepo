using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;

/// <summary>
/// Repository interface for GameAnalyticsEvent aggregate operations.
/// Issue #3918: Catalog Trending Analytics Service
/// </summary>
public interface IGameAnalyticsEventRepository
{
    /// <summary>
    /// Adds a new game analytics event.
    /// </summary>
    /// <param name="analyticsEvent">The event to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddAsync(GameAnalyticsEvent analyticsEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all events within a time window for trending calculation.
    /// </summary>
    /// <param name="since">Start of the time window (UTC).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of events since the specified date.</returns>
    Task<List<GameAnalyticsEvent>> GetEventsSinceAsync(DateTime since, CancellationToken cancellationToken = default);
}
