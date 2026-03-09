using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Generates progressive summaries of conversation history
/// for maintaining context in long conversations.
/// Issue #5259: Sliding window + conversation summarization.
/// </summary>
internal interface IConversationSummarizer
{
    /// <summary>
    /// Summarizes a batch of messages that are sliding out of the verbatim window.
    /// Merges with any existing summary to create an updated progressive summary.
    /// </summary>
    /// <param name="messagesToSummarize">Messages being evicted from the verbatim window</param>
    /// <param name="existingSummary">Current conversation summary (null if first summarization)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated summary incorporating both old and new context</returns>
    Task<string> SummarizeAsync(
        IReadOnlyList<ChatMessage> messagesToSummarize,
        string? existingSummary,
        CancellationToken cancellationToken = default);
}
