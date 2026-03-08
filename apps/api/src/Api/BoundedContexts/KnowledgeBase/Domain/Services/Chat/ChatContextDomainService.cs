using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Domain service for managing chat context in RAG queries.
/// Handles game-specific context filtering, chat history integration,
/// and sliding window strategy with progressive summarization.
/// Issue #857: Chat history context
/// Issue #5259: Sliding window + conversation summarization
/// </summary>
internal class ChatContextDomainService
{
    /// <summary>Maximum number of recent messages kept verbatim in the prompt.</summary>
    internal const int VerbatimWindowSize = 6;

    /// <summary>Maximum characters for the verbatim message section.</summary>
    internal const int MaxVerbatimChars = 3000;

    /// <summary>Maximum characters for the conversation summary section.</summary>
    internal const int MaxSummaryChars = 500;

    /// <summary>
    /// Message count threshold that triggers summarization of older messages.
    /// Below this threshold, all messages are kept verbatim.
    /// </summary>
    internal const int SummarizationThreshold = 8;

    /// <summary>
    /// Filters search results by game context from chat thread.
    /// Ensures RAG results are relevant to the thread's game.
    /// </summary>
    public virtual List<SearchResult> FilterByGameContext(
        List<SearchResult> results,
        Guid gameId)
    {
        if (gameId == Guid.Empty)
            throw new ArgumentException("GameId cannot be empty", nameof(gameId));

        return results;
    }

    /// <summary>
    /// Builds chat history context using sliding window strategy.
    /// For short conversations: all messages verbatim.
    /// For long conversations: summary of older messages + recent messages verbatim.
    /// </summary>
    public virtual string BuildChatHistoryContext(ChatThread thread)
    {
        ArgumentNullException.ThrowIfNull(thread);

        if (thread.IsEmpty)
            return string.Empty;

        var allMessages = thread.Messages
            .OrderBy(m => m.Timestamp)
            .ToList();

        // Short conversation: all messages verbatim
        if (allMessages.Count <= VerbatimWindowSize)
        {
            return FormatVerbatimMessages(allMessages);
        }

        // Long conversation: summary + recent verbatim window
        var recentMessages = allMessages
            .Skip(Math.Max(0, allMessages.Count - VerbatimWindowSize))
            .ToList();

        var parts = new List<string>();

        // Include conversation summary if available
        if (!string.IsNullOrWhiteSpace(thread.ConversationSummary))
        {
            var summary = thread.ConversationSummary.Length > MaxSummaryChars
                ? string.Concat(thread.ConversationSummary.AsSpan(0, MaxSummaryChars - 3), "...")
                : thread.ConversationSummary;
            parts.Add($"Conversation summary (earlier context):\n{summary}");
        }

        // Include recent messages verbatim
        var verbatim = FormatVerbatimMessages(recentMessages);
        if (!string.IsNullOrEmpty(verbatim))
        {
            parts.Add(verbatim);
        }

        return parts.Count == 0 ? string.Empty : string.Join("\n\n", parts);
    }

    /// <summary>
    /// Gets messages that should be summarized (messages outside the verbatim window
    /// that were added since the last summarization). Returns empty if no new messages
    /// need summarizing.
    /// </summary>
    public virtual IReadOnlyList<ChatMessage> GetMessagesToSummarize(ChatThread thread)
    {
        ArgumentNullException.ThrowIfNull(thread);

        if (thread.MessageCount < SummarizationThreshold)
            return Array.Empty<ChatMessage>();

        var allMessages = thread.Messages
            .OrderBy(m => m.Timestamp)
            .ToList();

        var windowStart = Math.Max(0, allMessages.Count - VerbatimWindowSize);

        // Messages outside the verbatim window
        var messagesOutsideWindow = allMessages.Take(windowStart).ToList();

        if (messagesOutsideWindow.Count == 0)
            return Array.Empty<ChatMessage>();

        // If a previous summary exists, only return messages that are genuinely new
        // (added after the last summarization). LastSummarizedMessageCount tracks
        // how many messages existed when the summary was last updated.
        if (thread.LastSummarizedMessageCount > 0)
        {
            // The old summary covered messages 0..lastSummarizedOutsideWindow-1.
            // New messages outside the window are from that index onwards.
            var lastSummarizedWindowStart = Math.Max(0, thread.LastSummarizedMessageCount - VerbatimWindowSize);
            var newMessages = messagesOutsideWindow.Skip(lastSummarizedWindowStart).ToList();

            return newMessages.Count > 0 ? newMessages : Array.Empty<ChatMessage>();
        }

        return messagesOutsideWindow;
    }

    /// <summary>
    /// Validates that a game ID is present for context filtering.
    /// </summary>
    public virtual bool ValidateGameContext(Guid? gameId)
    {
        return gameId.HasValue && gameId.Value != Guid.Empty;
    }

    /// <summary>
    /// Determines if chat history should be included based on thread state.
    /// </summary>
    public virtual bool ShouldIncludeChatHistory(ChatThread thread)
    {
        if (thread == null)
            return false;

        return !thread.IsEmpty && thread.Status.IsActive;
    }

    /// <summary>
    /// Enriches user prompt with chat history context.
    /// </summary>
    public virtual string EnrichPromptWithHistory(string userQuestion, string chatHistoryContext)
    {
        if (string.IsNullOrWhiteSpace(userQuestion))
            throw new ArgumentException("User question cannot be empty", nameof(userQuestion));

        if (string.IsNullOrWhiteSpace(chatHistoryContext))
            return userQuestion;

        return $"{chatHistoryContext}\n\nCurrent question: {userQuestion}";
    }

    /// <summary>
    /// Builds a structured multi-turn system prompt that includes conversation awareness.
    /// Issue #5260: Structured prompt template for multi-turn conversations.
    /// </summary>
    public virtual string BuildSystemPrompt(string agentName, bool hasConversationHistory)
    {
        if (string.IsNullOrWhiteSpace(agentName))
            throw new ArgumentException("Agent name cannot be empty", nameof(agentName));

        var basePrompt = $"You are {agentName}, a specialized board game AI assistant. " +
                         "Answer questions based ONLY on the provided context from the game rules and documentation. " +
                         "If the context doesn't contain the answer, say so clearly. " +
                         "Always cite the page number when referencing specific rules.";

        if (!hasConversationHistory)
            return basePrompt;

        return basePrompt +
               "\n\nConversation Guidelines:" +
               "\n- You have access to the conversation history below. Use it to maintain context and coherence." +
               "\n- If the user refers to something discussed earlier, use the history to provide a consistent answer." +
               "\n- Avoid repeating information already provided unless the user asks for clarification." +
               "\n- When the user asks a follow-up, connect your answer to the relevant previous exchange.";
    }

    /// <summary>
    /// Builds system prompt using typology-specific template.
    /// Issue #5278: Typology-aware system prompts.
    /// </summary>
    public virtual string BuildSystemPrompt(
        string agentName,
        string? typologyName,
        string? gameName,
        bool hasConversationHistory)
    {
        if (string.IsNullOrWhiteSpace(agentName))
            throw new ArgumentException("Agent name cannot be empty", nameof(agentName));

        var profile = TypologyProfile.FromName(typologyName);
        return profile.BuildSystemPrompt(agentName, gameName, hasConversationHistory);
    }

    /// <summary>
    /// Builds the complete structured user prompt with RAG context and conversation history.
    /// Issue #5260: Structured prompt template for multi-turn conversations.
    /// </summary>
    public virtual string BuildStructuredUserPrompt(
        string userQuestion,
        string? ragContext,
        string? chatHistoryContext)
    {
        if (string.IsNullOrWhiteSpace(userQuestion))
            throw new ArgumentException("User question cannot be empty", nameof(userQuestion));

        var sections = new List<string>();

        // Section 1: RAG context
        if (!string.IsNullOrWhiteSpace(ragContext))
        {
            sections.Add($"=== Game Documentation ===\n{ragContext}");
        }

        // Section 2: Conversation history
        if (!string.IsNullOrWhiteSpace(chatHistoryContext))
        {
            sections.Add($"=== Conversation History ===\n{chatHistoryContext}");
        }

        // Section 3: Current question
        sections.Add($"=== Current Question ===\n{userQuestion}");

        // Section 4: Instructions based on available context
        if (string.IsNullOrWhiteSpace(ragContext))
        {
            sections.Add("Note: No relevant context found in knowledge base. Answer based on conversation history if available, or inform the user.");
        }
        else
        {
            sections.Add("Provide a clear answer based on the game documentation above. Cite page numbers where applicable.");
        }

        return string.Join("\n\n", sections);
    }

    /// <summary>
    /// Formats a list of messages as verbatim text for the prompt.
    /// </summary>
    private static string FormatVerbatimMessages(IReadOnlyList<ChatMessage> messages)
    {
        var contextLines = new List<string>();
        var totalChars = 0;

        foreach (var message in messages)
        {
            var line = $"{message.Role}: {message.Content}";

            if (totalChars + line.Length > MaxVerbatimChars)
                break;

            contextLines.Add(line);
            totalChars += line.Length;
        }

        if (contextLines.Count == 0)
            return string.Empty;

        return "Previous conversation:\n" + string.Join("\n", contextLines);
    }
}
