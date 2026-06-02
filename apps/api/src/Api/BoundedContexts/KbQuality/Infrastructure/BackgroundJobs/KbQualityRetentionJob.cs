using Api.BoundedContexts.KbQuality.Application.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api.BoundedContexts.KbQuality.Infrastructure.BackgroundJobs;

/// <summary>
/// Daily retention sweep for completed evaluation runs (#1675 Task 20).
///
/// <para>Runs at 03:00 UTC each day; deletes any <c>DocumentEvaluationRun</c> whose
/// <c>CompletedAt</c> is older than <c>EvalQuality:RetentionMonths</c> (default 18).
/// In-flight (non-terminal) runs are preserved because <c>DeleteOlderThanAsync</c>
/// filters on <c>CompletedAt != null</c>; a stuck Pending row therefore needs a
/// separate operational intervention rather than being silently dropped here.</para>
///
/// <para>The 03:00 UTC slot is chosen to land outside both EU and US business hours so
/// the sweep's <c>ExecuteDeleteAsync</c> doesn't compete with admin traffic. Errors are
/// caught + logged but never bring the host down — retention is best-effort: the next
/// daily run will pick up anything missed.</para>
/// </summary>
public sealed class KbQualityRetentionJob(
    IServiceProvider services,
    IOptionsMonitor<EvalQualityOptions> options,
    ILogger<KbQualityRetentionJob> logger) : BackgroundService
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
        var todayAtThree = new DateTime(now.Year, now.Month, now.Day, 3, 0, 0, DateTimeKind.Utc);
        return now < todayAtThree ? todayAtThree : todayAtThree.AddDays(1);
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        try
        {
            using var scope = services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IEvaluationRepository>();
            var months = options.CurrentValue.RetentionMonths;
            var cutoff = DateTime.UtcNow.AddMonths(-months);

            var deleted = await repo.DeleteOlderThanAsync(cutoff, ct).ConfigureAwait(false);
            logger.LogInformation(
                "KbQuality retention sweep deleted {Count} runs older than {Cutoff:o}",
                deleted,
                cutoff);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "KbQuality retention sweep failed; next daily run will retry");
        }
    }
}
