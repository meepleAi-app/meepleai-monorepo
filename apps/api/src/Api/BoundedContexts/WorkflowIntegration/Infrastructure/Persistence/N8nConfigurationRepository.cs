using Api.BoundedContexts.WorkflowIntegration.Domain.Entities;
using Api.BoundedContexts.WorkflowIntegration.Domain.Repositories;
using Api.BoundedContexts.WorkflowIntegration.Domain.ValueObjects;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.WorkflowIntegration.Infrastructure.Persistence;

public class N8nConfigurationRepository : IN8nConfigurationRepository
{
    private readonly MeepleAiDbContext _dbContext;

    public N8nConfigurationRepository(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<N8nConfiguration?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Set<Api.Infrastructure.Entities.N8nConfigEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<List<N8nConfiguration>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.Set<Api.Infrastructure.Entities.N8nConfigEntity>()
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<N8nConfiguration?> GetActiveConfigurationAsync(CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Set<Api.Infrastructure.Entities.N8nConfigEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.IsActive, cancellationToken);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<N8nConfiguration?> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Set<Api.Infrastructure.Entities.N8nConfigEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Name == name, cancellationToken);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task AddAsync(N8nConfiguration config, CancellationToken cancellationToken = default)
    {
        var entity = MapToPersistence(config);
        await _dbContext.Set<Api.Infrastructure.Entities.N8nConfigEntity>().AddAsync(entity, cancellationToken);
    }

    public Task UpdateAsync(N8nConfiguration config, CancellationToken cancellationToken = default)
    {
        var entity = MapToPersistence(config);
        _dbContext.Set<Api.Infrastructure.Entities.N8nConfigEntity>().Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(N8nConfiguration config, CancellationToken cancellationToken = default)
    {
        var entity = MapToPersistence(config);
        _dbContext.Set<Api.Infrastructure.Entities.N8nConfigEntity>().Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<Api.Infrastructure.Entities.N8nConfigEntity>()
            .AnyAsync(c => c.Id == id, cancellationToken);
    }

    private static N8nConfiguration MapToDomain(Api.Infrastructure.Entities.N8nConfigEntity entity)
    {
        var baseUrl = new WorkflowUrl(entity.BaseUrl);
        WorkflowUrl? webhookUrl = entity.WebhookUrl != null ? new WorkflowUrl(entity.WebhookUrl) : null;

        var config = new N8nConfiguration(
            id: entity.Id,
            name: entity.Name,
            baseUrl: baseUrl,
            apiKeyEncrypted: entity.ApiKeyEncrypted,
            createdByUserId: entity.CreatedByUserId,
            webhookUrl: webhookUrl
        );

        // Override timestamps and state from DB
        var isActiveProp = typeof(N8nConfiguration).GetProperty("IsActive");
        isActiveProp?.SetValue(config, entity.IsActive);

        var lastTestedAtProp = typeof(N8nConfiguration).GetProperty("LastTestedAt");
        lastTestedAtProp?.SetValue(config, entity.LastTestedAt);

        var lastTestResultProp = typeof(N8nConfiguration).GetProperty("LastTestResult");
        lastTestResultProp?.SetValue(config, entity.LastTestResult);

        var createdAtProp = typeof(N8nConfiguration).GetProperty("CreatedAt");
        createdAtProp?.SetValue(config, entity.CreatedAt);

        var updatedAtProp = typeof(N8nConfiguration).GetProperty("UpdatedAt");
        updatedAtProp?.SetValue(config, entity.UpdatedAt);

        return config;
    }

    private static Api.Infrastructure.Entities.N8nConfigEntity MapToPersistence(N8nConfiguration domain)
    {
        return new Api.Infrastructure.Entities.N8nConfigEntity
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
