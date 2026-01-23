namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Service for retrieving review lock configuration values.
/// Issue #2729: Application - Review Lock Management
/// </summary>
internal interface IReviewLockConfigService
{
    /// <summary>
    /// Gets the default lock duration in minutes for share request reviews.
    /// Falls back to 30 minutes if not configured.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Lock duration in minutes.</returns>
    Task<int> GetDefaultLockDurationMinutesAsync(CancellationToken cancellationToken = default);
}
