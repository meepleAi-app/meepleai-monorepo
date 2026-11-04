namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Read-model that resolves, for a set of GameManagement <c>GameSession</c>s, the id of the
/// <c>LiveGameSession</c> each one is the companion of (#4156).
///
/// The correlation is stored on the live side only (<c>LiveGameSession.CorrelatedGameSessionId</c>,
/// set by the start saga of ADR-083), so a caller holding a GameSession cannot reach the live
/// session on its own. The session pages under <c>/sessions/[id]</c> address a live session:
/// lists and reads of GameSessions expose this id so their links can point there, instead of
/// the pages guessing the aggregate from the id (ADR-089 §2).
/// </summary>
internal interface ILiveSessionLinkReader
{
    /// <summary>
    /// Maps each given GameSession id to the id of its correlated live session. Ids that no
    /// live session correlates to are absent from the result.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, Guid>> GetLiveSessionIdsAsync(
        IReadOnlyCollection<Guid> gameSessionIds,
        CancellationToken cancellationToken);
}
