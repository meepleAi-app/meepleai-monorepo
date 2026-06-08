using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Middleware.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.UserLibrary.Infrastructure.Persistence;

/// <summary>
/// Repository implementation for ProposalMigration aggregate.
/// Issue #3666: Phase 5 - Migration Choice Flow.
/// </summary>
public sealed class ProposalMigrationRepository : RepositoryBase, IProposalMigrationRepository
{

    public ProposalMigrationRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task AddAsync(ProposalMigration migration, CancellationToken cancellationToken = default)
    {
        var entity = MapToEntity(migration);
        await DbContext.Set<ProposalMigrationEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> AddAndCommitAsync(ProposalMigration migration, CancellationToken cancellationToken = default)
    {
        var entity = MapToEntity(migration);
        await DbContext.Set<ProposalMigrationEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);

        try
        {
            await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException ex) when (CounterTableIdempotency.IsUniqueViolation(ex))
        {
            // CF-2 / #1938: outbox replay or concurrent pod already inserted the row.
            // Detach so the next SaveChanges on this scoped context doesn't retry,
            // and signal the caller to skip downstream work.
            _ = ex;
            DbContext.Entry(entity).State = EntityState.Detached;
            return false;
        }
    }

    public async Task<ProposalMigration?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<ProposalMigrationEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken).ConfigureAwait(false);

        return entity == null ? null : MapToDomain(entity);
    }

    public async Task<List<ProposalMigration>> GetPendingByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<ProposalMigrationEntity>()
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.Choice == (int)PostApprovalMigrationChoice.Pending)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<ProposalMigration?> GetByShareRequestIdAsync(Guid shareRequestId, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<ProposalMigrationEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.ShareRequestId == shareRequestId, cancellationToken).ConfigureAwait(false);

        return entity == null ? null : MapToDomain(entity);
    }

    public Task UpdateAsync(ProposalMigration migration, CancellationToken cancellationToken = default)
    {
        var entity = MapToEntity(migration);

        // Issue #3531: Check if entity is already tracked to avoid InvalidOperationException
        var existingEntry = DbContext.ChangeTracker.Entries<ProposalMigrationEntity>()
            .FirstOrDefault(e => e.Entity.Id == entity.Id);

        if (existingEntry != null)
        {
            existingEntry.CurrentValues.SetValues(entity);
        }
        else
        {
            DbContext.Set<ProposalMigrationEntity>().Update(entity);
        }

        return Task.CompletedTask;
    }

    // Mapping methods
    private static ProposalMigrationEntity MapToEntity(ProposalMigration migration)
    {
        return new ProposalMigrationEntity
        {
            Id = migration.Id,
            ShareRequestId = migration.ShareRequestId,
            PrivateGameId = migration.PrivateGameId,
            SharedGameId = migration.SharedGameId,
            UserId = migration.UserId,
            Choice = (int)migration.Choice,
            CreatedAt = migration.CreatedAt,
            ChoiceAt = migration.ChoiceAt,
            SourceEventId = migration.SourceEventId
        };
    }

    private static ProposalMigration MapToDomain(ProposalMigrationEntity entity)
    {
        // Use reflection to reconstruct domain entity (since constructor is private)
        var migration = (ProposalMigration)Activator.CreateInstance(typeof(ProposalMigration), true)!;

        typeof(ProposalMigration).GetProperty(nameof(ProposalMigration.Id))!
            .SetValue(migration, entity.Id);
        typeof(ProposalMigration).GetProperty(nameof(ProposalMigration.ShareRequestId))!
            .SetValue(migration, entity.ShareRequestId);
        typeof(ProposalMigration).GetProperty(nameof(ProposalMigration.PrivateGameId))!
            .SetValue(migration, entity.PrivateGameId);
        typeof(ProposalMigration).GetProperty(nameof(ProposalMigration.SharedGameId))!
            .SetValue(migration, entity.SharedGameId);
        typeof(ProposalMigration).GetProperty(nameof(ProposalMigration.UserId))!
            .SetValue(migration, entity.UserId);
        typeof(ProposalMigration).GetProperty(nameof(ProposalMigration.Choice))!
            .SetValue(migration, (PostApprovalMigrationChoice)entity.Choice);
        typeof(ProposalMigration).GetProperty(nameof(ProposalMigration.CreatedAt))!
            .SetValue(migration, entity.CreatedAt);
        typeof(ProposalMigration).GetProperty(nameof(ProposalMigration.ChoiceAt))!
            .SetValue(migration, entity.ChoiceAt);
        typeof(ProposalMigration).GetProperty(nameof(ProposalMigration.SourceEventId))!
            .SetValue(migration, entity.SourceEventId);

        return migration;
    }
}
