using Api.BoundedContexts.Administration.Domain.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Infrastructure.BackgroundJobs;

internal sealed class ProbeAuditRetentionJob : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _config;
    private readonly ILogger<ProbeAuditRetentionJob> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private static readonly TimeSpan InitialDelay = TimeSpan.FromHours(1);

    public ProbeAuditRetentionJob(
        IServiceProvider services,
        IConfiguration config,
        ILogger<ProbeAuditRetentionJob> logger)
    {
        _services = services;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(InitialDelay, stoppingToken).ConfigureAwait(false); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var days = _config.GetValue<int?>("Administration:ProbeAuditRetentionDays") ?? 365;
                var cutoff = DateTime.UtcNow.AddDays(-days);
                using var scope = _services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IProviderProbeAuditRepository>();
                var deleted = await repo.DeleteOlderThanAsync(cutoff, stoppingToken).ConfigureAwait(false);
                if (deleted > 0)
                    _logger.LogInformation("Probe audit retention deleted {Count} entries older than {Cutoff:O}", deleted, cutoff);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) { _logger.LogError(ex, "Probe audit retention job failed"); }

            try { await Task.Delay(Interval, stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
        }
    }
}
