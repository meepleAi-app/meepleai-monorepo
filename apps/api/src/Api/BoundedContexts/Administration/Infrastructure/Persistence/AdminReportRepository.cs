using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Api.BoundedContexts.Administration.Infrastructure.Persistence;

/// <summary>
/// EF Core repository for AdminReport aggregate
/// ISSUE-916: Repository implementation with domain/entity mapping
/// </summary>
internal sealed class AdminReportRepository : RepositoryBase, IAdminReportRepository
{

    public AdminReportRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<AdminReport?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.AdminReports
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity is not null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<AdminReport>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.AdminReports
            .AsNoTracking()
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<AdminReport>> GetActiveScheduledReportsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.AdminReports
            .AsNoTracking()
            .Where(r => r.IsActive && r.ScheduleExpression != null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task AddAsync(AdminReport report, CancellationToken cancellationToken = default)
    {
        var entity = MapToEntity(report);
        await DbContext.AdminReports.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(AdminReport report, CancellationToken cancellationToken = default)
    {
        var entity = MapToEntity(report);
        DbContext.AdminReports.Update(entity);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.AdminReports
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is not null)
        {
            DbContext.AdminReports.Remove(entity);
            await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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
            CreatedBy = domain.CreatedBy,
            EmailRecipientsJson = JsonSerializer.Serialize(domain.EmailRecipients) // ISSUE-918
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
            ? new Dictionary<string, object>(StringComparer.Ordinal)
            : JsonSerializer.Deserialize<Dictionary<string, object>>(entity.ParametersJson, options)
              ?? new Dictionary<string, object>(StringComparer.Ordinal);

        // ISSUE-918: Deserialize email recipients
        var emailRecipientsList = string.IsNullOrWhiteSpace(entity.EmailRecipientsJson)
            ? new List<string>()
            : JsonSerializer.Deserialize<List<string>>(entity.EmailRecipientsJson, options)
              ?? new List<string>();

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
            CreatedBy = entity.CreatedBy,
            EmailRecipients = emailRecipientsList.AsReadOnly()
        };
    }
}

