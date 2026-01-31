using Api.BoundedContexts.WorkflowIntegration.Domain.Entities;
using Api.BoundedContexts.WorkflowIntegration.Domain.Repositories;
using Api.BoundedContexts.WorkflowIntegration.Domain.ValueObjects;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.WorkflowIntegration.Infrastructure.Persistence;

internal class N8NConfigurationRepository : RepositoryBase, IN8NConfigurationRepository
{
    public N8NConfigurationRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<N8NConfiguration?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<Api.Infrastructure.Entities.N8NConfigEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<N8NConfiguration>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<Api.Infrastructure.Entities.N8NConfigEntity>()
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<N8NConfiguration?> GetActiveConfigurationAsync(CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<Api.Infrastructure.Entities.N8NConfigEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.IsActive, cancellationToken).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<N8NConfiguration?> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<Api.Infrastructure.Entities.N8NConfigEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Name == name, cancellationToken).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task AddAsync(N8NConfiguration config, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(config);
        var entity = MapToPersistence(config);
        await DbContext.Set<Api.Infrastructure.Entities.N8NConfigEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(N8NConfiguration config, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(config);
        var entity = MapToPersistence(config);
        DbContext.Set<Api.Infrastructure.Entities.N8NConfigEntity>().Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(N8NConfiguration config, CancellationToken cancellationToken = default)
    {
        var entity = MapToPersistence(config);
        DbContext.Set<Api.Infrastructure.Entities.N8NConfigEntity>().Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<Api.Infrastructure.Entities.N8NConfigEntity>()
            .AnyAsync(c => c.Id == id, cancellationToken).ConfigureAwait(false);
    }

    private static N8NConfiguration MapToDomain(Api.Infrastructure.Entities.N8NConfigEntity entity)
    {
        var baseUrl = new WorkflowUrl(entity.BaseUrl);
        WorkflowUrl? webhookUrl = entity.WebhookUrl != null ? new WorkflowUrl(entity.WebhookUrl) : null;

        var config = new N8NConfiguration(
            id: entity.Id,
            name: entity.Name,
            baseUrl: baseUrl,
            apiKeyEncrypted: entity.ApiKeyEncrypted,
            createdByUserId: entity.CreatedByUserId,
            webhookUrl: webhookUrl
        );

        // Override timestamps and state from DB
        var isActiveProp = typeof(N8NConfiguration).GetProperty("IsActive");
        isActiveProp?.SetValue(config, entity.IsActive);

        var lastTestedAtProp = typeof(N8NConfiguration).GetProperty("LastTestedAt");
        lastTestedAtProp?.SetValue(config, entity.LastTestedAt);

        var lastTestResultProp = typeof(N8NConfiguration).GetProperty("LastTestResult");
        lastTestResultProp?.SetValue(config, entity.LastTestResult);

        var createdAtProp = typeof(N8NConfiguration).GetProperty("CreatedAt");
        createdAtProp?.SetValue(config, entity.CreatedAt);

        var updatedAtProp = typeof(N8NConfiguration).GetProperty("UpdatedAt");
        updatedAtProp?.SetValue(config, entity.UpdatedAt);

        return config;
    }

    private static Api.Infrastructure.Entities.N8NConfigEntity MapToPersistence(N8NConfiguration domain)
    {
        return new Api.Infrastructure.Entities.N8NConfigEntity
        {
            Id = domain.Id,
            Name = domain.Name,
            BaseUrl = domain.BaseUrl.Value,
            ApiKeyEncrypted = domain.ApiKeyEncrypted,
            WebhookUrl = domain.WebhookUrl?.Value,
            IsActive = domain.IsActive,
            LastTestedAt = domain.LastTestedAt,
            LastTestResult = domain.LastTestResult,
            CreatedAt = domain.CreatedAt,
            UpdatedAt = domain.UpdatedAt,
            CreatedByUserId = domain.CreatedByUserId
        };
    }
}
