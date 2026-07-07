using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that runs once on startup to recover PDFs stuck in
/// "pending" or "processing" status (e.g. after a container restart that
/// killed in-memory background tasks).
/// </summary>
internal sealed class StalePdfRecoveryService : BackgroundService
{
    /// <summary>Time to wait after startup for services to initialize.</summary>
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);

    /// <summary>A "pending" PDF older than this is considered stale.</summary>
    private static readonly TimeSpan PendingStaleness = TimeSpan.FromMinutes(2);

    /// <summary>A "processing" PDF older than this is considered stale.</summary>
    private static readonly TimeSpan ProcessingStaleness = TimeSpan.FromMinutes(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StalePdfRecoveryService> _logger;

    public StalePdfRecoveryService(
        IServiceScopeFactory scopeFactory,
        ILogger<StalePdfRecoveryService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[StalePdfRecovery] Service started, waiting {Delay}s for initialization",
            StartupDelay.TotalSeconds);

        try
        {
            await Task.Delay(StartupDelay, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogInformation(ex, "[StalePdfRecovery] Cancelled during startup delay");
            return;
        }

        _logger.LogInformation("[StalePdfRecovery] Scanning for stale PDFs");

        List<StalePdfInfo> stalePdfs;

        try
        {
            stalePdfs = await FindStalePdfsAsync(stoppingToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Service boundary - must not crash host
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "[StalePdfRecovery] Failed to query for stale PDFs");
            return;
        }
#pragma warning restore CA1031

        if (stalePdfs.Count == 0)
        {
            _logger.LogInformation("[StalePdfRecovery] No stale PDFs found");
            return;
        }

        _logger.LogInformation("[StalePdfRecovery] Found {Count} stale PDF(s) to recover", stalePdfs.Count);

        var (recovered, failed, stillStuck) = await RecoverAllAsync(stalePdfs, stoppingToken).ConfigureAwait(false);

        _logger.LogInformation(
            "[StalePdfRecovery] Recovery complete: {Recovered} recovered, {Failed} failed, {StillStuck} still stuck, {Total} total",
            recovered, failed, stillStuck, stalePdfs.Count);
    }

    /// <summary>
    /// Exposed as internal for unit testing: runs the full recovery loop and
    /// returns honest outcome counts re-read from the DB after each ProcessAsync call.
    /// </summary>
    internal async Task<(int recovered, int failed, int stillStuck)> RecoverAllAsync(CancellationToken stoppingToken)
    {
        var stalePdfs = await FindStalePdfsAsync(stoppingToken).ConfigureAwait(false);
        return await RecoverAllAsync(stalePdfs, stoppingToken).ConfigureAwait(false);
    }

    private async Task<(int recovered, int failed, int stillStuck)> RecoverAllAsync(
        IReadOnlyList<StalePdfInfo> stalePdfs,
        CancellationToken stoppingToken)
    {
        var recovered = 0;
        var failed = 0;
        var stillStuck = 0;

        foreach (var pdf in stalePdfs)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("[StalePdfRecovery] Cancellation requested, stopping recovery");
                break;
            }

            try
            {
                _logger.LogInformation(
                    "[StalePdfRecovery] Recovering PDF {PdfId} (was {Status}, uploaded {UploadedAt})",
                    pdf.Id, pdf.OriginalStatus, pdf.UploadedAt);

                // Reset to "pending" before reprocessing (so pipeline picks it up)
                await ResetToPendingAsync(pdf.Id, stoppingToken).ConfigureAwait(false);

                // Create a fresh scope for each PDF
                using var scope = _scopeFactory.CreateScope();
                var pipeline = scope.ServiceProvider.GetRequiredService<IPdfProcessingPipelineService>();

                await pipeline.ProcessAsync(pdf.Id, pdf.FilePath, pdf.UploadedByUserId, stoppingToken)
                    .ConfigureAwait(false);

                // Re-read state from DB: ProcessAsync may have returned without reaching Ready
                // (e.g. claim-skip or concurrency abort). Counting unconditionally was dishonest.
                var finalState = await ReadProcessingStateAsync(pdf.Id, stoppingToken).ConfigureAwait(false);

                if (string.Equals(finalState, nameof(PdfProcessingState.Ready), StringComparison.Ordinal))
                {
                    recovered++;
                    _logger.LogInformation("[StalePdfRecovery] Recovered PDF {PdfId} → Ready", pdf.Id);
                }
                else if (string.Equals(finalState, nameof(PdfProcessingState.Failed), StringComparison.Ordinal))
                {
                    failed++;
                    _logger.LogWarning(
                        "[StalePdfRecovery] PDF {PdfId} ended Failed after reprocessing (will be retried by RetryFailedPdfsJob)",
                        pdf.Id);
                }
                else
                {
                    stillStuck++;
                    _logger.LogWarning(
                        "[StalePdfRecovery] PDF {PdfId} did NOT progress (state={State}); recovery ineffective",
                        pdf.Id, finalState);
                }
            }
#pragma warning disable CA1031 // Non-blocking: log and continue to next PDF
            catch (OperationCanceledException ex) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation(ex, "[StalePdfRecovery] Cancelled during recovery of {PdfId}", pdf.Id);
                break;
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogError(ex, "[StalePdfRecovery] Failed to recover PDF {PdfId}", pdf.Id);
            }
#pragma warning restore CA1031
        }

        return (recovered, failed, stillStuck);
    }

    private async Task<string?> ReadProcessingStateAsync(Guid pdfDocumentId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.PdfDocuments
            .Where(p => p.Id == pdfDocumentId)
            .Select(p => p.ProcessingState)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }

    private async Task<List<StalePdfInfo>> FindStalePdfsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var now = DateTime.UtcNow;
        var pendingCutoff = now - PendingStaleness;
        var processingCutoff = now - ProcessingStaleness;

        // Use ProcessingState enum (stored as string) instead of deprecated ProcessingStatus
        var pendingState = nameof(PdfProcessingState.Pending);
        var uploadingState = nameof(PdfProcessingState.Uploading);
        var extractingState = nameof(PdfProcessingState.Extracting);
        var chunkingState = nameof(PdfProcessingState.Chunking);
        var embeddingState = nameof(PdfProcessingState.Embedding);
        var indexingState = nameof(PdfProcessingState.Indexing);

        return await db.PdfDocuments
            .Where(p =>
                (p.ProcessingState == pendingState && p.UploadedAt < pendingCutoff) ||
                ((p.ProcessingState == uploadingState ||
                  p.ProcessingState == extractingState ||
                  p.ProcessingState == chunkingState ||
                  p.ProcessingState == embeddingState ||
                  p.ProcessingState == indexingState) && p.UploadedAt < processingCutoff))
            .Select(p => new StalePdfInfo
            {
                Id = p.Id,
                FilePath = p.FilePath,
                UploadedByUserId = p.UploadedByUserId,
                OriginalStatus = p.ProcessingState,
                UploadedAt = p.UploadedAt
            })
            .OrderBy(p => p.UploadedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task ResetToPendingAsync(Guid pdfDocumentId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var pdfDoc = await db.PdfDocuments
            .FindAsync(new object[] { pdfDocumentId }, cancellationToken)
            .ConfigureAwait(false);

        var readyState = nameof(PdfProcessingState.Ready);
        if (pdfDoc != null && !string.Equals(pdfDoc.ProcessingState, readyState, StringComparison.Ordinal))
        {
            pdfDoc.ProcessingState = nameof(PdfProcessingState.Pending);
            pdfDoc.ProcessingError = null;
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class StalePdfInfo
    {
        public Guid Id { get; init; }
        public string FilePath { get; init; } = default!;
        public Guid UploadedByUserId { get; init; }
        public string OriginalStatus { get; init; } = default!;
        public DateTime UploadedAt { get; init; }
    }
}
