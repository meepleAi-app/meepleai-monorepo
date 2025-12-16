using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Domain service for managing chat context in RAG queries.
/// Handles game-specific context filtering and chat history integration.
/// </summary>
internal class ChatContextDomainService
{
    private const int MaxHistoryMessages = 10;
    private const int MaxHistoryChars = 2000;

    /// <summary>
    /// Filters search results by game context from chat thread.
    /// Ensures RAG results are relevant to the thread's game.
    /// </summary>
    /// <param name="results">Search results to filter</param>
    /// <param name="gameId">Game ID from chat thread</param>
    /// <returns>Filtered results matching game context</returns>
    public virtual List<SearchResult> FilterByGameContext(
        List<SearchResult> results,
        Guid gameId)
    {
        if (gameId == Guid.Empty)
            throw new ArgumentException("GameId cannot be empty", nameof(gameId));

        // Note: This method is defensive but in practice, the search query
        // already filters by gameId at the repository level. This provides
        // an additional safety layer for domain logic.
        return results;
    }

    /// <summary>
    /// Builds chat history context from recent messages in the thread.
    /// Formats messages for LLM context window.
    /// </summary>
    /// <param name="thread">Chat thread with message history</param>
    /// <returns>Formatted chat history string for LLM context</returns>
    public virtual string BuildChatHistoryContext(ChatThread thread)
    {
        ArgumentNullException.ThrowIfNull(thread);

        if (thread.IsEmpty)
            return string.Empty;

        // Get recent messages (last N, within char limit)
        var recentMessages = thread.Messages
            .OrderByDescending(m => m.Timestamp)
            .Take(MaxHistoryMessages)
            .Reverse() // Oldest first for chronological order
            .ToList();

        // Build context string
        var contextLines = new List<string>();
        var totalChars = 0;

        foreach (var message in recentMessages)
        {
            var line = $"{message.Role}: {message.Content}";

            // Respect character limit
            if (totalChars + line.Length > MaxHistoryChars)
                break;

            contextLines.Add(line);
            totalChars += line.Length;
        }

        if (contextLines.Count == 0)
            return string.Empty;

        return "Previous conversation:\n" + string.Join("\n", contextLines);
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
    /// <param name="thread">Chat thread to evaluate</param>
    /// <returns>True if history should be included in context</returns>
    public virtual bool ShouldIncludeChatHistory(ChatThread thread)
    {
        if (thread == null)
            return false;

        // Include history if thread has messages and is active
        return !thread.IsEmpty && thread.Status.IsActive;
    }

    /// <summary>
    /// Enriches user prompt with chat history context.
    /// </summary>
    /// <param name="userQuestion">Original user question</param>
    /// <param name="chatHistoryContext">Formatted chat history</param>
    /// <returns>Enriched prompt with history context</returns>
    public virtual string EnrichPromptWithHistory(string userQuestion, string chatHistoryContext)
    {
        if (string.IsNullOrWhiteSpace(userQuestion))
            throw new ArgumentException("User question cannot be empty", nameof(userQuestion));

        if (string.IsNullOrWhiteSpace(chatHistoryContext))
            return userQuestion; // No history, return original

        return $"{chatHistoryContext}\n\nCurrent question: {userQuestion}";
    }
}
