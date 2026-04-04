namespace Api.BoundedContexts.GameManagement.Domain.Services;

/// <summary>
/// Searches knowledge base chunks for turn/phase rules of a specific game.
/// Interface defined in GameManagement domain; implemented in infrastructure using pgvector.
/// </summary>
internal interface IPhaseRulesSearchService
{
    Task<IReadOnlyList<string>> SearchRulesChunksAsync(Guid gameId, string query, int topK, CancellationToken ct = default);
}
