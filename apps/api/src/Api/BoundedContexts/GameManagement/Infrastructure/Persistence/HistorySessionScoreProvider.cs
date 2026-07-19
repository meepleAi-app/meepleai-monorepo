using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Persistence;

/// <summary>
/// EF Core read-model provider that resolves the polymorphic score for session
/// history rows by bridging, within the shared <see cref="MeepleAiDbContext"/>:
/// <c>GameManagement.GameSession</c> ← <c>LiveGameSession.CorrelatedGameSessionId</c>
/// and <c>LiveGameSession.TrackingSessionId</c> → <c>SessionTracking.Session</c>
/// (which owns <c>scoring_type</c> + <c>score_data</c>). Isolates the cross-context
/// read so the history query handler stays free of the join (#3080).
/// </summary>
internal sealed class HistorySessionScoreProvider : IHistorySessionScoreProvider
{
    private readonly MeepleAiDbContext _dbContext;

    public HistorySessionScoreProvider(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<IReadOnlyDictionary<Guid, HistorySessionScore>> GetScoresAsync(
        IReadOnlyCollection<Guid> gameSessionIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(gameSessionIds);

        if (gameSessionIds.Count == 0)
            return new Dictionary<Guid, HistorySessionScore>();

        var ids = gameSessionIds as Guid[] ?? gameSessionIds.ToArray();

        var rows = await (
            from live in _dbContext.LiveGameSessions.AsNoTracking()
            where live.CorrelatedGameSessionId != null
                  && ids.Contains(live.CorrelatedGameSessionId.Value)
            join track in _dbContext.SessionTrackingSessions.AsNoTracking()
                on live.TrackingSessionId equals (Guid?)track.Id
            select new
            {
                live.CorrelatedGameSessionId,
                track.ScoringType,
                track.ScoreData,
                track.UpdatedAt,
                track.CreatedAt
            }
        ).ToListAsync(cancellationToken).ConfigureAwait(false);

        // A GameSession correlates to at most one live/tracking session. If the data
        // somehow carries more than one, take the most recently updated tracking row.
        return rows
            .Where(r => r.CorrelatedGameSessionId.HasValue)
            .GroupBy(r => r.CorrelatedGameSessionId!.Value)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var latest = group.OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt).First();
                    return new HistorySessionScore(latest.ScoringType, latest.ScoreData);
                });
    }

    public async Task<SessionScoreboard?> GetScoreboardAsync(
        Guid gameSessionId,
        CancellationToken cancellationToken)
    {
        // Bridge GameSession → LiveGameSession → SessionTracking.Session (owns score_data).
        var scoreRow = await (
            from live in _dbContext.LiveGameSessions.AsNoTracking()
            where live.CorrelatedGameSessionId == gameSessionId
            join track in _dbContext.SessionTrackingSessions.AsNoTracking()
                on live.TrackingSessionId equals (Guid?)track.Id
            orderby (track.UpdatedAt ?? track.CreatedAt) descending
            select new { LiveId = live.Id, track.ScoringType, track.ScoreData }
        ).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (scoreRow is null)
            return null;

        // The SessionPlayers on that same LiveGameSession are the identity space of
        // scoreData.scores[].playerId, and carry DisplayName + Color.
        var players = await _dbContext.SessionPlayers.AsNoTracking()
            .Where(p => p.LiveGameSessionId == scoreRow.LiveId)
            .Select(p => new ScorePlayerReadModel(p.Id, p.DisplayName, p.Color))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return new SessionScoreboard(scoreRow.ScoringType, scoreRow.ScoreData, players);
    }
}
