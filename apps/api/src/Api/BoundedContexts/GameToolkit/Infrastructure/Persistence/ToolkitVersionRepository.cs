using Api.BoundedContexts.GameToolkit.Domain.Entities;
using Api.BoundedContexts.GameToolkit.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameToolkit;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameToolkit.Infrastructure.Persistence;

/// <summary>
/// Repository for the <see cref="ToolkitVersion"/> aggregate.
/// Phase 5 schema foundation per issue #822.
/// </summary>
internal class ToolkitVersionRepository : RepositoryBase, IToolkitVersionRepository
{
    public ToolkitVersionRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<ToolkitVersion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<ToolkitVersionEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<IReadOnlyList<ToolkitVersion>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<ToolkitVersionEntity>()
            .AsNoTracking()
            .OrderByDescending(v => v.PublishedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<ToolkitVersion>> GetByToolkitIdAsync(
        Guid toolkitId,
        bool includeYanked = false,
        CancellationToken cancellationToken = default)
    {
        var query = DbContext.Set<ToolkitVersionEntity>()
            .AsNoTracking()
            .Where(v => v.ToolkitId == toolkitId);

        if (!includeYanked)
        {
            query = query.Where(v => v.YankedAt == null);
        }

        var entities = await query
            .OrderByDescending(v => v.PublishedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<ToolkitVersion?> GetLatestNonYankedAsync(
        Guid toolkitId,
        CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<ToolkitVersionEntity>()
            .AsNoTracking()
            .Where(v => v.ToolkitId == toolkitId && v.YankedAt == null)
            .OrderByDescending(v => v.PublishedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<bool> ExistsAsync(
        Guid toolkitId,
        string versionNumber,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(versionNumber);

        return await DbContext.Set<ToolkitVersionEntity>()
            .AsNoTracking()
            .AnyAsync(
                v => v.ToolkitId == toolkitId && v.VersionNumber == versionNumber,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<ToolkitVersionEntity>()
            .AsNoTracking()
            .AnyAsync(v => v.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(ToolkitVersion version, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(version);
        CollectDomainEvents(version);
        var entity = MapToPersistence(version);
        await DbContext.Set<ToolkitVersionEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(ToolkitVersion version, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(version);
        CollectDomainEvents(version);
        var entity = MapToPersistence(version);
        DbContext.Set<ToolkitVersionEntity>().Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(ToolkitVersion version, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(version);
        // Hard delete only used in tests / admin paths; production yanks via the
        // Yank() domain method which persists a soft-delete via UpdateAsync.
        var entity = MapToPersistence(version);
        DbContext.Set<ToolkitVersionEntity>().Remove(entity);
        return Task.CompletedTask;
    }

    // ========================================================================
    // Mapping
    // ========================================================================

    private static ToolkitVersion MapToDomain(ToolkitVersionEntity entity)
    {
        // Bypass the factory constructor on hydration to avoid raising a
        // spurious ToolkitVersionPublishedEvent during reconstitution.
        var version = (ToolkitVersion)System.Runtime.CompilerServices.RuntimeHelpers
            .GetUninitializedObject(typeof(ToolkitVersion));

        SetPrivateProperty(version, "Id", entity.Id);
        SetPrivateProperty(version, "ToolkitId", entity.ToolkitId);
        SetPrivateProperty(version, "VersionNumber", entity.VersionNumber);
        SetPrivateProperty(version, "Changelog", entity.Changelog);
        SetPrivateProperty(version, "PublishedAt", entity.PublishedAt);
        SetPrivateProperty(version, "PublishedBy", entity.PublishedBy);
        SetPrivateProperty(version, "YankedAt", entity.YankedAt);
        SetPrivateProperty(version, "YankReason", entity.YankReason);
        SetPrivateProperty(version, "YankedBy", entity.YankedBy);

        // Reinitialise the domain-events list (GetUninitializedObject leaves it null).
        SetPrivateField(version, "_domainEvents", new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>());

        return version;
    }

    private static ToolkitVersionEntity MapToPersistence(ToolkitVersion version)
    {
        return new ToolkitVersionEntity
        {
            Id = version.Id,
            ToolkitId = version.ToolkitId,
            VersionNumber = version.VersionNumber,
            Changelog = version.Changelog,
            PublishedAt = version.PublishedAt,
            PublishedBy = version.PublishedBy,
            YankedAt = version.YankedAt,
            YankReason = version.YankReason,
            YankedBy = version.YankedBy,
        };
    }

    // Reflection helpers for DDD aggregate reconstitution from persistence layer.
    // S3011 suppressed: accessibility bypass is intentional for restoring aggregate
    // state without invoking the factory constructor (mirrors GameToolkitRepository).
#pragma warning disable S3011
    private static void SetPrivateProperty(object instance, string propertyName, object? value)
    {
        var prop = typeof(ToolkitVersion).GetProperty(
            propertyName,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Property {propertyName} not found on ToolkitVersion.");
        prop.SetValue(instance, value);
    }

    private static void SetPrivateField(object instance, string fieldName, object? value)
    {
        var field = typeof(ToolkitVersion).BaseType?.GetField(
            fieldName,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Instance)
            ?? typeof(ToolkitVersion).GetField(
                fieldName,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Field {fieldName} not found on ToolkitVersion or its base type.");
        field.SetValue(instance, value);
    }
#pragma warning restore S3011
}
