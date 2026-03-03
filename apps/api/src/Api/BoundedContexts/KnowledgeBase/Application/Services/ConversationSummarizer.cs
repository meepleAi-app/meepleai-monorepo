using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Models;
using Api.Services;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Uses a lightweight LLM call to progressively summarize conversation history.
/// Issue #5259: Sliding window + conversation summarization.
/// </summary>
internal sealed class ConversationSummarizer : IConversationSummarizer
{
    private readonly ILlmService _llmService;
    private readonly ILogger<ConversationSummarizer> _logger;

    private const string SummarizeSystemPrompt =
        "You are a conversation summarizer. Summarize the conversation concisely, preserving:\n" +
        "- Key topics and game rules discussed\n" +
        "- User preferences and context\n" +
        "- Important facts, decisions, and answered questions\n" +
        "Output ONLY the summary in 2-4 sentences. Do not include any preamble.";

    public ConversationSummarizer(
        ILlmService llmService,
        ILogger<ConversationSummarizer> logger)
    {
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> SummarizeAsync(
        IReadOnlyList<ChatMessage> messagesToSummarize,
        string? existingSummary,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messagesToSummarize);

        if (messagesToSummarize.Count == 0)
            return existingSummary ?? string.Empty;

        try
        {
            var messagesText = string.Join("\n",
                messagesToSummarize.Select(m => $"{m.Role}: {m.Content}"));

            var userPrompt = string.IsNullOrWhiteSpace(existingSummary)
                ? $"Conversation to summarize:\n{messagesText}\n\nSummary:"
                : $"Existing summary:\n{existingSummary}\n\nNew messages to incorporate:\n{messagesText}\n\nUpdated summary:";

            var result = await _llmService.GenerateCompletionAsync(
                SummarizeSystemPrompt,
                userPrompt,
                RequestSource.RagPipeline,
                cancellationToken).ConfigureAwait(false);

            if (!result.Success || string.IsNullOrWhiteSpace(result.Response))
            {
                _logger.LogWarning("Conversation summarization failed: {Error}", result.ErrorMessage);
                return existingSummary ?? string.Empty;
            }

            var summary = result.Response.Trim();
            _logger.LogInformation(
                "Summarized {MessageCount} messages into {SummaryLength} chars",
                messagesToSummarize.Count, summary.Length);

            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Conversation summarization threw exception");
            return existingSummary ?? string.Empty;
        }
    }
}
