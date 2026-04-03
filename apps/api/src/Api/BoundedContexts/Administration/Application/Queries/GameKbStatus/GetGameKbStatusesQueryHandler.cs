using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Queries.GameKbStatus;

/// <summary>
/// Handles <see cref="GetGameKbStatusesQuery"/>.
/// Aggregates VectorDocument records grouped by GameId, joined with Games for names.
/// Returns KB status (complete/partial/none) per game.
/// </summary>
internal sealed class GetGameKbStatusesQueryHandler
    : IRequestHandler<GetGameKbStatusesQuery, GetGameKbStatusesResult>
{
    private readonly MeepleAiDbContext _db;
    private readonly ILogger<GetGameKbStatusesQueryHandler> _logger;

    public GetGameKbStatusesQueryHandler(
        MeepleAiDbContext db,
        ILogger<GetGameKbStatusesQueryHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<GetGameKbStatusesResult> Handle(
        GetGameKbStatusesQuery request,
        CancellationToken cancellationToken)
    {
        // ── 1. Get all games ──────────────────────────────────────────────────
        var games = await _db.Games
            .AsNoTracking()
            .Select(g => new { g.Id, g.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // ── 2. Get VectorDocument stats grouped by GameId ─────────────────────
        var vectorStats = await _db.VectorDocuments
            .AsNoTracking()
            .Where(v => v.GameId != null)
            .GroupBy(v => v.GameId!.Value)
            .Select(g => new
            {
                GameId = g.Key,
                CompletedDocs = g.Count(v => v.IndexingStatus == "completed"),
                TotalDocs = g.Count(),
                TotalChunks = g.Sum(v => (int?)v.ChunkCount) ?? 0,
                LatestIndexedAt = g.Max(v => v.IndexedAt),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var statsByGame = vectorStats.ToDictionary(s => s.GameId);

        _logger.LogDebug(
            "GetGameKbStatuses: {GameCount} games, {IndexedGames} with KB data",
            games.Count, statsByGame.Count);

        // ── 3. Build result ───────────────────────────────────────────────────
        var items = games
            .Select(g =>
            {
                if (!statsByGame.TryGetValue(g.Id, out var stats))
                {
                    return new GameKbStatusDto(
                        GameId: g.Id,
                        GameName: g.Name,
                        KbStatus: "none",
                        DocumentCount: 0,
                        TotalChunks: 0,
                        LatestIndexedAt: null,
                        HasAutoBackup: false);
                }

                var status = stats.CompletedDocs > 0 && stats.CompletedDocs == stats.TotalDocs
                    ? "complete"
                    : stats.CompletedDocs > 0
                        ? "partial"
                        : "none";

                return new GameKbStatusDto(
                    GameId: g.Id,
                    GameName: g.Name,
                    KbStatus: status,
                    DocumentCount: stats.CompletedDocs,
                    TotalChunks: stats.TotalChunks,
                    LatestIndexedAt: stats.LatestIndexedAt,
                    // Auto-backup runs on every successful indexing; if indexed, backup exists
                    HasAutoBackup: stats.CompletedDocs > 0);
            })
            // Sort: complete first, then partial, then none; within each group by name
            .OrderBy(g => string.Equals(g.KbStatus, "complete", StringComparison.Ordinal) ? 0
                        : string.Equals(g.KbStatus, "partial", StringComparison.Ordinal) ? 1 : 2)
            .ThenBy(g => g.GameName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new GetGameKbStatusesResult(items);
    }
}
