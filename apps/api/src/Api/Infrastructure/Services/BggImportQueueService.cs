using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Infrastructure.Services;

/// <summary>
/// Service for managing the BGG import queue with PostgreSQL persistence.
/// Issue #3541: BGG Import Queue Service
///
/// IMPORTANT: This service uses .AsTracking() on all mutation queries because
/// the DbContext defaults to QueryTrackingBehavior.NoTracking (PERF-06).
/// Without explicit tracking, SaveChangesAsync silently skips untracked entity changes.
/// </summary>
internal sealed class BggImportQueueService : IBggImportQueueService
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<BggImportQueueService> _logger;
    private readonly TimeProvider _timeProvider;

    public BggImportQueueService(
        MeepleAiDbContext dbContext,
        ILogger<BggImportQueueService> logger,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<BggImportQueueEntity> EnqueueAsync(
        int bggId,
        string? gameName = null,
        Guid? requestedByUserId = null,
        CancellationToken cancellationToken = default)
    {
        // Check if already queued or imported
        var exists = await _dbContext.BggImportQueue
            .AnyAsync(q => q.BggId == bggId && q.Status != BggImportStatus.Failed, cancellationToken)
            .ConfigureAwait(false);

        if (exists)
        {
            // Issue #3543 - Fix #2: Use ConflictException instead of InvalidOperationException
            throw new Api.Middleware.Exceptions.ConflictException($"BGG ID {bggId} is already queued or imported");
        }

        // Get next position (max position + 1)
        var maxPosition = await _dbContext.BggImportQueue
            .Where(q => q.Status == BggImportStatus.Queued)
            .MaxAsync(q => (int?)q.Position, cancellationToken)
            .ConfigureAwait(false);

        var nextPosition = (maxPosition ?? 0) + 1;

        var entity = new BggImportQueueEntity
        {
            Id = Guid.NewGuid(),
            BggId = bggId,
            GameName = gameName,
            Status = BggImportStatus.Queued,
            Position = nextPosition,
            RetryCount = 0,
            RequestedByUserId = requestedByUserId,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        };

        await _dbContext.BggImportQueue.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Enqueued BGG import: BggId={BggId}, Position={Position}",
            bggId, nextPosition);

        return entity;
    }

    public async Task<List<BggImportQueueEntity>> EnqueueBatchAsync(
        IEnumerable<int> bggIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bggIds);

        var bggIdList = bggIds.Distinct().ToList();
        if (bggIdList.Count == 0)
            return [];

        // Find already queued/imported IDs
        var existingBggIds = await _dbContext.BggImportQueue
            .Where(q => q.BggId.HasValue && bggIdList.Contains(q.BggId.Value) && q.Status != BggImportStatus.Failed)
            .Select(q => q.BggId!.Value)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Filter to only new IDs
        var newBggIds = bggIdList.Except(existingBggIds).ToList();
        if (newBggIds.Count == 0)
        {
            _logger.LogWarning(
                "Batch enqueue: All {Count} BGG IDs already queued/imported",
                bggIdList.Count);
            return [];
        }

        // Get next position
        var maxPosition = await _dbContext.BggImportQueue
            .Where(q => q.Status == BggImportStatus.Queued)
            .MaxAsync(q => (int?)q.Position, cancellationToken)
            .ConfigureAwait(false);

        var nextPosition = (maxPosition ?? 0) + 1;
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var entities = new List<BggImportQueueEntity>();
        foreach (var bggId in newBggIds)
        {
            var entity = new BggImportQueueEntity
            {
                Id = Guid.NewGuid(),
                BggId = bggId,
                Status = BggImportStatus.Queued,
                Position = nextPosition++,
                RetryCount = 0,
                CreatedAt = now
            };
            entities.Add(entity);
        }

        await _dbContext.BggImportQueue.AddRangeAsync(entities, cancellationToken).ConfigureAwait(false);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Batch enqueued {NewCount} BGG imports (skipped {SkippedCount} duplicates)",
            newBggIds.Count, existingBggIds.Count);

        return entities;
    }

    public async Task<List<BggImportQueueEntity>> GetQueueStatusAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.BggImportQueue
            .Where(q => q.Status == BggImportStatus.Queued || q.Status == BggImportStatus.Processing)
            .OrderBy(q => q.Position)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<BggImportQueueEntity>> GetAllQueueItemsAsync(
        CancellationToken cancellationToken = default)
    {
        // Issue #3543 - Fix #3: Get all statuses for SSE streaming
        return await _dbContext.BggImportQueue
            .OrderByDescending(q => q.CreatedAt)
            .Take(500)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<BggImportQueueEntity?> GetByBggIdAsync(
        int bggId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.BggImportQueue
            .FirstOrDefaultAsync(q => q.BggId == bggId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> CancelAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.BggImportQueue
            .AsTracking()
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entity == null || entity.Status != BggImportStatus.Queued)
            return false;

        _dbContext.BggImportQueue.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Recalculate positions after removal
        await RecalculatePositionsAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Cancelled BGG import queue entry: Id={Id}, BggId={BggId}",
            id, entity.BggId);

        return true;
    }

    public async Task<bool> RetryFailedAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.BggImportQueue
            .AsTracking()
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entity == null || entity.Status != BggImportStatus.Failed)
            return false;

        // Get next position
        var maxPosition = await _dbContext.BggImportQueue
            .Where(q => q.Status == BggImportStatus.Queued)
            .MaxAsync(q => (int?)q.Position, cancellationToken)
            .ConfigureAwait(false);

        entity.Status = BggImportStatus.Queued;
        entity.Position = (maxPosition ?? 0) + 1;
        entity.RetryCount = 0;
        entity.ErrorMessage = null;
        entity.ProcessedAt = null;
        entity.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Retry failed BGG import: Id={Id}, BggId={BggId}, NewPosition={Position}",
            id, entity.BggId, entity.Position);

        return true;
    }

    public async Task<int> CleanupOldJobsAsync(
        int retentionDays,
        CancellationToken cancellationToken = default)
    {
        if (retentionDays <= 0)
            return 0;

        var cutoffDate = _timeProvider.GetUtcNow().UtcDateTime.AddDays(-retentionDays);

        var oldJobs = await _dbContext.BggImportQueue
            .AsTracking()
            .Where(q =>
                (q.Status == BggImportStatus.Completed || q.Status == BggImportStatus.Failed) &&
                q.ProcessedAt.HasValue &&
                q.ProcessedAt.Value < cutoffDate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (oldJobs.Count == 0)
            return 0;

        _dbContext.BggImportQueue.RemoveRange(oldJobs);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Cleaned up {Count} old BGG import jobs older than {Days} days",
            oldJobs.Count, retentionDays);

        return oldJobs.Count;
    }

    public async Task<BggImportQueueEntity?> GetNextQueuedItemAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.BggImportQueue
            .AsTracking()
            .Where(q => q.Status == BggImportStatus.Queued)
            .OrderBy(q => q.Position)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task MarkAsProcessingAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.BggImportQueue
            .AsTracking()
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entity == null)
            throw new InvalidOperationException($"Queue entry {id} not found");

        entity.Status = BggImportStatus.Processing;
        entity.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogDebug(
            "Marked BGG import as processing: Id={Id}, BggId={BggId}",
            id, entity.BggId);
    }

    public async Task MarkAsCompletedAsync(
        Guid id,
        Guid createdGameId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.BggImportQueue
            .AsTracking()
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entity == null)
            throw new InvalidOperationException($"Queue entry {id} not found");

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        entity.Status = BggImportStatus.Completed;
        entity.CreatedGameId = createdGameId;
        entity.ProcessedAt = now;
        entity.UpdatedAt = now;
        entity.ErrorMessage = null;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Recalculate positions after completion
        await RecalculatePositionsAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Marked BGG import as completed: Id={Id}, BggId={BggId}, CreatedGameId={CreatedGameId}",
            id, entity.BggId, createdGameId);
    }

    public async Task MarkAsFailedAsync(
        Guid id,
        string errorMessage,
        int maxRetries,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.BggImportQueue
            .AsTracking()
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entity == null)
            throw new InvalidOperationException($"Queue entry {id} not found");

        entity.RetryCount++;
        entity.ErrorMessage = errorMessage;
        entity.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;

        if (entity.RetryCount >= maxRetries)
        {
            // Max retries reached - mark as permanently failed
            entity.Status = BggImportStatus.Failed;
            entity.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;

            _logger.LogWarning(
                "BGG import failed permanently after {RetryCount} attempts: Id={Id}, BggId={BggId}, Error={Error}",
                entity.RetryCount, id, entity.BggId, errorMessage);
        }
        else
        {
            // Retry available - change back to Queued
            entity.Status = BggImportStatus.Queued;

            _logger.LogWarning(
                "BGG import failed (retry {RetryCount}/{MaxRetries}): Id={Id}, BggId={BggId}, Error={Error}",
                entity.RetryCount, maxRetries, id, entity.BggId, errorMessage);
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Recalculate positions if failed permanently
        if (entity.Status == BggImportStatus.Failed)
        {
            await RecalculatePositionsAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task RecalculatePositionsAsync(
        CancellationToken cancellationToken = default)
    {
        var queuedItems = await _dbContext.BggImportQueue
            .AsTracking()
            .Where(q => q.Status == BggImportStatus.Queued)
            .OrderBy(q => q.Position)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (queuedItems.Count == 0)
            return;

        // Reassign positions sequentially (1, 2, 3, ...)
        for (int i = 0; i < queuedItems.Count; i++)
        {
            queuedItems[i].Position = i + 1;
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogDebug(
            "Recalculated queue positions: {Count} items",
            queuedItems.Count);
    }

    public async Task<BggImportQueueEntity> EnqueueEnrichmentAsync(
        Guid sharedGameId,
        int? bggId,
        string gameName,
        Guid requestedByUserId,
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        var maxPosition = await _dbContext.BggImportQueue
            .Where(q => q.Status == BggImportStatus.Queued)
            .MaxAsync(q => (int?)q.Position, cancellationToken)
            .ConfigureAwait(false);

        var entity = new BggImportQueueEntity
        {
            Id = Guid.NewGuid(),
            JobType = BggQueueJobType.Enrichment,
            BggId = bggId,
            GameName = gameName,
            SharedGameId = sharedGameId,
            BatchId = batchId,
            Status = BggImportStatus.Queued,
            Position = (maxPosition ?? 0) + 1,
            RetryCount = 0,
            RequestedByUserId = requestedByUserId,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        };

        _dbContext.BggImportQueue.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Enqueued BGG enrichment: SharedGameId={SharedGameId}, BggId={BggId}, BatchId={BatchId}",
            sharedGameId, bggId, batchId);

        return entity;
    }

    public async Task<(Guid BatchId, int Enqueued)> EnqueueEnrichmentBatchAsync(
        IEnumerable<(Guid SharedGameId, int? BggId, string GameName)> items,
        Guid requestedByUserId,
        CancellationToken cancellationToken = default)
    {
        var batchId = Guid.NewGuid();
        var itemList = items.ToList();
        if (itemList.Count == 0)
            return (batchId, 0);

        var maxPosition = await _dbContext.BggImportQueue
            .Where(q => q.Status == BggImportStatus.Queued)
            .MaxAsync(q => (int?)q.Position, cancellationToken)
            .ConfigureAwait(false);

        var nextPosition = (maxPosition ?? 0) + 1;
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        foreach (var item in itemList)
        {
            var entity = new BggImportQueueEntity
            {
                Id = Guid.NewGuid(),
                JobType = BggQueueJobType.Enrichment,
                BggId = item.BggId,
                GameName = item.GameName,
                SharedGameId = item.SharedGameId,
                BatchId = batchId,
                Status = BggImportStatus.Queued,
                Position = nextPosition++,
                RetryCount = 0,
                RequestedByUserId = requestedByUserId,
                CreatedAt = now
            };
            _dbContext.BggImportQueue.Add(entity);
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Enqueued BGG enrichment batch: BatchId={BatchId}, Count={Count}",
            batchId, itemList.Count);

        return (batchId, itemList.Count);
    }

    public async Task<bool> TryClaimItemAsync(
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var affected = await _dbContext.Database.ExecuteSqlRawAsync(
            """
            UPDATE "BggImportQueue"
            SET "Status" = 1, "UpdatedAt" = {0}
            WHERE "Id" = {1} AND "Status" = 0
            """,
            [now, itemId],
            cancellationToken).ConfigureAwait(false);

        return affected > 0;
    }

    public async Task<int> RecoverStaleItemsAsync(
        TimeSpan staleThreshold,
        CancellationToken cancellationToken = default)
    {
        var threshold = _timeProvider.GetUtcNow().UtcDateTime - staleThreshold;
        var affected = await _dbContext.Database.ExecuteSqlRawAsync(
            """
            UPDATE "BggImportQueue"
            SET "Status" = 0, "RetryCount" = "RetryCount" + 1, "UpdatedAt" = {0}
            WHERE "Status" = 1 AND "UpdatedAt" < {1}
            """,
            [_timeProvider.GetUtcNow().UtcDateTime, threshold],
            cancellationToken).ConfigureAwait(false);

        return affected;
    }
}
