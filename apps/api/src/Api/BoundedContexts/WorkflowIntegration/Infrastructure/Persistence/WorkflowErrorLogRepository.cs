using Api.BoundedContexts.WorkflowIntegration.Domain.Entities;
using Api.BoundedContexts.WorkflowIntegration.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.WorkflowIntegration.Infrastructure.Persistence;

public class WorkflowErrorLogRepository : RepositoryBase, IWorkflowErrorLogRepository
{
    public WorkflowErrorLogRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<WorkflowErrorLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<Api.Infrastructure.Entities.WorkflowErrorLogEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<List<WorkflowErrorLog>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<Api.Infrastructure.Entities.WorkflowErrorLogEntity>()
            .AsNoTracking()
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<WorkflowErrorLog>> FindByWorkflowIdAsync(string workflowId, CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<Api.Infrastructure.Entities.WorkflowErrorLogEntity>()
            .AsNoTracking()
            .Where(e => e.WorkflowId == workflowId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<WorkflowErrorLog?> FindByExecutionIdAsync(string executionId, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<Api.Infrastructure.Entities.WorkflowErrorLogEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.ExecutionId == executionId, cancellationToken).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task AddAsync(WorkflowErrorLog errorLog, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(errorLog);
        var entity = MapToPersistence(errorLog);
        await DbContext.Set<Api.Infrastructure.Entities.WorkflowErrorLogEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(WorkflowErrorLog errorLog, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(errorLog);
        var entity = MapToPersistence(errorLog);
        DbContext.Set<Api.Infrastructure.Entities.WorkflowErrorLogEntity>().Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(WorkflowErrorLog errorLog, CancellationToken cancellationToken = default)
    {
        var entity = MapToPersistence(errorLog);
        DbContext.Set<Api.Infrastructure.Entities.WorkflowErrorLogEntity>().Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<Api.Infrastructure.Entities.WorkflowErrorLogEntity>()
            .AnyAsync(e => e.Id == id, cancellationToken).ConfigureAwait(false);
    }

    private static WorkflowErrorLog MapToDomain(Api.Infrastructure.Entities.WorkflowErrorLogEntity entity)
    {
        var errorLog = new WorkflowErrorLog(
            id: entity.Id,
            workflowId: entity.WorkflowId,
            executionId: entity.ExecutionId,
            errorMessage: entity.ErrorMessage,
            nodeName: entity.NodeName,
            stackTrace: entity.StackTrace
        );

        // Override RetryCount and CreatedAt from DB
        var retryCountProp = typeof(WorkflowErrorLog).GetProperty("RetryCount");
        retryCountProp?.SetValue(errorLog, entity.RetryCount);

        var createdAtProp = typeof(WorkflowErrorLog).GetProperty("CreatedAt");
        createdAtProp?.SetValue(errorLog, entity.CreatedAt);

        return errorLog;
    }

    private static Api.Infrastructure.Entities.WorkflowErrorLogEntity MapToPersistence(WorkflowErrorLog domain)
    {
        return new Api.Infrastructure.Entities.WorkflowErrorLogEntity
        {
            Id = domain.Id,
            WorkflowId = domain.WorkflowId,
            ExecutionId = domain.ExecutionId,
            ErrorMessage = domain.ErrorMessage,
            NodeName = domain.NodeName,
            RetryCount = domain.RetryCount,
            StackTrace = domain.StackTrace,
            CreatedAt = domain.CreatedAt
        };
    }
}
