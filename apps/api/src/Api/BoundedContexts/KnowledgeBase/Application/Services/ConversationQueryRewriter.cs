using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Models;
using Api.Services;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Uses a lightweight LLM call to rewrite ambiguous follow-up queries
/// into standalone questions for improved RAG vector search.
/// Issue #5258: Conversation-aware query rewriting.
/// </summary>
internal sealed class ConversationQueryRewriter : IConversationQueryRewriter
{
    private readonly ILlmService _llmService;
    private readonly ILogger<ConversationQueryRewriter> _logger;

    private const string RewriteSystemPrompt =
        "You are a query rewriter. Given a conversation history and a follow-up question, " +
        "rewrite the follow-up question as a standalone question that includes all necessary context. " +
        "Output ONLY the rewritten question, nothing else. " +
        "If the question is already standalone, return it unchanged.";

    public ConversationQueryRewriter(
        ILlmService llmService,
        ILogger<ConversationQueryRewriter> logger)
    {
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> RewriteQueryAsync(
        string currentQuery,
        string chatHistoryContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentQuery);

        // Skip rewriting if no conversation history
        if (string.IsNullOrWhiteSpace(chatHistoryContext))
            return currentQuery;

        try
        {
            var userPrompt = $"Conversation history:\n{chatHistoryContext}\n\nFollow-up question: {currentQuery}\n\nStandalone question:";

            var result = await _llmService.GenerateCompletionAsync(
                RewriteSystemPrompt,
                userPrompt,
                RequestSource.RagPipeline,
                cancellationToken).ConfigureAwait(false);

            if (!result.Success || string.IsNullOrWhiteSpace(result.Response))
            {
                _logger.LogWarning("Query rewriting failed, using original query. Error: {Error}", result.ErrorMessage);
                return currentQuery;
            }

            var rewritten = result.Response.Trim();

            // Sanity check: rewritten query should not be empty or excessively long.
            // Use a minimum floor of 200 chars so short follow-up queries like "Why?"
            // or "How?" can be expanded into proper standalone questions.
            var maxRewriteLength = Math.Max(200, currentQuery.Length * 5);
            if (rewritten.Length == 0 || rewritten.Length > maxRewriteLength)
            {
                _logger.LogWarning("Query rewriting produced suspicious result (length={Length}, max={Max}), using original",
                    rewritten.Length, maxRewriteLength);
                return currentQuery;
            }

            _logger.LogDebug(
                "Query rewritten: original={OriginalLength} chars → rewritten={RewrittenLength} chars",
                currentQuery.Length, rewritten.Length);

            return rewritten;
        }
        catch (Exception ex)
        {
            // Fail-safe: never block the pipeline because of rewriting failure
            _logger.LogWarning(ex, "Query rewriting threw exception, using original query");
            return currentQuery;
        }
    }
}
