using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Domain service for building enhanced agent prompts with game state context.
/// Issue #3184 (AGT-010): Session-Based Agent Lifecycle.
/// </summary>
internal interface IAgentPromptBuilder
{
    /// <summary>
    /// Builds an enhanced prompt with game state context injection.
    /// </summary>
    /// <param name="typologyName">Name of the agent typology (e.g., "Rules Expert")</param>
    /// <param name="gameTitle">Title of the game</param>
    /// <param name="gameState">Current game state</param>
    /// <param name="userQuestion">User's question</param>
    /// <returns>Enhanced prompt with context injection</returns>
    string BuildSessionPrompt(
        string typologyName,
        string gameTitle,
        GameState gameState,
        string userQuestion);
}
