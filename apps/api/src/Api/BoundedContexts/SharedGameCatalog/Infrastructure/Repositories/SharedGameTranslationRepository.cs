using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ISharedGameTranslationRepository"/>.
/// Issue #2339 — sub-PR 1/3.
/// </summary>
/// <remarks>
/// <para>
/// Maps between the domain aggregate <see cref="SharedGameTranslation"/>
/// and the EF entity <see cref="SharedGameTranslationEntity"/>. Reads use
/// <c>AsNoTracking()</c> for performance; the global query filter
/// (<c>WHERE NOT is_deleted</c>) is honoured automatically.
/// </para>
/// <para>
/// Writes go through the change tracker — handlers are responsible for
/// calling <c>SaveChangesAsync()</c> via the unit-of-work (ADR-060).
/// </para>
/// </remarks>
internal sealed class SharedGameTranslationRepository : ISharedGameTranslationRepository
{
    private readonly MeepleAiDbContext _dbContext;

    public SharedGameTranslationRepository(MeepleAiDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task AddAsync(
        SharedGameTranslation translation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(translation);
        var entity = ToEntity(translation);
        await _dbContext.SharedGameTranslations
            .AddAsync(entity, cancellationToken)
            .ConfigureAwait(false);
    }

    public Task UpdateAsync(
        SharedGameTranslation translation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(translation);

        // Translate the detached aggregate into a fresh entity instance,
        // then attach + mark Modified so EF projects the new state without
        // having to fetch the existing row again. The Xmin token participates
        // in the concurrency check via IsConcurrencyToken on the column.
        var entity = ToEntity(translation);
        _dbContext.SharedGameTranslations.Attach(entity);
        _dbContext.Entry(entity).State = EntityState.Modified;
        return Task.CompletedTask;
    }

    public async Task<SharedGameTranslation?> GetByGameIdAndLocaleAsync(
        Guid gameId, string locale, CancellationToken cancellationToken = default)
    {
        if (gameId == Guid.Empty || string.IsNullOrWhiteSpace(locale))
        {
            return null;
        }

        var entity = await _dbContext.SharedGameTranslations
            .AsNoTracking()
            .FirstOrDefaultAsync(
                t => t.SharedGameId == gameId && t.Locale == locale, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : FromEntity(entity);
    }

    public async Task<IReadOnlyList<SharedGameTranslation>> GetByGameIdAsync(
        Guid gameId, CancellationToken cancellationToken = default)
    {
        if (gameId == Guid.Empty)
        {
            return Array.Empty<SharedGameTranslation>();
        }

        var entities = await _dbContext.SharedGameTranslations
            .AsNoTracking()
            .Where(t => t.SharedGameId == gameId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(FromEntity).ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<SharedGameTranslation>>>
        GetByGameIdsAsync(
            IReadOnlyList<Guid> gameIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gameIds);

        if (gameIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<SharedGameTranslation>>();
        }

        var idsSet = gameIds.ToHashSet();
        var entities = await _dbContext.SharedGameTranslations
            .AsNoTracking()
            .Where(t => idsSet.Contains(t.SharedGameId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities
            .GroupBy(t => t.SharedGameId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<SharedGameTranslation>)g
                    .Select(FromEntity)
                    .ToList());
    }

    public Task<bool> ExistsActiveAsync(
        Guid gameId, string locale, CancellationToken cancellationToken = default)
    {
        if (gameId == Guid.Empty || string.IsNullOrWhiteSpace(locale))
        {
            return Task.FromResult(false);
        }

        return _dbContext.SharedGameTranslations
            .AsNoTracking()
            .AnyAsync(
                t => t.SharedGameId == gameId && t.Locale == locale, cancellationToken);
    }

    private static SharedGameTranslationEntity ToEntity(SharedGameTranslation t) => new()
    {
        Id = t.Id,
        SharedGameId = t.SharedGameId,
        Locale = t.Locale.Value,
        Title = t.Title,
        Description = t.Description,
        Source = TranslationSourceMapper.ToPersistedString(t.Source),
        CreatedAt = t.CreatedAt,
        CreatedBy = t.CreatedBy,
        UpdatedAt = t.UpdatedAt,
        UpdatedBy = t.UpdatedBy,
        IsDeleted = t.IsDeleted,
        DeletedAt = t.DeletedAt,
        DeletedBy = t.DeletedBy,
        Xmin = t.Xmin
    };

    private static SharedGameTranslation FromEntity(SharedGameTranslationEntity e) =>
        SharedGameTranslation.Rehydrate(
            e.Id,
            e.SharedGameId,
            Locale.Create(e.Locale),
            e.Title,
            e.Description,
            TranslationSourceMapper.FromPersistedString(e.Source),
            e.CreatedAt,
            e.CreatedBy,
            e.UpdatedAt,
            e.UpdatedBy,
            e.IsDeleted,
            e.DeletedAt,
            e.DeletedBy,
            e.Xmin);
}
