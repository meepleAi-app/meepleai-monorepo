using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Services;

/// <summary>
/// Background service that cleans up session photo attachments after retention period.
/// Runs daily, deletes S3 blobs and soft-deletes DB records older than configured days.
/// Issue #5366 - SessionAttachmentCleanupJob.
/// </summary>
internal sealed class SessionAttachmentCleanupJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SessionAttachmentCleanupJob> _logger;

    private int RetentionDays =>
        _configuration.GetValue("SessionAttachments:RetentionDays", 90);

    private int BatchSize =>
        _configuration.GetValue("SessionAttachments:CleanupBatchSize", 100);

    public SessionAttachmentCleanupJob(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<SessionAttachmentCleanupJob> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "SessionAttachmentCleanupJob started. Retention: {RetentionDays} days, Batch: {BatchSize}",
            RetentionDays, BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            var nextRun = CalculateNextRun();
            var delay = nextRun - DateTime.UtcNow;

            if (delay > TimeSpan.Zero)
            {
                _logger.LogDebug("Next cleanup run at {NextRun} UTC", nextRun);
                await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
            }

            try
            {
                await RunCleanupAsync(stoppingToken).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Background service must not crash
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SessionAttachmentCleanupJob failed. Will retry next cycle.");
            }
#pragma warning restore CA1031
        }
    }

    internal async Task RunCleanupAsync(CancellationToken cancellationToken)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-RetentionDays);
        var totalCleaned = 0;
        var s3Errors = 0;
        var sessionIds = new HashSet<Guid>();

        _logger.LogInformation("Starting cleanup for attachments older than {CutoffDate}", cutoffDate);

        while (!cancellationToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ISessionAttachmentRepository>();
            var attachmentService = scope.ServiceProvider.GetRequiredService<ISessionAttachmentService>();

            var batch = await repository.GetExpiredAttachmentsAsync(
                cutoffDate, BatchSize, cancellationToken).ConfigureAwait(false);

            if (batch.Count == 0)
                break;

            foreach (var attachment in batch)
            {
                // Delete S3 blobs (non-blocking — failures retried next cycle)
                try
                {
                    await attachmentService.DeleteBlobsAsync(
                        attachment.BlobUrl, attachment.ThumbnailUrl, cancellationToken).ConfigureAwait(false);
                }
#pragma warning disable CA1031 // S3 failure is non-critical for cleanup
                catch (Exception ex)
                {
                    s3Errors++;
                    _logger.LogWarning(ex,
                        "Failed to delete S3 blobs for attachment {AttachmentId}. Will retry next cycle.",
                        attachment.Id);
                }
#pragma warning restore CA1031

                // Soft delete in DB regardless of S3 result
                await repository.SoftDeleteAsync(attachment.Id, cancellationToken).ConfigureAwait(false);
                sessionIds.Add(attachment.SessionId);
                totalCleaned++;
            }

            await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        _logger.LogInformation(
            "Cleanup complete: {TotalCleaned} attachments cleaned for {SessionCount} sessions. S3 errors: {S3Errors}",
            totalCleaned, sessionIds.Count, s3Errors);
    }

    private static DateTime CalculateNextRun()
    {
        // Next 3:00 AM UTC
        var now = DateTime.UtcNow;
        var today3Am = now.Date.AddHours(3);
        return now < today3Am ? today3Am : today3Am.AddDays(1);
    }
}
