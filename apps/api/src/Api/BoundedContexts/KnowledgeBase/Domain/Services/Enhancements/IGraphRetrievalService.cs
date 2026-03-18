namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.Enhancements;

/// <summary>
/// RAG Enhancement: Graph RAG retrieval service.
/// Queries the entity-relation knowledge graph for a game and formats
/// the results as additional context to inject into the RAG prompt.
/// </summary>
internal interface IGraphRetrievalService
{
    /// <summary>
    /// Retrieve entity-relation context for a game from the knowledge graph.
    /// </summary>
    /// <param name="gameId">The game to retrieve entity relations for</param>
    /// <param name="maxRelations">Maximum number of relations to return</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Formatted knowledge graph context string, or empty if no relations exist</returns>
    Task<string> GetEntityContextAsync(Guid gameId, int maxRelations, CancellationToken ct);
}
