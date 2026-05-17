using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.Administration;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Infrastructure.Persistence;

internal sealed class StagingAllowlistRepository : RepositoryBase, IStagingAllowlistRepository
{
    public StagingAllowlistRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    // Note: `IsDeleted` filtering is enforced globally by the entity's HasQueryFilter
    // in StagingAllowlistEntityConfiguration. Methods that need to see soft-deleted
    // rows must call `.IgnoreQueryFilters()` explicitly (see UpdateAsync below).

    public async Task<StagingAllowlistEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.StagingAllowlist
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<IReadOnlyList<StagingAllowlistEntry>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.StagingAllowlist
            .AsNoTracking()
            .OrderByDescending(e => e.AddedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<StagingAllowlistEntry?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(normalizedEmail);

        var entity = await DbContext.StagingAllowlist
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Email == normalizedEmail, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<bool> ExistsByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(normalizedEmail);

        return await DbContext.StagingAllowlist
            .AnyAsync(e => e.Email == normalizedEmail, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlySet<string>> GetAllowedEmailsAsync(CancellationToken cancellationToken = default)
    {
        var emails = await DbContext.StagingAllowlist
            .AsNoTracking()
            .Select(e => e.Email)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new HashSet<string>(emails, StringComparer.Ordinal);
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        DbContext.StagingAllowlist.AnyAsync(e => e.Id == id, cancellationToken);

    public async Task AddAsync(StagingAllowlistEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        CollectDomainEvents(entry);
        var entity = MapToPersistence(entry);
        await DbContext.StagingAllowlist.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(StagingAllowlistEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        CollectDomainEvents(entry);

        // Soft-delete path: load tracked entity bypassing the IsDeleted filter
        var tracked = await DbContext.StagingAllowlist
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == entry.Id, cancellationToken)
            .ConfigureAwait(false);

        if (tracked is null)
        {
            throw new InvalidOperationException($"StagingAllowlistEntry {entry.Id} not found");
        }

        tracked.Email = entry.Email;
        tracked.AddedByUserId = entry.AddedByUserId;
        tracked.AddedAt = entry.AddedAt;
        tracked.Note = entry.Note;
        tracked.IsDeleted = entry.IsDeleted;
        tracked.DeletedAt = entry.DeletedAt;
        tracked.DeletedByUserId = entry.DeletedByUserId;
    }

    public Task DeleteAsync(StagingAllowlistEntry entry, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(
            "Hard delete not permitted on staging_allowlist. Use SoftDelete + UpdateAsync to preserve audit history.");
    }

    private static StagingAllowlistEntry MapToDomain(StagingAllowlistEntity entity)
    {
        // Reconstitute domain entity from persistence — bypasses the public factory
        // (which would set new Id/AddedAt and emit domain events) by using reflection
        // via the parameterless ctor + direct property writes is not ideal but the
        // pattern is consistent with AuditLogRepository.MapToDomain (uses public ctor with id).
        // Here we use a dedicated reconstitution method via factory + state injection.
        return StagingAllowlistEntryMapper.Reconstitute(entity);
    }

    private static StagingAllowlistEntity MapToPersistence(StagingAllowlistEntry domain) =>
        new()
        {
            Id = domain.Id,
            Email = domain.Email,
            AddedByUserId = domain.AddedByUserId,
            AddedAt = domain.AddedAt,
            Note = domain.Note,
            IsDeleted = domain.IsDeleted,
            DeletedAt = domain.DeletedAt,
            DeletedByUserId = domain.DeletedByUserId
        };
}
