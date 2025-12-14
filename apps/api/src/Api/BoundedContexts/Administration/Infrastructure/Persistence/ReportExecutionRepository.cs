using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Api.BoundedContexts.Administration.Infrastructure.Persistence;

/// <summary>
/// EF Core repository for ReportExecution aggregate
/// ISSUE-916: Repository implementation for execution history
/// </summary>
public sealed class ReportExecutionRepository : IReportExecutionRepository
{
    private readonly MeepleAiDbContext _dbContext;

    public ReportExecutionRepository(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<ReportExecution?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _dbContext.ReportExecutions
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            .ConfigureAwait(false);

        return entity is not null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<ReportExecution>> GetByReportIdAsync(
        Guid reportId,
        int limit = 50,
        CancellationToken ct = default)
    {
        var entities = await _dbContext.ReportExecutions
            .AsNoTracking()
            .Where(e => e.ReportId == reportId)
            .OrderByDescending(e => e.StartedAt)
            .Take(limit)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<ReportExecution>> GetRecentExecutionsAsync(
        int limit = 100,
        CancellationToken ct = default)
    {
        var entities = await _dbContext.ReportExecutions
            .AsNoTracking()
            .OrderByDescending(e => e.StartedAt)
            .Take(limit)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task AddAsync(ReportExecution execution, CancellationToken ct = default)
    {
        var entity = MapToEntity(execution);
        await _dbContext.ReportExecutions.AddAsync(entity, ct).ConfigureAwait(false);
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task UpdateAsync(ReportExecution execution, CancellationToken ct = default)
    {
        var entity = MapToEntity(execution);
        _dbContext.ReportExecutions.Update(entity);
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    // Domain to Entity mapping
    private static ReportExecutionEntity MapToEntity(ReportExecution domain)
    {
        return new ReportExecutionEntity
        {
            Id = domain.Id,
            ReportId = domain.ReportId,
            StartedAt = domain.StartedAt,
            CompletedAt = domain.CompletedAt,
            Status = (int)domain.Status,
            ErrorMessage = domain.ErrorMessage,
            OutputPath = domain.OutputPath,
            FileSizeBytes = domain.FileSizeBytes,
            DurationMs = domain.Duration.HasValue ? (long)domain.Duration.Value.TotalMilliseconds : null,
            ExecutionMetadataJson = JsonSerializer.Serialize(domain.ExecutionMetadata)
        };
    }

    // Entity to Domain mapping
    private static ReportExecution MapToDomain(ReportExecutionEntity entity)
    {
        // ISSUE-916: Safe JSON deserialization with type restrictions (SECURITY)
        var options = new JsonSerializerOptions
        {
            MaxDepth = 10,
            PropertyNameCaseInsensitive = false,
            AllowTrailingCommas = false
        };

        var metadata = string.IsNullOrWhiteSpace(entity.ExecutionMetadataJson)
            ? new Dictionary<string, object>(StringComparer.Ordinal)
            : JsonSerializer.Deserialize<Dictionary<string, object>>(entity.ExecutionMetadataJson, options)
              ?? new Dictionary<string, object>(StringComparer.Ordinal);

        return new ReportExecution
        {
            Id = entity.Id,
            ReportId = entity.ReportId,
            StartedAt = entity.StartedAt,
            CompletedAt = entity.CompletedAt,
            Status = (ReportExecutionStatus)entity.Status,
            ErrorMessage = entity.ErrorMessage,
            OutputPath = entity.OutputPath,
            FileSizeBytes = entity.FileSizeBytes,
            Duration = entity.DurationMs.HasValue ? TimeSpan.FromMilliseconds(entity.DurationMs.Value) : null,
            ExecutionMetadata = metadata
        };
    }
}
