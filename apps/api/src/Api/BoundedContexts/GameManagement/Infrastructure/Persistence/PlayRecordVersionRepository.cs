using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of IPlayRecordVersionRepository.
/// #2437-3: version history + restore for play records.
/// </summary>
internal sealed class PlayRecordVersionRepository : RepositoryBase, IPlayRecordVersionRepository
{
    public PlayRecordVersionRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    /// <inheritdoc />
    public async Task<int> GetNextVersionNumberAsync(Guid playRecordId, CancellationToken ct = default)
    {
        var maxVersion = await DbContext.PlayRecordVersions
            .Where(v => v.PlayRecordId == playRecordId)
            .MaxAsync(v => (int?)v.VersionNumber, ct)
            .ConfigureAwait(false);

        return (maxVersion ?? 0) + 1;
    }

    /// <inheritdoc />
    public async Task AddAsync(PlayRecordVersion version, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(version);
        var entity = MapToPersistence(version);
        await DbContext.PlayRecordVersions.AddAsync(entity, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PlayRecordVersion>> GetRecentAsync(
        Guid playRecordId, int limit, CancellationToken ct = default)
    {
        var entities = await DbContext.PlayRecordVersions
            .AsNoTracking()
            .Where(v => v.PlayRecordId == playRecordId)
            .OrderByDescending(v => v.VersionNumber)
            .Take(limit)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    /// <inheritdoc />
    public async Task<PlayRecordVersion?> GetByVersionNumberAsync(
        Guid playRecordId, int versionNumber, CancellationToken ct = default)
    {
        var entity = await DbContext.PlayRecordVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.PlayRecordId == playRecordId && v.VersionNumber == versionNumber, ct)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    /// <inheritdoc />
    public async Task PruneOldestAsync(Guid playRecordId, int keep, CancellationToken ct = default)
    {
        var toDelete = await DbContext.PlayRecordVersions
            .Where(v => v.PlayRecordId == playRecordId)
            .OrderByDescending(v => v.VersionNumber)
            .Skip(keep)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (toDelete.Count > 0)
        {
            DbContext.PlayRecordVersions.RemoveRange(toDelete);
        }
    }

    // ========================================================================
    // Mapping
    // ========================================================================

    private static PlayRecordVersion MapToDomain(PlayRecordVersionEntity entity)
    {
        // PlayRecordVersion is a simple Entity<Guid> with no domain events.
        // Use the internal constructor directly — repositories share the same assembly.
        return new PlayRecordVersion(
            entity.Id,
            entity.PlayRecordId,
            entity.VersionNumber,
            entity.SessionDate,
            entity.Notes,
            entity.Location,
            entity.CreatedAt,
            entity.CreatedByUserId);
    }

    private static PlayRecordVersionEntity MapToPersistence(PlayRecordVersion version)
    {
        return new PlayRecordVersionEntity
        {
            Id = version.Id,
            PlayRecordId = version.PlayRecordId,
            VersionNumber = version.VersionNumber,
            SessionDate = version.SessionDate,
            Notes = version.Notes,
            Location = version.Location,
            CreatedAt = version.CreatedAt,
            CreatedByUserId = version.CreatedByUserId,
        };
    }
}
