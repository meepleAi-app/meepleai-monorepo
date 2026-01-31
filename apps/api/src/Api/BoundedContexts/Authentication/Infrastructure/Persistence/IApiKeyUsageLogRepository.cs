using Api.BoundedContexts.Authentication.Domain.Entities;

namespace Api.BoundedContexts.Authentication.Infrastructure.Persistence;

/// <summary>
/// Repository interface for ApiKeyUsageLog aggregate.
/// Provides data access operations for API key usage logs.
/// </summary>
internal interface IApiKeyUsageLogRepository
{
    /// <summary>
    /// Adds a new usage log entry.
    /// </summary>
    Task AddAsync(ApiKeyUsageLog usageLog, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets usage logs for a specific API key with pagination.
    /// </summary>
    /// <param name="keyId">The API key ID to query.</param>
    /// <param name="skip">Number of records to skip (for pagination).</param>
    /// <param name="take">Number of records to take (for pagination).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of usage logs ordered by UsedAt descending (most recent first).</returns>
    Task<List<ApiKeyUsageLog>> GetByKeyIdAsync(
        Guid keyId,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets usage logs for a specific API key within a date range.
    /// </summary>
    Task<List<ApiKeyUsageLog>> GetByKeyIdAndDateRangeAsync(
        Guid keyId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets total usage count for a specific API key.
    /// </summary>
    Task<int> GetUsageCountAsync(Guid keyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets usage count for a specific API key within a date range.
    /// </summary>
    Task<int> GetUsageCountInRangeAsync(
        Guid keyId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all usage logs older than a specified date.
    /// Useful for data retention policies.
    /// </summary>
    Task<int> DeleteOlderThanAsync(DateTime cutoffDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves all pending changes.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
