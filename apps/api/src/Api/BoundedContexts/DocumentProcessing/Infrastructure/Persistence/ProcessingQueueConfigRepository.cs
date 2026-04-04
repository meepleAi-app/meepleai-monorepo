using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Api.Infrastructure.Entities.DocumentProcessing;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;

/// <summary>
/// Repository implementation for the singleton ProcessingQueueConfig.
/// Auto-creates default config if none exists.
/// Issue #5455: Queue configuration management.
/// </summary>
internal class ProcessingQueueConfigRepository : RepositoryBase, IProcessingQueueConfigRepository
{

    public ProcessingQueueConfigRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<ProcessingQueueConfig> GetOrCreateAsync(CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.ProcessingQueueConfigs
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

        await DbContext.ProcessingQueueConfigs.AddAsync(newEntity, cancellationToken).ConfigureAwait(false);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return defaultConfig;
    }

    public async Task UpdateAsync(ProcessingQueueConfig config, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.ProcessingQueueConfigs
            .AsTracking()
            .FirstOrDefaultAsync(e => e.Id == config.Id, cancellationToken)
            .ConfigureAwait(false);

        if (entity == null)
            throw new InvalidOperationException("ProcessingQueueConfig not found.");

        entity.IsPaused = config.IsPaused;
        entity.MaxConcurrentWorkers = config.MaxConcurrentWorkers;
        entity.UpdatedAt = config.UpdatedAt;
        entity.UpdatedBy = config.UpdatedBy;

        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
