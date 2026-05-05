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
/// Singleton row (<c>Id = 1</c>) is guaranteed by the M2.0 migration, which seeds the default
/// thresholds on first apply. <see cref="GetAsync"/> therefore expects to find the row; absence
/// indicates a broken deployment and surfaces as an <see cref="InvalidOperationException"/>.
/// The repository never commits mid-Unit-of-Work (no <c>SaveChangesAsync</c> inside read/write
/// methods) — persistence is always deferred to the caller's UoW boundary.
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

        if (entity is null)
        {
            // Migration M2.0 seeds the singleton row; absence indicates a broken deployment.
            // Fail fast rather than silently re-seeding — a mid-UoW SaveChangesAsync here would
            // flush unrelated tracked changes and break test isolation.
            throw new InvalidOperationException(
                "CertificationThresholdsConfig singleton row (Id=1) is missing. " +
                "Ensure the M2.0 migration has been applied.");
        }

        return MapToDomain(entity);
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
