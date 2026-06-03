using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;

/// <summary>
/// EF Core repository for the <see cref="CatalogSyncRun"/> aggregate (#1861, F4-A6 BE).
/// </summary>
internal sealed class CatalogSyncRunRepository : RepositoryBase, ICatalogSyncRunRepository
{
    public CatalogSyncRunRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task AddAsync(CatalogSyncRun run, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        var entity = MapToEntity(run);
        await DbContext.CatalogSyncRuns.AddAsync(entity, cancellationToken).ConfigureAwait(false);

        CollectDomainEvents(run);
    }

    public async Task<CatalogSyncRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.CatalogSyncRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<CatalogSyncRun?> GetCurrentRunningAsync(CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.CatalogSyncRuns
            .AsNoTracking()
            .Where(r => r.Status == CatalogSyncStatus.Running)
            .OrderByDescending(r => r.StartedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<CatalogSyncRun?> GetLatestCompletedAsync(CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.CatalogSyncRuns
            .AsNoTracking()
            .Where(r => r.Status == CatalogSyncStatus.Success
                     || r.Status == CatalogSyncStatus.Failed
                     || r.Status == CatalogSyncStatus.TimedOut)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<(IReadOnlyList<CatalogSyncRun> Items, int Total)> GetPagedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(page), page, "Page must be >= 1.");
        }

        if (pageSize < 1 || pageSize > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, "PageSize must be 1-100.");
        }

        var query = DbContext.CatalogSyncRuns.AsNoTracking();
        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var entities = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = entities.Select(MapToDomain).ToList();
        return (items, total);
    }

    /// <summary>
    /// Persists changes to a previously loaded run. Assumes the aggregate was loaded via the
    /// <c>AsNoTracking</c> paths above, so no change-tracker conflict.
    /// </summary>
    public Task UpdateAsync(CatalogSyncRun run, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        var entity = MapToEntity(run);
        DbContext.CatalogSyncRuns.Update(entity);

        CollectDomainEvents(run);
        return Task.CompletedTask;
    }

    // === Mapping ===

    private static CatalogSyncRun MapToDomain(CatalogSyncRunEntity entity)
    {
        return CatalogSyncRun.Reconstitute(
            id: entity.Id,
            provider: entity.Provider,
            status: entity.Status,
            title: entity.Title,
            triggeredByUserId: entity.TriggeredByUserId,
            itemsAdded: entity.ItemsAdded,
            itemsUpdated: entity.ItemsUpdated,
            itemsFailed: entity.ItemsFailed,
            errorCode: entity.ErrorCode,
            errorDetail: entity.ErrorDetail,
            logTailJsonPath: entity.LogTailJsonPath,
            createdAt: entity.CreatedAt,
            startedAt: entity.StartedAt,
            completedAt: entity.CompletedAt);
    }

    private static CatalogSyncRunEntity MapToEntity(CatalogSyncRun run)
    {
        return new CatalogSyncRunEntity
        {
            Id = run.Id,
            Provider = run.Provider,
            Status = run.Status,
            Title = run.Title,
            TriggeredByUserId = run.TriggeredByUserId,
            ItemsAdded = run.ItemsAdded,
            ItemsUpdated = run.ItemsUpdated,
            ItemsFailed = run.ItemsFailed,
            ErrorCode = run.ErrorCode,
            ErrorDetail = run.ErrorDetail,
            LogTailJsonPath = run.LogTailJsonPath,
            CreatedAt = run.CreatedAt,
            StartedAt = run.StartedAt,
            CompletedAt = run.CompletedAt,
        };
    }
}
