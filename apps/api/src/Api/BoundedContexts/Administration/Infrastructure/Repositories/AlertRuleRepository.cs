using Api.BoundedContexts.Administration.Domain.Aggregates.AlertRules;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.Administration;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Infrastructure.Repositories;

public class AlertRuleRepository : IAlertRuleRepository
{
    private readonly MeepleAiDbContext _context;
    public AlertRuleRepository(MeepleAiDbContext context) => _context = context;
    
    public async Task<AlertRule?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _context.AlertRules.FindAsync(new object[] { id }, ct);
        return entity == null ? null : MapToDomain(entity);
    }
    
    public async Task<AlertRule?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        var entity = await _context.AlertRules.FirstOrDefaultAsync(r => r.Name == name, ct);
        return entity == null ? null : MapToDomain(entity);
    }
    
    public async Task<List<AlertRule>> GetAllAsync(CancellationToken ct = default) =>
        (await _context.AlertRules.ToListAsync(ct)).Select(MapToDomain).ToList();
    
    public async Task<List<AlertRule>> GetEnabledAsync(CancellationToken ct = default) =>
        (await _context.AlertRules.Where(r => r.IsEnabled).ToListAsync(ct)).Select(MapToDomain).ToList();
    
    public async Task<List<AlertRule>> GetByAlertTypeAsync(string alertType, CancellationToken ct = default) =>
        (await _context.AlertRules.Where(r => r.AlertType == alertType).ToListAsync(ct)).Select(MapToDomain).ToList();
    
    public async Task AddAsync(AlertRule alertRule, CancellationToken ct = default)
    {
        await _context.AlertRules.AddAsync(MapToEntity(alertRule), ct);
        await _context.SaveChangesAsync(ct);
    }
    
    public async Task UpdateAsync(AlertRule alertRule, CancellationToken ct = default)
    {
        var entity = await _context.AlertRules.FindAsync(new object[] { alertRule.Id }, ct);
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
        await _context.SaveChangesAsync(ct);
    }
    
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _context.AlertRules.FindAsync(new object[] { id }, ct);
        if (entity != null) { _context.AlertRules.Remove(entity); await _context.SaveChangesAsync(ct); }
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
