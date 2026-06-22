namespace Api.Models;

/// <summary>
/// Configuration for <see cref="Api.Infrastructure.BackgroundServices.CatalogSyncCronService"/>
/// (#1861 Phase 5). Bound to <c>CatalogSyncCron</c> section in appsettings.
/// </summary>
internal sealed class CatalogSyncCronConfiguration
{
    /// <summary>Enable/disable the cron service. Default: false (opt-in).</summary>
    public bool Enabled { get; set; }

    /// <summary>Interval between sync triggers in hours. Default: 6h.</summary>
    public double IntervalHours { get; set; } = 6.0;

    /// <summary>
    /// Delay before first tick fires after service starts, in minutes.
    /// Avoids tight loops on rapid restarts. Default: 5 min.
    /// </summary>
    public double InitialDelayMinutes { get; set; } = 5.0;
}
