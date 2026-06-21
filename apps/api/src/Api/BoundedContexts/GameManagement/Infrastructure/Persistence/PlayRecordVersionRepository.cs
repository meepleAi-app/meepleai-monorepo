using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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

    /// <inheritdoc />
    public async Task ReassignVersionNumberAsync(Guid playRecordId, Guid versionId, CancellationToken ct = default)
    {
        // Re-read the current MAX to get the next safe version number.
        var maxVersion = await DbContext.PlayRecordVersions
            .Where(v => v.PlayRecordId == playRecordId)
            .MaxAsync(v => (int?)v.VersionNumber, ct)
            .ConfigureAwait(false);

        var newVersionNumber = (maxVersion ?? 0) + 1;

        // Update the already-tracked (Added) entity in the change tracker so the
        // next SaveChangesAsync will use the newly computed VersionNumber.
        var trackedEntry = DbContext.ChangeTracker
            .Entries<PlayRecordVersionEntity>()
            .FirstOrDefault(e => e.Entity.Id == versionId);

        if (trackedEntry is not null)
        {
            trackedEntry.Entity.VersionNumber = newVersionNumber;
        }
    }

    // ========================================================================
    // Conflict detection
    // ========================================================================

    /// <summary>
    /// Returns <c>true</c> when <paramref name="ex"/> wraps a PostgreSQL unique-violation
    /// on the <c>UX_play_record_versions_record_version</c> index — the signal that two
    /// concurrent saves computed the same MAX+1 version number for the same record.
    /// Delegates to the testable overload so tests can probe the predicate without
    /// constructing a real <see cref="PostgresException"/>.
    /// </summary>
    internal static bool IsVersionNumberConflict(DbUpdateException ex) =>
        ex.InnerException is PostgresException pgEx
        && IsVersionNumberConflict(pgEx.SqlState, pgEx.ConstraintName);

    /// <summary>
    /// Testable inner predicate — checks SQLSTATE 23505 + the specific constraint name.
    /// Mirrors the split used by <c>NotificationDedupePipelineBehavior.IsNotificationDedupViolation</c>.
    /// </summary>
    internal static bool IsVersionNumberConflict(string? sqlState, string? constraintName) =>
        string.Equals(sqlState, "23505", StringComparison.Ordinal)
        && string.Equals(constraintName, "UX_play_record_versions_record_version", StringComparison.Ordinal);

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
