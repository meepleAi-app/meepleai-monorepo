using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.Authentication;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Authentication.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for <see cref="WaitlistEntry"/> aggregate.
/// Maps between the domain aggregate and <see cref="WaitlistEntryEntity"/> infrastructure POCO.
/// </summary>
internal sealed class WaitlistEntryRepository : RepositoryBase, IWaitlistEntryRepository
{
    public WaitlistEntryRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector) { }

    public async Task<WaitlistEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.WaitlistEntries
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<IReadOnlyList<WaitlistEntry>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.WaitlistEntries
            .OrderBy(e => e.Position)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return entities.Select(MapToDomain).ToList();
    }

    public async Task AddAsync(WaitlistEntry entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        CollectDomainEvents(entity);
        var infrastructureEntity = MapToInfrastructure(entity);
        await DbContext.WaitlistEntries.AddAsync(infrastructureEntity, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(WaitlistEntry entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        CollectDomainEvents(entity);
        var existing = await DbContext.WaitlistEntries
            .FindAsync(new object[] { entity.Id }, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
            throw new InvalidOperationException($"WaitlistEntry {entity.Id} not found");

        existing.Email = entity.Email;
        existing.Name = entity.Name;
        existing.GamePreferenceId = entity.GamePreferenceId;
        existing.GamePreferenceOther = entity.GamePreferenceOther;
        existing.NewsletterOptIn = entity.NewsletterOptIn;
        existing.Position = entity.Position;
        existing.CreatedAt = entity.CreatedAt;
        existing.ContactedAt = entity.ContactedAt;
    }

    public async Task DeleteAsync(WaitlistEntry entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var existing = await DbContext.WaitlistEntries
            .FindAsync(new object[] { entity.Id }, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
            DbContext.WaitlistEntries.Remove(existing);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.WaitlistEntries
            .AnyAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<WaitlistEntry?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var entity = await DbContext.WaitlistEntries
            .FirstOrDefaultAsync(e => e.Email == normalizedEmail, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<int?> GetMaxPositionAsync(CancellationToken cancellationToken = default)
    {
        // EF Core translates Max() over an empty sequence to a NULL-aware aggregate.
        // Selecting nullable int avoids the "sequence contains no elements" exception on empty tables.
        return await DbContext.WaitlistEntries
            .Select(e => (int?)e.Position)
            .MaxAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Acquires a Postgres transaction-scoped advisory lock so concurrent POST /waitlist
    /// requests serialize the read-max-then-insert critical section. Lock auto-releases on
    /// COMMIT/ROLLBACK. No-op outside Postgres (e.g. in-memory tests using mocked repos).
    /// Spec §3.5 (concurrency model).
    /// </summary>
    public async Task AcquirePositionLockAsync(CancellationToken cancellationToken = default)
    {
        if (!DbContext.Database.IsNpgsql())
        {
            return;
        }

        // Constant 64-bit key dedicated to the waitlist-position critical section.
        // Different keys would deadlock-free-coexist; the same key serializes all callers.
        await DbContext.Database
            .ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(8439485847294583299)", cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Maps infrastructure entity to domain aggregate.
    /// Uses internal factory + RestoreState method (no reflection — S3011).
    /// </summary>
    private static WaitlistEntry MapToDomain(WaitlistEntryEntity entity)
    {
        var aggregate = WaitlistEntry.CreateForHydration(entity.Id);
        aggregate.RestoreState(
            email: entity.Email,
            name: entity.Name,
            gamePreferenceId: entity.GamePreferenceId,
            gamePreferenceOther: entity.GamePreferenceOther,
            newsletterOptIn: entity.NewsletterOptIn,
            position: entity.Position,
            createdAt: entity.CreatedAt,
            contactedAt: entity.ContactedAt);
        return aggregate;
    }

    /// <summary>
    /// Maps domain aggregate to infrastructure entity.
    /// </summary>
    private static WaitlistEntryEntity MapToInfrastructure(WaitlistEntry domain)
    {
        return new WaitlistEntryEntity
        {
            Id = domain.Id,
            Email = domain.Email,
            Name = domain.Name,
            GamePreferenceId = domain.GamePreferenceId,
            GamePreferenceOther = domain.GamePreferenceOther,
            NewsletterOptIn = domain.NewsletterOptIn,
            Position = domain.Position,
            CreatedAt = domain.CreatedAt,
            ContactedAt = domain.ContactedAt
        };
    }
}
