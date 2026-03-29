using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Handles GetVectorStatsQuery.
/// Aggregates VectorDocument records from pgvector grouped by SharedGameId,
/// computing chunk counts and health percentages per game.
/// Task 3: Qdrant → pgvector migration.
/// </summary>
internal sealed class GetVectorStatsQueryHandler : IQueryHandler<GetVectorStatsQuery, VectorStatsDto>
{
    /// <summary>e5-base embedding dimensions (constant for this deployment).</summary>
    private const int Dimensions = 768;

    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetVectorStatsQueryHandler> _logger;

    public GetVectorStatsQueryHandler(
        MeepleAiDbContext dbContext,
        ILogger<GetVectorStatsQueryHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<VectorStatsDto> Handle(
        GetVectorStatsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Total vector count and overall health: lightweight projection over all documents.
        var allDocs = await _dbContext.VectorDocuments
            .AsNoTracking()
            .Select(v => new
            {
                v.SharedGameId,
                v.ChunkCount,
                v.IndexingStatus
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (allDocs.Count == 0)
        {
            _logger.LogDebug("No VectorDocuments found in database; returning zero stats.");
            return new VectorStatsDto(
                TotalVectors: 0,
                Dimensions: Dimensions,
                GamesIndexed: 0,
                AvgHealthPercent: 100,
                SizeEstimateBytes: 0,
                GameBreakdown: new List<VectorGameBreakdownDto>());
        }

        var totalVectors = allDocs.Sum(v => (long)v.ChunkCount);

        // Build per-game breakdown using a database-side GroupBy to avoid
        // materialising all rows for the aggregation.
        var grouped = await _dbContext.VectorDocuments
            .AsNoTracking()
            .Where(v => v.SharedGameId.HasValue)
            .GroupBy(v => v.SharedGameId!.Value)
            .Select(g => new
            {
                SharedGameId = g.Key,
                Total = (long)g.Sum(v => v.ChunkCount),
                Completed = (long)g.Sum(v => v.IndexingStatus == "completed" ? v.ChunkCount : 0),
                Failed = (long)g.Sum(v => v.IndexingStatus == "failed" ? v.ChunkCount : 0),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Fetch SharedGame titles for all relevant IDs in one round-trip.
        var sharedGameIds = grouped.Select(g => g.SharedGameId).ToList();
        var gameTitles = await _dbContext.SharedGames
            .AsNoTracking()
            .Where(sg => sharedGameIds.Contains(sg.Id))
            .Select(sg => new { sg.Id, sg.Title })
            .ToDictionaryAsync(sg => sg.Id, sg => sg.Title, cancellationToken)
            .ConfigureAwait(false);

        var breakdown = new List<VectorGameBreakdownDto>(grouped.Count);

        foreach (var g in grouped)
        {
            var healthPercent = g.Total > 0
                ? (int)Math.Round(g.Completed * 100.0 / g.Total)
                : 100;

            gameTitles.TryGetValue(g.SharedGameId, out var title);

            breakdown.Add(new VectorGameBreakdownDto(
                GameId: g.SharedGameId,
                GameName: title ?? g.SharedGameId.ToString(),
                VectorCount: g.Total,
                CompletedCount: g.Completed,
                FailedCount: g.Failed,
                HealthPercent: healthPercent));
        }

        // AvgHealthPercent based on all chunked vectors (including private-game docs).
        var totalCompletedAll = allDocs
            .Where(v => string.Equals(v.IndexingStatus, "completed", StringComparison.OrdinalIgnoreCase))
            .Sum(v => (long)v.ChunkCount);

        var avgHealthPercent = totalVectors > 0
            ? (int)Math.Round(totalCompletedAll * 100.0 / totalVectors)
            : 100;

        // SizeEstimateBytes = vectors × 768 floats × 4 bytes
        var sizeEstimateBytes = totalVectors * Dimensions * 4L;

        return new VectorStatsDto(
            TotalVectors: totalVectors,
            Dimensions: Dimensions,
            GamesIndexed: breakdown.Count,
            AvgHealthPercent: avgHealthPercent,
            SizeEstimateBytes: sizeEstimateBytes,
            GameBreakdown: breakdown);
    }
}
