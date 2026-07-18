namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Read-model provider that resolves the polymorphic score payload for a set of
/// GameManagement <c>GameSession</c>s shown in session history.
///
/// The score lives in a different bounded context (<c>SessionTracking.Session</c>,
/// table <c>session_tracking_sessions</c>) and is bridged to the history session
/// only through the <c>LiveGameSession</c> aggregate, which carries both
/// <c>CorrelatedGameSessionId</c> (→ GameSession) and <c>TrackingSessionId</c>
/// (→ SessionTracking.Session). This provider isolates that cross-context read so
/// the history query handler stays free of the join (read-model concern, #3080).
/// </summary>
internal interface IHistorySessionScoreProvider
{
    /// <summary>
    /// Resolves the score payload for the given GameSession ids. Only ids that have
    /// a correlated live session with a tracking session are present in the result;
    /// ids with no resolvable score are simply absent from the dictionary.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, HistorySessionScore>> GetScoresAsync(
        IReadOnlyCollection<Guid> gameSessionIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resolves score + the players aligned to scoreData (LiveGameSession SessionPlayers,
    /// whose Id == scoreData.scores[].playerId) for a single GameSession. Null when the
    /// session has no correlated live/tracking session with a score. (#3022)
    /// </summary>
    Task<SessionScoreboard?> GetScoreboardAsync(Guid gameSessionId, CancellationToken cancellationToken);
}

/// <summary>
/// Polymorphic score payload for a single session: the scoring type name
/// ("Points" | "BinaryWin" | "Objectives" | "Ranking") and the raw JSON score data.
/// </summary>
internal readonly record struct HistorySessionScore(string ScoringType, string ScoreData);

/// <summary>
/// Score + the players aligned to scoreData for one session (#3022). Players are the
/// LiveGameSession SessionPlayers, whose Id matches scoreData.scores[].playerId, so the
/// FE can join per-player VP to a display name and color without name-matching.
/// </summary>
internal readonly record struct SessionScoreboard(
    string ScoringType,
    string ScoreData,
    IReadOnlyList<ScorePlayerReadModel> Players);

/// <summary>A player whose Id matches scoreData.scores[].playerId (#3022).</summary>
internal readonly record struct ScorePlayerReadModel(Guid Id, string DisplayName, string Color);
