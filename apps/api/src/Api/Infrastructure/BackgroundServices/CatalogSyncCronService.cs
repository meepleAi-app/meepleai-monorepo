using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Models;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

namespace Api.Infrastructure.BackgroundServices;

/// <summary>
/// Periodic cron service that enqueues a new <see cref="CatalogSyncRun"/> at
/// <see cref="CatalogSyncCronConfiguration.IntervalHours"/> intervals (#1861 Phase 5).
/// </summary>
/// <remarks>
/// Scope (Phase 5 — "cron wrap" minimal):
/// <list type="bullet">
/// <item>Each tick: if no run is currently <see cref="CatalogSyncStatus.Running"/>, create a
/// new run via <see cref="CatalogSyncRun.Enqueue"/> + persist. Skip otherwise.</item>
/// <item>This service does NOT process queue items — item-level enrichment is handled by
/// <see cref="BggImportQueueBackgroundService"/> independently. The run lifecycle hook
/// (MarkRunning → Complete) for cron runs is deferred to a follow-up that wires the
/// queue processor into the catalog-sync aggregate (out of scope for #1861).</item>
/// <item>Opt-in: disabled unless <c>CatalogSyncCron:Enabled=true</c> in configuration.</item>
/// </list>
/// </remarks>
internal sealed class CatalogSyncCronService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CatalogSyncCronService> _logger;
    private readonly CatalogSyncCronConfiguration _config;

    public CatalogSyncCronService(
        IServiceScopeFactory scopeFactory,
        ILogger<CatalogSyncCronService> logger,
        IOptions<CatalogSyncCronConfiguration> config)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(config);
        _scopeFactory = scopeFactory;
        _logger = logger;
        _config = config.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.Enabled)
        {
            _logger.LogInformation(
                "Catalog sync cron service is disabled (set CatalogSyncCron:Enabled=true to enable).");
            return;
        }

        if (_config.IntervalHours <= 0)
        {
            _logger.LogWarning(
                "Catalog sync cron interval is {Hours} hours (invalid). Service disabled.",
                _config.IntervalHours);
            return;
        }

        _logger.LogInformation(
            "Catalog sync cron service starting. Interval: {Hours}h, InitialDelay: {Minutes}m",
            _config.IntervalHours, _config.InitialDelayMinutes);

        var initialDelay = TimeSpan.FromMinutes(Math.Max(0, _config.InitialDelayMinutes));
        if (initialDelay > TimeSpan.Zero)
        {
            try
            {
                await Task.Delay(initialDelay, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        using var timer = new PeriodicTimer(TimeSpan.FromHours(_config.IntervalHours));

        // Trigger once on startup (after initial delay), then on each timer tick.
        await TryTriggerSyncAsync(stoppingToken).ConfigureAwait(false);

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await TryTriggerSyncAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Single-tick body. Public for tests that drive the cron logic without spinning the timer.
    /// Idempotent: skips if a run is already in <see cref="CatalogSyncStatus.Running"/>.
    /// </summary>
    internal async Task TryTriggerSyncAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<ICatalogSyncRunRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var existing = await repo
                .GetCurrentRunningAsync(cancellationToken)
                .ConfigureAwait(false);

            if (existing is not null)
            {
                _logger.LogInformation(
                    "Skipping cron tick: catalog sync {RunId} is already Running.",
                    existing.Id);
                return;
            }

            var run = CatalogSyncRun.Enqueue(
                CatalogSyncProvider.BggApi,
                title: "BGG cron sync",
                triggeredBy: null);

            await repo.AddAsync(run, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Enqueued cron catalog sync run {RunId}.",
                run.Id);
        }
        catch (OperationCanceledException)
        {
            // Service is shutting down — ignore.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Catalog sync cron tick failed.");
        }
    }
}
