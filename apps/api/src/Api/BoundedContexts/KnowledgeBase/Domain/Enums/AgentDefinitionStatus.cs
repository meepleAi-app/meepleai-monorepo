namespace Api.BoundedContexts.KnowledgeBase.Domain.Enums;

/// <summary>
/// Lifecycle status for AgentDefinition: Draft -> Testing -> Published.
/// </summary>
public enum AgentDefinitionStatus
{
    /// <summary>
    /// Initial state. Not visible to regular users.
    /// </summary>
    Draft = 0,

    /// <summary>
    /// Under testing by admins. Not visible to regular users.
    /// </summary>
    Testing = 1,

    /// <summary>
    /// Published and visible to all users.
    /// </summary>
    Published = 2
}
