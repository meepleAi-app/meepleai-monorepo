using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Api.BoundedContexts.Administration.Infrastructure.Persistence;

internal class AlertRepository : RepositoryBase, IAlertRepository
{
    public AlertRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<Alert?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<Api.Infrastructure.Entities.AlertEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<Alert>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<Api.Infrastructure.Entities.AlertEntity>()
            .AsNoTracking()
            .OrderByDescending(a => a.TriggeredAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<Alert>> GetActiveAlertsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<Api.Infrastructure.Entities.AlertEntity>()
            .AsNoTracking()
            .Where(a => a.IsActive)
            .OrderByDescending(a => a.TriggeredAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<Alert>> GetAlertsByTypeAsync(string alertType, CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<Api.Infrastructure.Entities.AlertEntity>()
            .AsNoTracking()
            .Where(a => a.AlertType == alertType)
            .OrderByDescending(a => a.TriggeredAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task AddAsync(Alert alert, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(alert);
        var entity = MapToPersistence(alert);
        await DbContext.Set<Api.Infrastructure.Entities.AlertEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(Alert alert, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(alert);
        var entity = MapToPersistence(alert);
        DbContext.Set<Api.Infrastructure.Entities.AlertEntity>().Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Alert alert, CancellationToken cancellationToken = default)
    {
        var entity = MapToPersistence(alert);
        DbContext.Set<Api.Infrastructure.Entities.AlertEntity>().Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<Api.Infrastructure.Entities.AlertEntity>()
            .AnyAsync(a => a.Id == id, cancellationToken).ConfigureAwait(false);
    }

    private static Alert MapToDomain(Api.Infrastructure.Entities.AlertEntity entity)
    {
        var severity = entity.Severity.ToLower(CultureInfo.InvariantCulture) switch
        {
            "critical" => AlertSeverity.Critical,
            "warning" => AlertSeverity.Warning,
            _ => AlertSeverity.Info
        };

        var alert = new Alert(
            id: entity.Id,
            alertType: entity.AlertType,
            severity: severity,
            message: entity.Message,
            metadata: entity.Metadata
        );

        // Override timestamps
        var triggeredAtProp = typeof(Alert).GetProperty("TriggeredAt");
        triggeredAtProp?.SetValue(alert, entity.TriggeredAt);

        var resolvedAtProp = typeof(Alert).GetProperty("ResolvedAt");
        resolvedAtProp?.SetValue(alert, entity.ResolvedAt);

        var isActiveProp = typeof(Alert).GetProperty("IsActive");
        isActiveProp?.SetValue(alert, entity.IsActive);

        return alert;
    }

    private static Api.Infrastructure.Entities.AlertEntity MapToPersistence(Alert domain)
    {
        return new Api.Infrastructure.Entities.AlertEntity
        {
            Id = domain.Id,
            AlertType = domain.AlertType,
            Severity = domain.Severity.Value,
            Message = domain.Message,
            Metadata = domain.Metadata,
            TriggeredAt = domain.TriggeredAt,
            ResolvedAt = domain.ResolvedAt,
            IsActive = domain.IsActive
        };
    }
}
