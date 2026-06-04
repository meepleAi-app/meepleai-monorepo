using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for <see cref="CatalogSeedDraftEntity"/>.
/// Reads use <c>AsNoTracking()</c>; write/update paths use <c>AsTracking()</c> per
/// PERF-06 (CLAUDE.md memory <c>notracking-default-update-gotcha</c>) so that
/// subsequent <c>SaveChangesAsync()</c> persists modifications without explicit
/// <c>.Update()</c> attaching a detached graph.
/// Issue #1903 — M1.5.
/// </summary>
internal sealed class CatalogSeedDraftRepository : ICatalogSeedDraftRepository
{
    private readonly MeepleAiDbContext _db;

    public CatalogSeedDraftRepository(MeepleAiDbContext db)
        => _db = db ?? throw new ArgumentNullException(nameof(db));

    public Task<CatalogSeedDraftEntity?> GetByIdAsync(Guid id, CancellationToken ct)
        => _db.CatalogSeedDrafts.AsTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CatalogSeedDraftEntity>> GetByStatusAsync(
        string status, int take, CancellationToken ct)
        => await _db.CatalogSeedDrafts
            .AsTracking()
            .Where(x => x.Status == status)
            .OrderBy(x => x.CreatedAt)
            .Take(take)
            .ToListAsync(ct)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<CatalogSeedDraftEntity>> ListAsync(
        string? statusFilter, int skip, int take, CancellationToken ct)
    {
        var q = _db.CatalogSeedDrafts.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            q = q.Where(x => x.Status == statusFilter);
        }
        return await q
            .OrderByDescending(x => x.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public Task<int> CountAsync(string? statusFilter, CancellationToken ct)
    {
        var q = _db.CatalogSeedDrafts.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            q = q.Where(x => x.Status == statusFilter);
        }
        return q.CountAsync(ct);
    }

    public async Task AddAsync(CatalogSeedDraftEntity entity, CancellationToken ct)
        => await _db.CatalogSeedDrafts.AddAsync(entity, ct).ConfigureAwait(false);

    public Task AddRangeAsync(IReadOnlyList<CatalogSeedDraftEntity> entities, CancellationToken ct)
        => _db.CatalogSeedDrafts.AddRangeAsync(entities, ct);

    public void Update(CatalogSeedDraftEntity entity) => _db.CatalogSeedDrafts.Update(entity);

    public Task<bool> ExistsAsync(int bggId, CancellationToken ct)
        => _db.CatalogSeedDrafts.AsNoTracking().AnyAsync(x => x.BggId == bggId, ct);
}
