namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.Enhancements;

/// <summary>
/// Generates multiple query variations for RAG-Fusion retrieval.
/// Uses a FAST (free) model to produce alternative phrasings of the user's question.
/// </summary>
internal interface IQueryExpander
{
    /// <summary>
    /// Expand a user query into multiple variations for parallel retrieval.
    /// The original query is always included as the first element.
    /// </summary>
    /// <param name="query">The user's natural language query</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of query variations (original + up to 4 alternatives)</returns>
    Task<List<string>> ExpandAsync(string query, CancellationToken ct);
}
