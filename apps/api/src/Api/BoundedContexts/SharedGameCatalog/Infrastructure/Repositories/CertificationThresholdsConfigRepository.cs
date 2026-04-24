using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for the <see cref="CertificationThresholdsConfig"/> aggregate (ADR-051 Sprint 1 / Task 15).
/// </summary>
/// <remarks>
/// Singleton row (<c>Id = 1</c>) — the M2.0 migration seeds the default values on first apply, so
/// <see cref="GetAsync"/> is guaranteed to find a row. If a deployment somehow loses the seed row,
/// we fall back to <see cref="CertificationThresholdsConfig.Seed"/> and persist it to honour the
/// interface contract (non-null result).
/// Optimistic concurrency is enforced via PostgreSQL's <c>xmin</c> system column.
/// </remarks>
internal sealed class CertificationThresholdsConfigRepository : RepositoryBase, ICertificationThresholdsConfigRepository
{
    public CertificationThresholdsConfigRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<CertificationThresholdsConfig> GetAsync(CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.CertificationThresholdsConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == 1, cancellationToken)
            .ConfigureAwait(false);

        if (entity is not null)
        {
            return MapToDomain(entity);
        }

        // Defensive fallback: seed the singleton on first access if missing.
        var seed = CertificationThresholdsConfig.Seed();
        var seedEntity = MapToEntity(seed);
        await DbContext.CertificationThresholdsConfigs.AddAsync(seedEntity, cancellationToken).ConfigureAwait(false);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Reload so the returned aggregate carries the DB-assigned xmin token — without this,
        // a subsequent UpdateAsync on the returned instance would fail concurrency checks
        // (aggregate xmin = 0 vs DB row xmin > 0).
        var reloaded = await DbContext.CertificationThresholdsConfigs
            .AsNoTracking()
            .FirstAsync(c => c.Id == 1, cancellationToken)
            .ConfigureAwait(false);
        return MapToDomain(reloaded);
    }

    public Task UpdateAsync(CertificationThresholdsConfig config, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        var entity = MapToEntity(config);
        DbContext.CertificationThresholdsConfigs.Update(entity);
        return Task.CompletedTask;
    }

    // === Mapping ===

    private static CertificationThresholdsConfig MapToDomain(CertificationThresholdsConfigEntity entity)
    {
        var thresholds = CertificationThresholds.Create(
            minCoveragePct: entity.MinCoveragePct,
            maxPageTolerance: entity.MaxPageTolerance,
            minBggMatchPct: entity.MinBggMatchPct,
            minOverallScore: entity.MinOverallScore);

        return CertificationThresholdsConfig.Reconstitute(
            thresholds: thresholds,
            updatedAt: entity.UpdatedAt,
            updatedByUserId: entity.UpdatedByUserId,
            xminVersion: entity.Xmin);
    }

    private static CertificationThresholdsConfigEntity MapToEntity(CertificationThresholdsConfig config)
    {
        return new CertificationThresholdsConfigEntity
        {
            Id = config.Id,
            MinCoveragePct = config.Thresholds.MinCoveragePct,
            MaxPageTolerance = config.Thresholds.MaxPageTolerance,
            MinBggMatchPct = config.Thresholds.MinBggMatchPct,
            MinOverallScore = config.Thresholds.MinOverallScore,
            UpdatedAt = config.UpdatedAt,
            UpdatedByUserId = config.UpdatedByUserId,
            Xmin = config.XminVersion,
        };
    }
}
