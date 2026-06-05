using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;

/// <summary>
/// Repository interface for <see cref="EnrichmentAttempt"/> (#1874).
/// </summary>
public interface IEnrichmentAttemptRepository
{
    /// <summary>Stages a new attempt record for insertion.</summary>
    Task AddAsync(EnrichmentAttempt attempt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the latest failed attempt per shared game within the trailing <paramref name="days"/> window,
    /// joined with the shared-game title. Soft-deleted shared games are excluded. Capped at
    /// <paramref name="limit"/> rows (caller caps at 100).
    /// </summary>
    Task<(IReadOnlyList<FailedItemAggregate> Items, int Total)> GetFailedAggregatesAsync(
        int days,
        int limit,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Read-model projection for the admin Failed Items panel: one row per shared game whose most
/// recent attempt was a failure within the time window.
/// </summary>
public sealed record FailedItemAggregate(
    Guid SharedGameId,
    string SharedGameTitle,
    string ErrorCode,
    string ErrorDetail,
    DateTimeOffset LastAttemptAt,
    int RetryCount);
