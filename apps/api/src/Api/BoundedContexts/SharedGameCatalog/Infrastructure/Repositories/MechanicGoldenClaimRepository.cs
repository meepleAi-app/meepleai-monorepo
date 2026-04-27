using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for the <see cref="MechanicGoldenClaim"/> aggregate (ADR-051 Sprint 1 / Task 15).
/// </summary>
/// <remarks>
/// Soft-delete is enforced by a global query filter (<c>!IsDeleted</c>); the aggregate owns its own
/// <c>DeletedAt</c> plus the entity's <c>IsDeleted</c> flag. <see cref="GetVersionHashAsync"/> joins
/// claim triples with BGG tag names to produce the deterministic <see cref="VersionHash"/> consumed
/// by the scoring pipeline.
/// </remarks>
internal sealed class MechanicGoldenClaimRepository : RepositoryBase, IMechanicGoldenClaimRepository
{
    public MechanicGoldenClaimRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<IReadOnlyList<MechanicGoldenClaim>> GetByGameAsync(
        Guid sharedGameId,
        CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.MechanicGoldenClaims
            .AsNoTracking()
            .Where(c => c.SharedGameId == sharedGameId)
            .OrderBy(c => c.Section)
            .ThenBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<MechanicGoldenClaim?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.MechanicGoldenClaims
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task AddAsync(MechanicGoldenClaim claim, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claim);

        var entity = MapToEntity(claim);
        await DbContext.MechanicGoldenClaims.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(MechanicGoldenClaim claim, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claim);

        // Load the tracked entity so EF preserves the server-managed `xmin` concurrency token
        // (loaded original value drives the WHERE clause). A fresh entity built via MapToEntity
        // has Xmin=0 — Postgres' actual xmin is non-zero, so DbSet.Update(entity) would emit
        // UPDATE … WHERE xmin=0, return 0 affected rows and trip DbUpdateConcurrencyException
        // (mirrors the MechanicAnalysisRepository graph-state bug fixed in commit cd708babc).
        // IgnoreQueryFilters lets us hydrate a soft-deleted row in case of a re-deactivation
        // retry (the global filter is `!IsDeleted`).
        var entity = await DbContext.MechanicGoldenClaims
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == claim.Id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            throw new InvalidOperationException(
                $"MechanicGoldenClaim {claim.Id} not found for update — caller should load via GetByIdAsync first.");
        }

        entity.Statement = claim.Statement;
        entity.ExpectedPage = claim.ExpectedPage;
        entity.SourceQuote = claim.SourceQuote;
        entity.KeywordsJson = JsonSerializer.Serialize(claim.Keywords);
        entity.Embedding = claim.Embedding is null ? null : new Vector(claim.Embedding);
        entity.UpdatedAt = claim.UpdatedAt;
        entity.IsDeleted = claim.DeletedAt.HasValue;
        entity.DeletedAt = claim.DeletedAt;
    }

    public async Task<VersionHash?> GetVersionHashAsync(
        Guid sharedGameId,
        CancellationToken cancellationToken = default)
    {
        var claimTriples = await DbContext.MechanicGoldenClaims
            .AsNoTracking()
            .Where(c => c.SharedGameId == sharedGameId)
            .OrderBy(c => c.Id)
            .Select(c => new { c.Id, c.Statement, c.ExpectedPage })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (claimTriples.Count == 0)
        {
            return null;
        }

        var tagNames = await DbContext.MechanicGoldenBggTags
            .AsNoTracking()
            .Where(t => t.SharedGameId == sharedGameId)
            .Select(t => t.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var tuples = claimTriples.Select(x => (x.Id, x.Statement, x.ExpectedPage));
        return VersionHash.Compute(tuples, tagNames);
    }

    // === Mapping ===

    private static MechanicGoldenClaim MapToDomain(MechanicGoldenClaimEntity entity)
    {
        var keywords = string.IsNullOrWhiteSpace(entity.KeywordsJson)
            ? Array.Empty<string>()
            : JsonSerializer.Deserialize<string[]>(entity.KeywordsJson) ?? Array.Empty<string>();

        return MechanicGoldenClaim.Reconstitute(
            id: entity.Id,
            sharedGameId: entity.SharedGameId,
            section: (MechanicSection)entity.Section,
            statement: entity.Statement,
            expectedPage: entity.ExpectedPage,
            sourceQuote: entity.SourceQuote,
            keywords: keywords,
            embedding: entity.Embedding?.ToArray(),
            curatorUserId: entity.CuratorUserId,
            createdAt: entity.CreatedAt,
            updatedAt: entity.UpdatedAt,
            deletedAt: entity.DeletedAt);
    }

    private static MechanicGoldenClaimEntity MapToEntity(MechanicGoldenClaim claim)
    {
        return new MechanicGoldenClaimEntity
        {
            Id = claim.Id,
            SharedGameId = claim.SharedGameId,
            Section = (int)claim.Section,
            Statement = claim.Statement,
            ExpectedPage = claim.ExpectedPage,
            SourceQuote = claim.SourceQuote,
            KeywordsJson = JsonSerializer.Serialize(claim.Keywords),
            Embedding = claim.Embedding is null ? null : new Vector(claim.Embedding),
            CuratorUserId = claim.CuratorUserId,
            CreatedAt = claim.CreatedAt,
            UpdatedAt = claim.UpdatedAt,
            IsDeleted = claim.DeletedAt.HasValue,
            DeletedAt = claim.DeletedAt,
        };
    }
}
