using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Api.BoundedContexts.Administration.Infrastructure.Persistence;

/// <summary>
/// EF Core repository for AdminReport aggregate
/// ISSUE-916: Repository implementation with domain/entity mapping
/// </summary>
public sealed class AdminReportRepository : IAdminReportRepository
{
    private readonly MeepleAiDbContext _dbContext;

    public AdminReportRepository(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<AdminReport?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _dbContext.AdminReports
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            .ConfigureAwait(false);

        return entity is not null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<AdminReport>> GetAllAsync(CancellationToken ct = default)
    {
        var entities = await _dbContext.AdminReports
            .AsNoTracking()
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<AdminReport>> GetActiveScheduledReportsAsync(CancellationToken ct = default)
    {
        var entities = await _dbContext.AdminReports
            .AsNoTracking()
            .Where(r => r.IsActive && r.ScheduleExpression != null)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task AddAsync(AdminReport report, CancellationToken ct = default)
    {
        var entity = MapToEntity(report);
        await _dbContext.AdminReports.AddAsync(entity, ct).ConfigureAwait(false);
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task UpdateAsync(AdminReport report, CancellationToken ct = default)
    {
        var entity = MapToEntity(report);
        _dbContext.AdminReports.Update(entity);
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _dbContext.AdminReports
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            .ConfigureAwait(false);

        if (entity is not null)
        {
            _dbContext.AdminReports.Remove(entity);
            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }

    // Domain to Entity mapping
    private static AdminReportEntity MapToEntity(AdminReport domain)
    {
        return new AdminReportEntity
        {
            Id = domain.Id,
            Name = domain.Name,
            Description = domain.Description,
            Template = (int)domain.Template,
            Format = (int)domain.Format,
            ParametersJson = JsonSerializer.Serialize(domain.Parameters),
            ScheduleExpression = domain.ScheduleExpression,
            IsActive = domain.IsActive,
            CreatedAt = domain.CreatedAt,
            LastExecutedAt = domain.LastExecutedAt,
            CreatedBy = domain.CreatedBy
        };
    }

    // Entity to Domain mapping
    private static AdminReport MapToDomain(AdminReportEntity entity)
    {
        // ISSUE-916: Safe JSON deserialization with type restrictions (SECURITY)
        var options = new JsonSerializerOptions
        {
            MaxDepth = 10,
            PropertyNameCaseInsensitive = false,
            AllowTrailingCommas = false
        };

        var parameters = string.IsNullOrWhiteSpace(entity.ParametersJson)
            ? new Dictionary<string, object>()
            : JsonSerializer.Deserialize<Dictionary<string, object>>(entity.ParametersJson, options)
              ?? new Dictionary<string, object>();

        return new AdminReport
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Template = (ReportTemplate)entity.Template,
            Format = (ReportFormat)entity.Format,
            Parameters = parameters,
            ScheduleExpression = entity.ScheduleExpression,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            LastExecutedAt = entity.LastExecutedAt,
            CreatedBy = entity.CreatedBy
        };
    }
}
