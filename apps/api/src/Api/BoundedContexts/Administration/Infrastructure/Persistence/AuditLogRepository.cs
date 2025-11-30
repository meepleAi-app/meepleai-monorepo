using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Infrastructure.Persistence;

public class AuditLogRepository : RepositoryBase, IAuditLogRepository
{
    public AuditLogRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<AuditLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.AuditLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<List<AuditLog>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.AuditLogs
            .AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<AuditLog>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.AuditLogs
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<AuditLog>> GetByResourceAsync(string resource, CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.AuditLogs
            .AsNoTracking()
            .Where(a => a.Resource == resource)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(auditLog);
        var entity = MapToPersistence(auditLog);
        await DbContext.AuditLogs.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(AuditLog auditLog, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Audit logs are immutable and cannot be updated");
    }

    public Task DeleteAsync(AuditLog auditLog, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Audit logs cannot be deleted for compliance");
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.AuditLogs.AnyAsync(a => a.Id == id, cancellationToken).ConfigureAwait(false);
    }

    private static AuditLog MapToDomain(Api.Infrastructure.Entities.AuditLogEntity entity)
    {
        return new AuditLog(
            id: entity.Id,
            userId: entity.UserId,
            action: entity.Action,
            resource: entity.Resource,
            result: entity.Result,
            resourceId: entity.ResourceId,
            details: entity.Details,
            ipAddress: entity.IpAddress,
            userAgent: entity.UserAgent
        );
    }

    private static Api.Infrastructure.Entities.AuditLogEntity MapToPersistence(AuditLog domain)
    {
        return new Api.Infrastructure.Entities.AuditLogEntity
        {
            Id = domain.Id,
            UserId = domain.UserId,
            Action = domain.Action,
            Resource = domain.Resource,
            ResourceId = domain.ResourceId,
            Result = domain.Result,
            Details = domain.Details,
            IpAddress = domain.IpAddress,
            UserAgent = domain.UserAgent,
            CreatedAt = domain.CreatedAt
        };
    }
}
