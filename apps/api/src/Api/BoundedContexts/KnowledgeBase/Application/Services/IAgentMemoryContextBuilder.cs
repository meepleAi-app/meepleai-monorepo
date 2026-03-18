namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Builds agent memory context (house rules, group preferences, notes) for injection
/// into RAG chat system prompts during active sessions.
/// </summary>
internal interface IAgentMemoryContextBuilder
{
    /// <summary>
    /// Builds the agent memory context string to append to the system prompt.
    /// Returns null if the feature is disabled or no memory data exists.
    /// </summary>
    Task<string?> BuildContextAsync(Guid gameId, Guid ownerId, Guid? groupId, CancellationToken ct);
}
