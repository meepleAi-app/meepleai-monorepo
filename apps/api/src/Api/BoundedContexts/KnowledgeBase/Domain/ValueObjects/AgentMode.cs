namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Agent operation modes defining behavior and capabilities.
/// Issue #2391 Sprint 2
/// </summary>
public enum AgentMode
{
    /// <summary>
    /// Standard Q and A mode for rule clarifications.
    /// </summary>
    Chat = 0,

    /// <summary>
    /// Suggests optimal moves based on current game state.
    /// </summary>
    Player = 1,

    /// <summary>
    /// Tracks complete game state from conversation.
    /// </summary>
    Ledger = 2,

    /// <summary>
    /// Resolves disputes between players by citing the rulebook.
    /// Issue #5585: Arbiter Mode.
    /// </summary>
    Arbiter = 3
}
