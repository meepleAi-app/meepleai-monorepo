using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.Services;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Provides stateless helper operations for the agent chat pipeline:
/// RAG context formatting, model fallback selection, typology resolution,
/// and strategy tier mapping.
///
/// Extracted from SendAgentMessageCommandHandler to reduce handler size
/// and enable reuse across agent chat flows.
/// </summary>
internal interface IAgentChatHelperService
{
    /// <summary>
    /// Builds a formatted context string from retrieved RAG chunks for inclusion
    /// in the LLM prompt. Returns an empty string when no chunks are provided.
    /// </summary>
    string BuildContextFromChunks(IReadOnlyList<SearchResultItem> chunks);

    /// <summary>
    /// Selects a fallback LLM model when the primary model fails.
    /// Free-tier models fall back to Ollama; paid-tier models prefer an
    /// alternative same-tier model from a different provider.
    /// </summary>
    string? GetFallbackModel(string failedModel);

    /// <summary>
    /// Maps an agent's backend type value to the user-facing typology name
    /// (e.g. "RAG" → "Tutor", "RulesInterpreter" → "Arbitro").
    /// Returns null for custom or unknown types.
    /// </summary>
    string? ResolveTypologyName(Agent agent);

    /// <summary>
    /// Resolves the game name for a given agent asynchronously.
    /// Returns null when the agent has no associated game.
    /// </summary>
    Task<string?> ResolveGameNameAsync(Agent agent, CancellationToken cancellationToken);

    /// <summary>
    /// Maps the model tier to a user-facing strategy tier label.
    /// Issue #5481: ResponseMetaBadge — soft quality badge on AI responses.
    /// </summary>
    string ResolveStrategyTier(string modelId);
}
