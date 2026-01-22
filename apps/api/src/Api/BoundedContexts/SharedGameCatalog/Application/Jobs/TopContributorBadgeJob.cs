using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Jobs;

/// <summary>
/// Background job that periodically recalculates and awards/revokes TopContributor badges.
/// Runs monthly to identify the top 10 contributors and manage badge assignments.
/// </summary>
/// <remarks>
/// STUB IMPLEMENTATION - Requires background job infrastructure (Hangfire/Quartz).
/// Issue #2728: Application - Badge Assignment Handlers
/// </remarks>
internal sealed class TopContributorBadgeJob
{
    private readonly ILogger<TopContributorBadgeJob> _logger;

    public TopContributorBadgeJob(ILogger<TopContributorBadgeJob> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes the monthly top contributor badge calculation.
    /// </summary>
    /// <remarks>
    /// FUTURE IMPLEMENTATION: Requires background job infrastructure (Hangfire/Quartz).
    ///
    /// Implementation steps:
    /// 1. Query leaderboard for top 10 contributors (monthly period)
    /// 2. Award TopContributor badge to top 10 users
    /// 3. Revoke TopContributor badge from users no longer in top 10
    /// 4. Log results for monitoring
    ///
    /// Scheduling: Monthly on 1st day of month at 00:00 UTC
    /// </remarks>
    public Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "TopContributorBadgeJob.ExecuteAsync called but not yet implemented. " +
            "Requires background job infrastructure (Hangfire/Quartz) to be configured.");

        throw new NotSupportedException(
            "TopContributorBadgeJob requires background job infrastructure. " +
            "Please configure Hangfire or Quartz.NET for scheduled badge evaluation.");
    }
}
