using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.DocumentProcessing;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;

/// <summary>
/// Repository implementation for the singleton ProcessingQueueConfig.
/// Auto-creates default config if none exists.
/// Issue #5455: Queue configuration management.
/// </summary>
internal class ProcessingQueueConfigRepository : IProcessingQueueConfigRepository
{
    private readonly MeepleAiDbContext _dbContext;

    public ProcessingQueueConfigRepository(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ProcessingQueueConfig> GetOrCreateAsync(CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.ProcessingQueueConfigs
            .AsTracking()
            .FirstOrDefaultAsync(e => e.Id == ProcessingQueueConfig.SingletonId, cancellationToken)
            .ConfigureAwait(false);

        if (entity != null)
        {
            return ProcessingQueueConfig.Reconstitute(
                entity.Id,
                entity.IsPaused,
                entity.MaxConcurrentWorkers,
                entity.UpdatedAt,
                entity.UpdatedBy);
        }

        // Create default config
        var defaultConfig = ProcessingQueueConfig.CreateDefault();
        var newEntity = new ProcessingQueueConfigEntity
        {
            Id = ProcessingQueueConfig.SingletonId,
            IsPaused = defaultConfig.IsPaused,
            MaxConcurrentWorkers = defaultConfig.MaxConcurrentWorkers,
            UpdatedAt = defaultConfig.UpdatedAt,
            UpdatedBy = defaultConfig.UpdatedBy
        };

        await _dbContext.ProcessingQueueConfigs.AddAsync(newEntity, cancellationToken).ConfigureAwait(false);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return defaultConfig;
    }

    public async Task UpdateAsync(ProcessingQueueConfig config, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.ProcessingQueueConfigs
            .AsTracking()
            .FirstOrDefaultAsync(e => e.Id == config.Id, cancellationToken)
            .ConfigureAwait(false);

        if (entity == null)
            throw new InvalidOperationException("ProcessingQueueConfig not found.");

        entity.IsPaused = config.IsPaused;
        entity.MaxConcurrentWorkers = config.MaxConcurrentWorkers;
        entity.UpdatedAt = config.UpdatedAt;
        entity.UpdatedBy = config.UpdatedBy;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
