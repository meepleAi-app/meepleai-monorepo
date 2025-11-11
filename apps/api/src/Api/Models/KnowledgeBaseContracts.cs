namespace Api.Models;

/// <summary>
/// DDD-PHASE3: Request model for knowledge base Q&A.
/// Simplified version without chatId (use existing /agents/qa for chat integration).
/// </summary>
public record KnowledgeBaseAskRequest(
    string gameId,
    string query,
    string? language = null,
    bool? bypassCache = null
);
