using System.Globalization;
using Api.BoundedContexts.KbQuality.Application.Ports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KbQuality.Infrastructure.BackgroundJobs;

/// <summary>
/// Monthly cost-cap counter pruner (#1675 Task 21, plan amendment A1).
///
/// <para>Schedule: 1st of each calendar month at 00:05 UTC. The 5-minute offset gives
/// the system a beat past midnight so any in-flight evals at the rollover boundary
/// finish writing their counters first.</para>
///
/// <para>Plan A1 changed the storage from a generic <c>ISystemConfigStore</c> KV space
/// to a dedicated <c>kb_quality_budget_counters</c> table. The semantic stays the
/// same: cap is per <c>(tenant, yyyy-MM)</c>, the counter starts at 0 each month
/// implicitly by absence of a row. This job hard-deletes rows for any
/// <c>YearMonth &lt; (today - 1 month)</c> so the table doesn't accumulate stale
/// rows; the prior month is kept available for ~30 days post-rollover so the cost
/// audit endpoint can still surface it.</para>
///
/// <para>Errors are caught + logged but never bring the host down — pruning is
/// best-effort; the next monthly run picks up anything missed and the table size
/// remains bounded by <c>(tenants × months retained)</c> regardless of misses.</para>
/// </summary>
public sealed class KbQualityCostCapResetJob(
    IServiceProvider services,
    ILogger<KbQualityCostCapResetJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var nextRun = ComputeNextRunUtc(DateTime.UtcNow);
            var delay = nextRun - DateTime.UtcNow;
            try
            {
                await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            await SweepAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    internal static DateTime ComputeNextRunUtc(DateTime now)
    {
        var thisMonthFirstAtFive = new DateTime(now.Year, now.Month, 1, 0, 5, 0, DateTimeKind.Utc);
        return now < thisMonthFirstAtFive
            ? thisMonthFirstAtFive
            : thisMonthFirstAtFive.AddMonths(1);
    }

    /// <summary>
    /// Returns the <c>yyyy-MM</c> token strictly newer than which all counters should be
    /// pruned. With <paramref name="now"/> = 2026-06-01, this returns "2026-05" so rows
    /// for 2026-04 and earlier are deleted; the 2026-05 counter stays available for
    /// post-month audit lookups.
    /// </summary>
    internal static string ComputePriorYearMonthBoundary(DateTime now)
        => now.AddMonths(-1).ToString("yyyy-MM", CultureInfo.InvariantCulture);

    private async Task SweepAsync(CancellationToken ct)
    {
        try
        {
            using var scope = services.CreateScope();
            var budget = scope.ServiceProvider.GetRequiredService<IEvalCostBudgetChecker>();

            var boundary = ComputePriorYearMonthBoundary(DateTime.UtcNow);
            var deleted = await budget.DeleteBudgetCountersOlderThanAsync(boundary, ct).ConfigureAwait(false);

            logger.LogInformation(
                "KbQuality cost-cap reset pruned {Count} budget counters older than {Boundary}",
                deleted,
                boundary);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "KbQuality cost-cap reset failed; next monthly run will retry");
        }
    }
}
