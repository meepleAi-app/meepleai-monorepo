namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Checks if user's question relates to a house rule stored in AgentMemory.
/// Cross-BC read: reads from AgentMemory.IGameMemoryRepository.
/// Returns the most relevant house rule description, or null if none applies.
/// </summary>
internal interface IHouseRuleMatcher
{
    Task<string?> FindMatchingHouseRuleAsync(
        Guid gameId,
        Guid? userId,
        string question,
        CancellationToken ct = default);
}
