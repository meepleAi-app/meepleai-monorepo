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
/// Repository implementation for the <see cref="MechanicAnalysisMetrics"/> aggregate (ADR-051 Sprint 1 / Task 15).
/// </summary>
/// <remarks>
/// Rows are insert-only snapshots. <see cref="GetDashboardAsync"/> projects the latest row per
/// shared game via a <c>ROW_NUMBER() OVER (PARTITION BY shared_game_id ORDER BY computed_at DESC)</c>
/// window function executed in LINQ (EF 8+ supports <c>RowNumber</c> via <c>EF.Functions</c>) —
/// no raw SQL required.
/// </remarks>
internal sealed class MechanicAnalysisMetricsRepository : RepositoryBase, IMechanicAnalysisMetricsRepository
{
    public MechanicAnalysisMetricsRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task AddAsync(MechanicAnalysisMetrics metrics, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        var entity = MapToEntity(metrics);
        await DbContext.MechanicAnalysisMetrics.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public async Task<MechanicAnalysisMetrics?> GetByAnalysisAsync(
        Guid analysisId,
        CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.MechanicAnalysisMetrics
            .AsNoTracking()
            .Where(m => m.MechanicAnalysisId == analysisId)
            .OrderByDescending(m => m.ComputedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<MechanicAnalysisMetrics?> GetLatestByAnalysisAsync(
        Guid analysisId,
        CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.MechanicAnalysisMetrics
            .AsNoTracking()
            .Where(m => m.MechanicAnalysisId == analysisId)
            .OrderByDescending(m => m.ComputedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<IReadOnlyList<DashboardGameRow>> GetDashboardAsync(
        CancellationToken cancellationToken = default)
    {
        // Latest-per-game via a two-step LINQ query:
        //   1) Group metrics by SharedGameId and pick MAX(ComputedAt).
        //   2) Rejoin to grab the full row + the shared game's Title.
        // Equivalent to the spec's
        //   ROW_NUMBER() OVER (PARTITION BY shared_game_id ORDER BY computed_at DESC) = 1
        // but expressed in provider-agnostic LINQ to keep the repo pure EF.
        var latestPerGame =
            from m in DbContext.MechanicAnalysisMetrics.AsNoTracking()
            group m by m.SharedGameId into g
            select new
            {
                SharedGameId = g.Key,
                LastComputedAt = g.Max(x => x.ComputedAt),
            };

        var query =
            from l in latestPerGame
            join m in DbContext.MechanicAnalysisMetrics.AsNoTracking()
                on new { l.SharedGameId, ComputedAt = l.LastComputedAt }
                equals new { m.SharedGameId, m.ComputedAt }
            join g in DbContext.SharedGames.AsNoTracking() on m.SharedGameId equals g.Id
            select new
            {
                m.SharedGameId,
                g.Title,
                m.CertificationStatus,
                m.OverallScore,
                m.ComputedAt,
            };

        var rows = await query.ToListAsync(cancellationToken).ConfigureAwait(false);

        return rows
            .Select(r => new DashboardGameRow(
                SharedGameId: r.SharedGameId,
                Name: r.Title,
                Status: (CertificationStatus)r.CertificationStatus,
                OverallScore: r.OverallScore,
                LastComputedAt: r.ComputedAt))
            .ToList();
    }

    public async Task<IReadOnlyList<MechanicAnalysisMetrics>> GetTrendAsync(
        Guid sharedGameId,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            throw new ArgumentException("Take must be > 0.", nameof(take));
        }

        var entities = await DbContext.MechanicAnalysisMetrics
            .AsNoTracking()
            .Where(m => m.SharedGameId == sharedGameId)
            .OrderByDescending(m => m.ComputedAt)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    // === Mapping ===

    private static MechanicAnalysisMetrics MapToDomain(MechanicAnalysisMetricsEntity entity)
    {
        return MechanicAnalysisMetrics.Reconstitute(
            id: entity.Id,
            mechanicAnalysisId: entity.MechanicAnalysisId,
            sharedGameId: entity.SharedGameId,
            coveragePct: entity.CoveragePct,
            pageAccuracyPct: entity.PageAccuracyPct,
            bggMatchPct: entity.BggMatchPct,
            overallScore: entity.OverallScore,
            certificationStatus: (CertificationStatus)entity.CertificationStatus,
            goldenVersionHash: entity.GoldenVersionHash,
            thresholdsSnapshotJson: entity.ThresholdsSnapshotJson,
            matchDetailsJson: entity.MatchDetailsJson,
            computedAt: entity.ComputedAt);
    }

    private static MechanicAnalysisMetricsEntity MapToEntity(MechanicAnalysisMetrics metrics)
    {
        return new MechanicAnalysisMetricsEntity
        {
            Id = metrics.Id,
            MechanicAnalysisId = metrics.MechanicAnalysisId,
            SharedGameId = metrics.SharedGameId,
            CoveragePct = metrics.CoveragePct,
            PageAccuracyPct = metrics.PageAccuracyPct,
            BggMatchPct = metrics.BggMatchPct,
            OverallScore = metrics.OverallScore,
            CertificationStatus = (int)metrics.CertificationStatus,
            GoldenVersionHash = metrics.GoldenVersionHash,
            ThresholdsSnapshotJson = metrics.ThresholdsSnapshotJson,
            MatchDetailsJson = metrics.MatchDetailsJson,
            ComputedAt = metrics.ComputedAt,
        };
    }
}
