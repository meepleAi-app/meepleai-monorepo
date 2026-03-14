using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Services;
using Api.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
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
    /// Uses separate DI scopes for queue operations vs MediatR command execution
    /// to prevent the handler's DbContext mutations from polluting queue state.
    /// </summary>
    private async Task ProcessNextQueueItemAsync(CancellationToken cancellationToken)
    {
        // Scope 1: Read next queued item (isolated from handler's DbContext)
        Guid queueItemId;
        int? bggId;
        int retryCount;
        Guid? requestedByUserId;
        BggQueueJobType jobType;
        Guid? sharedGameId;
        {
            using var readScope = _scopeFactory.CreateScope();
            var readQueueService = readScope.ServiceProvider.GetRequiredService<IBggImportQueueService>();

            var queueItem = await readQueueService.GetNextQueuedItemAsync(cancellationToken).ConfigureAwait(false);
            if (queueItem == null)
            {
                if (_config.AutoCleanupDays > 0)
                {
                    await PerformCleanupAsync(readQueueService, cancellationToken).ConfigureAwait(false);
                }
                return;
            }

            queueItemId = queueItem.Id;
            bggId = queueItem.BggId;
            retryCount = queueItem.RetryCount;
            requestedByUserId = queueItem.RequestedByUserId;
            jobType = queueItem.JobType;
            sharedGameId = queueItem.SharedGameId;

            _logger.LogInformation(
                "Processing BGG import: Id={Id}, BggId={BggId}, Position={Position}, Attempt={Attempt}",
                queueItem.Id, queueItem.BggId, queueItem.Position, retryCount + 1);

            // Mark as processing in the same scope that loaded it
            await readQueueService.MarkAsProcessingAsync(queueItemId, cancellationToken).ConfigureAwait(false);
        }

        // Scope 2: Execute MediatR command (separate DbContext)
        Guid? createdGameId = null;
        Exception? importException = null;
        try
        {
            using var commandScope = _scopeFactory.CreateScope();
            var mediator = commandScope.ServiceProvider.GetRequiredService<IMediator>();

            var userId = requestedByUserId ?? Guid.Empty;

            if (jobType == BggQueueJobType.Import && bggId.HasValue)
            {
                var command = new ImportGameFromBggCommand(bggId.Value, userId);
                createdGameId = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
            }
            else if (jobType == BggQueueJobType.Enrichment)
            {
                // Enrichment processing will be implemented in Chunk 4
                _logger.LogWarning(
                    "Enrichment job type not yet implemented: QueueId={QueueId}, SharedGameId={SharedGameId}",
                    queueItemId, sharedGameId);
            }
        }
        catch (Exception ex)
        {
            importException = ex;
        }

        // Scope 3: Update queue status (fresh DbContext, no pollution from handler)
        {
            using var updateScope = _scopeFactory.CreateScope();
            var updateQueueService = updateScope.ServiceProvider.GetRequiredService<IBggImportQueueService>();

            if (importException == null && createdGameId.HasValue)
            {
                // Success path
                await updateQueueService.MarkAsCompletedAsync(queueItemId, createdGameId.Value, cancellationToken).ConfigureAwait(false);

                _logger.LogInformation(
                    "Successfully imported BGG game: QueueId={QueueId}, BggId={BggId}, CreatedGameId={CreatedGameId}",
                    queueItemId, bggId, createdGameId.Value);
            }
            else if (importException is InvalidOperationException { Message: var msg } && msg.Contains("already exists"))
            {
                // Game was already imported — look up existing and mark completed
                var updateDbContext = updateScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
                var existingGame = await updateDbContext.Set<Api.Infrastructure.Entities.SharedGameCatalog.SharedGameEntity>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(g => g.BggId == bggId, cancellationToken)
                    .ConfigureAwait(false);

                var existingGameId = existingGame?.Id ?? Guid.Empty;
                await updateQueueService.MarkAsCompletedAsync(queueItemId, existingGameId, cancellationToken).ConfigureAwait(false);

                _logger.LogInformation(
                    importException,
                    "BGG game already exists, marked as completed: QueueId={QueueId}, BggId={BggId}, ExistingGameId={GameId}",
                    queueItemId, bggId, existingGameId);
            }
            else if (importException != null)
            {
                // General failure — mark as failed with retry logic
                var errorMessage = $"{importException.GetType().Name}: {importException.Message}";
                await updateQueueService.MarkAsFailedAsync(
                    queueItemId,
                    errorMessage,
                    _config.MaxRetryAttempts,
                    cancellationToken).ConfigureAwait(false);

                _logger.LogError(
                    importException,
                    "Failed to import BGG game: QueueId={QueueId}, BggId={BggId}, Attempt={Attempt}/{MaxAttempts}",
                    queueItemId, bggId, retryCount + 1, _config.MaxRetryAttempts);

                if (retryCount + 1 < _config.MaxRetryAttempts)
                {
                    var backoffDelay = TimeSpan.FromSeconds(
                        _config.BaseRetryDelaySeconds * Math.Pow(2, retryCount));

                    _logger.LogInformation(
                        "Retry scheduled with exponential backoff: {Delay} seconds",
                        backoffDelay.TotalSeconds);

                    await Task.Delay(backoffDelay, cancellationToken).ConfigureAwait(false);
                }
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
