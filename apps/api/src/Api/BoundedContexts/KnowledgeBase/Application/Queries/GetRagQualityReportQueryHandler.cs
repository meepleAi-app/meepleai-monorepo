using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.Infrastructure;
using Api.Services;
using Api.SharedKernel.Domain.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Handler for GetRagQualityReportQuery.
/// Returns index-health statistics: document counts, RAPTOR summaries,
/// entity relations, embedded chunks, top games breakdown, and enhancement statuses.
/// </summary>
internal sealed class GetRagQualityReportQueryHandler
    : IRequestHandler<GetRagQualityReportQuery, RagQualityReportDto>
{
    private readonly MeepleAiDbContext _db;
    private readonly IFeatureFlagService _featureFlags;

    public GetRagQualityReportQueryHandler(
        MeepleAiDbContext db,
        IFeatureFlagService featureFlags)
    {
        _db = db;
        _featureFlags = featureFlags;
    }

    public async Task<RagQualityReportDto> Handle(
        GetRagQualityReportQuery request,
        CancellationToken cancellationToken)
    {
        var totalDocs = await _db.VectorDocuments
            .CountAsync(cancellationToken).ConfigureAwait(false);

        var totalRaptor = await _db.RaptorSummaries
            .CountAsync(cancellationToken).ConfigureAwait(false);

        var totalRelations = await _db.GameEntityRelations
            .CountAsync(cancellationToken).ConfigureAwait(false);

        var totalChunks = await _db.TextChunks
            .CountAsync(cancellationToken).ConfigureAwait(false);

        // Top 10 games by chunk count from VectorDocuments
        var topGamesRaw = await _db.VectorDocuments
            .Where(vd => vd.GameId != null)
            .GroupBy(vd => vd.GameId!.Value)
            .Select(g => new
            {
                GameId = g.Key,
                ChunkCount = g.Sum(vd => vd.ChunkCount)
            })
            .OrderByDescending(x => x.ChunkCount)
            .Take(10)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var gameIds = topGamesRaw.Select(g => g.GameId).ToList();

        // Look up game titles
        var gameTitles = await _db.Games
            .Where(g => gameIds.Contains(g.Id))
            .Select(g => new { g.Id, g.Name })
            .ToDictionaryAsync(g => g.Id, g => g.Name, cancellationToken)
            .ConfigureAwait(false);

        // Count RAPTOR summaries per game
        var raptorByGame = await _db.RaptorSummaries
            .Where(r => gameIds.Contains(r.GameId))
            .GroupBy(r => r.GameId)
            .Select(g => new { GameId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.GameId, x => x.Count, cancellationToken)
            .ConfigureAwait(false);

        // Count entity relations per game
        var relationsByGame = await _db.GameEntityRelations
            .Where(r => gameIds.Contains(r.GameId))
            .GroupBy(r => r.GameId)
            .Select(g => new { GameId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.GameId, x => x.Count, cancellationToken)
            .ConfigureAwait(false);

        var topGames = topGamesRaw.Select(g => new RagQualityGameBreakdown(
            GameId: g.GameId,
            GameTitle: gameTitles.GetValueOrDefault(g.GameId, "Unknown"),
            ChunkCount: g.ChunkCount,
            RaptorNodeCount: raptorByGame.GetValueOrDefault(g.GameId, 0),
            EntityRelationCount: relationsByGame.GetValueOrDefault(g.GameId, 0)
        )).ToList();

        // Enhancement statuses — check per-tier flags
        var enhancements = new List<RagEnhancementStatusDto>();
        foreach (var flag in Enum.GetValues<RagEnhancement>())
        {
            if (flag == RagEnhancement.None) continue;

            var key = flag.ToFeatureFlagKey();
            var freeEnabled = await _featureFlags
                .IsEnabledForTierAsync(key, UserTier.Free).ConfigureAwait(false);
            var normalEnabled = await _featureFlags
                .IsEnabledForTierAsync(key, UserTier.Normal).ConfigureAwait(false);
            var premiumEnabled = await _featureFlags
                .IsEnabledForTierAsync(key, UserTier.Premium).ConfigureAwait(false);

            enhancements.Add(new RagEnhancementStatusDto(
                Name: flag.ToString(),
                FeatureFlagKey: key,
                FreeEnabled: freeEnabled,
                NormalEnabled: normalEnabled,
                PremiumEnabled: premiumEnabled));
        }

        return new RagQualityReportDto(
            TotalIndexedDocuments: totalDocs,
            TotalRaptorSummaries: totalRaptor,
            TotalEntityRelations: totalRelations,
            TotalEmbeddedChunks: totalChunks,
            TopGamesByChunkCount: topGames,
            EnhancementStatuses: enhancements);
    }
}
