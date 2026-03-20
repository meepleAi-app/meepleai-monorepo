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
/// EF Core repository for ReportExecution aggregate
/// ISSUE-916: Repository implementation for execution history
/// </summary>
internal sealed class ReportExecutionRepository : RepositoryBase, IReportExecutionRepository
{

    public ReportExecutionRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    // CA1869: Cache JsonSerializerOptions for better performance
    private static readonly JsonSerializerOptions s_deserializeOptions = new()
    {
        MaxDepth = 10,
        PropertyNameCaseInsensitive = false,
        AllowTrailingCommas = false
    };

    public async Task<ReportExecution?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.ReportExecutions
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity is not null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<ReportExecution>> GetByReportIdAsync(
        Guid reportId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.ReportExecutions
            .AsNoTracking()
            .Where(e => e.ReportId == reportId)
            .OrderByDescending(e => e.StartedAt)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<ReportExecution>> GetRecentExecutionsAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.ReportExecutions
            .AsNoTracking()
            .OrderByDescending(e => e.StartedAt)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task AddAsync(ReportExecution execution, CancellationToken cancellationToken = default)
    {
        var entity = MapToEntity(execution);
        await DbContext.ReportExecutions.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(ReportExecution execution, CancellationToken cancellationToken = default)
    {
        // Issue #2541: Check if entity is already tracked to prevent identity conflicts
        var tracked = DbContext.ReportExecutions.Local.FirstOrDefault(e => e.Id == execution.Id);

        if (tracked != null)
        {
            // Update tracked entity properties
            tracked.CompletedAt = execution.CompletedAt;
            tracked.Status = (int)execution.Status;
            tracked.ErrorMessage = execution.ErrorMessage;
            tracked.FileSizeBytes = execution.FileSizeBytes;
        }
        else
        {
            var entity = MapToEntity(execution);
            DbContext.ReportExecutions.Update(entity);
        }

        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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
        var metadata = string.IsNullOrWhiteSpace(entity.ExecutionMetadataJson)
            ? new Dictionary<string, object>(StringComparer.Ordinal)
            : JsonSerializer.Deserialize<Dictionary<string, object>>(entity.ExecutionMetadataJson, s_deserializeOptions)
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

