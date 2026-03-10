namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Resolves expansion game IDs for a given base game via EntityLinks.
/// Used by the RAG pipeline to boost expansion document scores.
/// Issue #5588: Expansion priority in RAG search.
/// </summary>
internal interface IExpansionGameResolver
{
    /// <summary>
    /// Returns the IDs of games that are expansions of the given base game.
    /// </summary>
    /// <param name="baseGameId">The base game ID to find expansions for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of expansion game IDs (empty if none found).</returns>
    Task<IReadOnlyList<Guid>> GetExpansionGameIdsAsync(Guid baseGameId, CancellationToken ct);
}
