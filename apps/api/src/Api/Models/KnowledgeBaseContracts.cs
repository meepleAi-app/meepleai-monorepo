

#pragma warning disable MA0048 // File name must match type name - Contains related domain models
namespace Api.Models;

/// <summary>
/// DDD-PHASE3: Request model for knowledge base Q&amp;A.
/// Simplified version without chatId (use existing /agents/qa for chat integration).
/// </summary>
internal record KnowledgeBaseAskRequest(
    string gameId,
    string query,
    string? language = null,
    bool? bypassCache = null
);
