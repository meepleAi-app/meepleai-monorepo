namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.StructuredRetrieval;

/// <summary>
/// Service that retrieves structured data from RulebookAnalysis fields based on query intent.
/// Issue #5453: Structured RAG fusion.
/// </summary>
public interface IStructuredRetrievalService
{
    /// <summary>
    /// Retrieves structured results for a query against a specific game's RulebookAnalysis.
    /// </summary>
    /// <param name="query">The user's question.</param>
    /// <param name="sharedGameId">The game to search structured data for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Structured retrieval response with results, classification, and fusion metadata.</returns>
    Task<StructuredRetrievalResponse> RetrieveAsync(
        string query,
        Guid sharedGameId,
        CancellationToken cancellationToken = default);
}
