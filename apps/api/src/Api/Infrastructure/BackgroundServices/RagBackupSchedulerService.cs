using System.Globalization;
using Api.BoundedContexts.Administration.Application.Commands.ExportRagData;
using Api.BoundedContexts.Administration.Application.Services;
using MediatR;

namespace Api.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that runs a full RAG backup snapshot every Sunday at 03:00 UTC.
/// Old snapshots are pruned based on the BACKUP_RETENTION_WEEKS configuration value (default: 4).
/// The "latest" snapshot is always preserved regardless of age.
/// </summary>
internal sealed class RagBackupSchedulerService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RagBackupSchedulerService> _logger;

    public RagBackupSchedulerService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<RagBackupSchedulerService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RagBackupSchedulerService started. Weekly snapshot: Sunday 03:00 UTC");

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(CheckInterval, stoppingToken).ConfigureAwait(false);

            if (!IsScheduledTime()) continue;

#pragma warning disable CA1031 // Do not catch general exception types
            // BACKGROUND SERVICE: Generic catch prevents service from crashing the host process.
            try
            {
                await RunWeeklyBackupAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Weekly RAG backup failed");
            }
#pragma warning restore CA1031
        }

        _logger.LogInformation("RagBackupSchedulerService stopped");
    }

    internal static bool IsScheduledTime()
    {
        var now = DateTime.UtcNow;
        return now.DayOfWeek == DayOfWeek.Sunday && now.Hour == 3;
    }

    internal async Task RunWeeklyBackupAsync(CancellationToken ct)
    {
        _logger.LogInformation("Starting weekly RAG backup snapshot");

        using var scope = _scopeFactory.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var storageService = scope.ServiceProvider.GetRequiredService<IRagBackupStorageService>();

        var result = await mediator.Send(new ExportRagDataCommand(), ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Weekly RAG backup completed: SnapshotId={SnapshotId}, Documents={Documents}, Chunks={Chunks}, Embeddings={Embeddings}, Skipped={Skipped}, Failed={Failed}",
            result.SnapshotId,
            result.TotalDocuments,
            result.TotalChunks,
            result.TotalEmbeddings,
            result.Skipped,
            result.Failed);

        if (result.Errors.Count > 0)
        {
            _logger.LogWarning(
                "Weekly RAG backup completed with {ErrorCount} error(s): {Errors}",
                result.Errors.Count,
                string.Join("; ", result.Errors));
        }

        await PruneOldSnapshotsAsync(storageService, ct).ConfigureAwait(false);
    }

    internal async Task PruneOldSnapshotsAsync(IRagBackupStorageService storageService, CancellationToken ct)
    {
        var retentionWeeks = _configuration.GetValue<int>("BACKUP_RETENTION_WEEKS", 4);
        var cutoff = DateTime.UtcNow.AddDays(-retentionWeeks * 7);

        _logger.LogInformation(
            "Pruning RAG snapshots older than {Cutoff:O} (retention: {RetentionWeeks} weeks)",
            cutoff, retentionWeeks);

        List<string> snapshots;
        try
        {
            snapshots = await storageService.ListSnapshotsAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list RAG snapshots for pruning");
            return;
        }

        var pruned = 0;

        foreach (var snapshotId in snapshots)
        {
            // Always preserve the "latest" snapshot
            if (string.Equals(snapshotId, "latest", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!DateTime.TryParseExact(
                    snapshotId,
                    "yyyy-MM-dd-HHmmss",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var snapshotDate))
            {
                _logger.LogDebug("Skipping snapshot with unparseable date format: {SnapshotId}", snapshotId);
                continue;
            }

            if (snapshotDate >= cutoff)
                continue;

#pragma warning disable CA1031 // Do not catch general exception types
            // BACKGROUND SERVICE: Failure to prune one snapshot must not block pruning of others.
            try
            {
                await storageService.DeleteSnapshotAsync(snapshotId, ct).ConfigureAwait(false);
                pruned++;
                _logger.LogInformation("Pruned old RAG snapshot: {SnapshotId}", snapshotId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to prune RAG snapshot {SnapshotId}", snapshotId);
            }
#pragma warning restore CA1031
        }

        _logger.LogInformation(
            "RAG snapshot pruning completed: {Pruned}/{Total} snapshot(s) removed",
            pruned, snapshots.Count);
    }
}
