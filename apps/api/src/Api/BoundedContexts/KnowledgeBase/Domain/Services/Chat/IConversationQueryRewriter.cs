namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Rewrites ambiguous follow-up queries into standalone questions
/// using conversation context for improved RAG retrieval.
/// Issue #5258: Conversation-aware query rewriting.
/// </summary>
internal interface IConversationQueryRewriter
{
    /// <summary>
    /// Rewrites a follow-up question into a standalone query using conversation history.
    /// Returns the original query unchanged if rewriting is not needed or fails.
    /// </summary>
    /// <param name="currentQuery">The user's current question</param>
    /// <param name="chatHistoryContext">Formatted conversation history from ChatContextDomainService</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A standalone rewritten query, or the original if rewriting is not applicable</returns>
    Task<string> RewriteQueryAsync(
        string currentQuery,
        string chatHistoryContext,
        CancellationToken cancellationToken = default);
}
