using Api.BoundedContexts.Administration.Domain.Aggregates.AlertRules;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Api.Infrastructure.Entities.Administration;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Infrastructure.Repositories;

internal class AlertRuleRepository : RepositoryBase, IAlertRuleRepository
{
    public AlertRuleRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<AlertRule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.AlertRules.FindAsync(new object[] { id }, cancellationToken).ConfigureAwait(false);
        return entity == null ? null : MapToDomain(entity);
    }

    public async Task<AlertRule?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(name);
        var entity = await DbContext.AlertRules.FirstOrDefaultAsync(r => r.Name == name, cancellationToken).ConfigureAwait(false);
        return entity == null ? null : MapToDomain(entity);
    }

    public async Task<List<AlertRule>> GetAllAsync(CancellationToken cancellationToken = default) =>
        (await DbContext.AlertRules.ToListAsync(cancellationToken).ConfigureAwait(false)).Select(MapToDomain).ToList();

    public async Task<List<AlertRule>> GetEnabledAsync(CancellationToken cancellationToken = default) =>
        (await DbContext.AlertRules.Where(r => r.IsEnabled).ToListAsync(cancellationToken).ConfigureAwait(false)).Select(MapToDomain).ToList();

    public async Task<List<AlertRule>> GetByAlertTypeAsync(string alertType, CancellationToken cancellationToken = default) =>
        (await DbContext.AlertRules.Where(r => r.AlertType == alertType).ToListAsync(cancellationToken).ConfigureAwait(false)).Select(MapToDomain).ToList();

    public async Task AddAsync(AlertRule alertRule, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(alertRule);
        await DbContext.AlertRules.AddAsync(MapToEntity(alertRule), cancellationToken).ConfigureAwait(false);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(AlertRule alertRule, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(alertRule);
        var entity = await DbContext.AlertRules.FindAsync(new object[] { alertRule.Id }, cancellationToken).ConfigureAwait(false);
        if (entity == null) throw new InvalidOperationException($"AlertRule {alertRule.Id} not found");
        entity.Name = alertRule.Name;
        entity.Severity = alertRule.Severity.ToDisplayString();
        entity.Description = alertRule.Description;
        entity.Threshold = alertRule.Threshold.Value;
        entity.ThresholdUnit = alertRule.Threshold.Unit;
        entity.DurationMinutes = alertRule.Duration.Minutes;
        entity.IsEnabled = alertRule.IsEnabled;
        entity.Metadata = alertRule.Metadata;
        entity.UpdatedAt = alertRule.UpdatedAt;
        entity.UpdatedBy = alertRule.UpdatedBy;
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.AlertRules.FindAsync(new object[] { id }, cancellationToken).ConfigureAwait(false);
        if (entity != null) { DbContext.AlertRules.Remove(entity); await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false); }
    }

    private static AlertRule MapToDomain(AlertRuleEntity e) =>
        AlertRule.Reconstitute(
            e.Id,
            e.Name,
            e.AlertType,
            AlertSeverityExtensions.FromString(e.Severity),
            new AlertThreshold(e.Threshold, e.ThresholdUnit),
            new AlertDuration(e.DurationMinutes),
            e.IsEnabled,
            e.Description,
            e.Metadata,
            e.CreatedAt,
            e.UpdatedAt,
            e.CreatedBy,
            e.UpdatedBy
        );

    private static AlertRuleEntity MapToEntity(AlertRule r) => new() { Id = r.Id, Name = r.Name, AlertType = r.AlertType, Severity = r.Severity.ToDisplayString(), Description = r.Description, Threshold = r.Threshold.Value, ThresholdUnit = r.Threshold.Unit, DurationMinutes = r.Duration.Minutes, IsEnabled = r.IsEnabled, Metadata = r.Metadata, CreatedAt = r.CreatedAt, UpdatedAt = r.UpdatedAt, CreatedBy = r.CreatedBy, UpdatedBy = r.UpdatedBy };
}

