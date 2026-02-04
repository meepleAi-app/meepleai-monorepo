using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.Infrastructure.Services;
using Api.Models;
using MediatR;
using Microsoft.Extensions.Options;

namespace Api.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that processes the BGG import queue at 1 request/second.
/// Issue #3541: BGG Import Queue Service with rate limiting and retry logic.
///
/// Features:
/// - Singleton global queue (all users share)
/// - 1 request/second rate limiting (BGG API constraint)
/// - Exponential backoff retry logic (max 3 attempts)
/// - Auto-cleanup of old completed/failed jobs
/// - Comprehensive logging and error handling
/// </summary>
internal sealed class BggImportQueueBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BggImportQueueBackgroundService> _logger;
    private readonly BggImportQueueConfiguration _config;

    public BggImportQueueBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<BggImportQueueBackgroundService> logger,
        IOptions<BggImportQueueConfiguration> config)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        _scopeFactory = scopeFactory;
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        ArgumentNullException.ThrowIfNull(config);
        _config = config.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Check if service is enabled
        if (!_config.Enabled)
        {
            _logger.LogInformation(
                "BGG import queue service is disabled via configuration. Set BggImportQueue:Enabled=true to enable.");
            return;
        }

        // Validate configuration
        if (_config.ProcessingIntervalSeconds <= 0)
        {
            _logger.LogWarning(
                "BGG import queue processing interval is {Seconds} seconds (invalid). Service is disabled.",
                _config.ProcessingIntervalSeconds);
            return;
        }

        _logger.LogInformation(
            "BGG import queue service started. Processing interval: {Seconds}s, Max retries: {MaxRetries}",
            _config.ProcessingIntervalSeconds,
            _config.MaxRetryAttempts);

        // Wait before first run to allow application to fully start
        var initialDelay = TimeSpan.FromMinutes(Math.Max(0, _config.InitialDelayMinutes));
        if (initialDelay > TimeSpan.Zero)
        {
            _logger.LogInformation("Waiting {Minutes} minutes before starting queue processing", _config.InitialDelayMinutes);
            await Task.Delay(initialDelay, stoppingToken).ConfigureAwait(false);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessNextQueueItemAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation(ex, "BGG import queue processing cancelled (application shutting down)");
                break;
            }
#pragma warning disable CA1031 // Do not catch general exception types
            // BACKGROUND SERVICE: Background service boundary - prevents service crash
            // Generic catch prevents service from crashing host process
            // Processing failure logged but service continues queue processing
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing BGG import queue item");
                // Continue to next iteration despite error
            }
#pragma warning restore CA1031

            // Rate limiting: Wait for configured interval (default 1 second for BGG API)
            var interval = TimeSpan.FromSeconds(_config.ProcessingIntervalSeconds);
            await Task.Delay(interval, stoppingToken).ConfigureAwait(false);
        }

        _logger.LogInformation("BGG import queue service stopped");
    }

    /// <summary>
    /// Process the next queued item (lowest position).
    /// </summary>
    private async Task ProcessNextQueueItemAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var queueService = scope.ServiceProvider.GetRequiredService<IBggImportQueueService>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // Get next queued item
        var queueItem = await queueService.GetNextQueuedItemAsync(cancellationToken).ConfigureAwait(false);
        if (queueItem == null)
        {
            // Queue is empty - check for cleanup
            if (_config.AutoCleanupDays > 0)
            {
                await PerformCleanupAsync(queueService, cancellationToken).ConfigureAwait(false);
            }
            return;
        }

        _logger.LogInformation(
            "Processing BGG import: Id={Id}, BggId={BggId}, Position={Position}, Attempt={Attempt}",
            queueItem.Id, queueItem.BggId, queueItem.Position, queueItem.RetryCount + 1);

        try
        {
            // Mark as processing
            await queueService.MarkAsProcessingAsync(queueItem.Id, cancellationToken).ConfigureAwait(false);

            // Execute import via CQRS command
            // Note: Background queue imports use system user ID (Guid.Empty) since they are global operations
            var command = new ImportGameFromBggCommand(queueItem.BggId, Guid.Empty);
            var createdGameId = await mediator.Send(command, cancellationToken).ConfigureAwait(false);

            // Mark as completed
            await queueService.MarkAsCompletedAsync(queueItem.Id, createdGameId, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Successfully imported BGG game: QueueId={QueueId}, BggId={BggId}, CreatedGameId={CreatedGameId}",
                queueItem.Id, queueItem.BggId, createdGameId);
        }
        catch (Exception ex)
        {
            // Mark as failed (handles retry logic internally)
            var errorMessage = $"{ex.GetType().Name}: {ex.Message}";
            await queueService.MarkAsFailedAsync(
                queueItem.Id,
                errorMessage,
                _config.MaxRetryAttempts,
                cancellationToken).ConfigureAwait(false);

            _logger.LogError(
                ex,
                "Failed to import BGG game: QueueId={QueueId}, BggId={BggId}, Attempt={Attempt}/{MaxAttempts}",
                queueItem.Id, queueItem.BggId, queueItem.RetryCount + 1, _config.MaxRetryAttempts);

            // Exponential backoff delay if retry will occur
            if (queueItem.RetryCount + 1 < _config.MaxRetryAttempts)
            {
                var backoffDelay = TimeSpan.FromSeconds(
                    _config.BaseRetryDelaySeconds * Math.Pow(2, queueItem.RetryCount));

                _logger.LogInformation(
                    "Retry scheduled with exponential backoff: {Delay} seconds",
                    backoffDelay.TotalSeconds);

                await Task.Delay(backoffDelay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Perform cleanup of old completed/failed jobs (runs when queue is empty).
    /// </summary>
    private async Task PerformCleanupAsync(
        IBggImportQueueService queueService,
        CancellationToken cancellationToken)
    {
        try
        {
            var deletedCount = await queueService.CleanupOldJobsAsync(
                _config.AutoCleanupDays,
                cancellationToken).ConfigureAwait(false);

            if (deletedCount > 0)
            {
                _logger.LogInformation(
                    "Auto-cleanup removed {Count} old BGG import jobs",
                    deletedCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during auto-cleanup of old BGG import jobs");
        }
    }
}
