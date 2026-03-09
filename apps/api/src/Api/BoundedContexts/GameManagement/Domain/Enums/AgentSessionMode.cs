namespace Api.BoundedContexts.GameManagement.Domain.Enums;

/// <summary>
/// Mode of AI agent participation in a live game session.
/// </summary>
public enum AgentSessionMode
{
    /// <summary>
    /// No AI agent active in this session.
    /// </summary>
    None = 0,

    /// <summary>
    /// AI assists with rules clarification and suggestions.
    /// </summary>
    Assistant = 1,

    /// <summary>
    /// AI acts as game master, managing turns and enforcing rules.
    /// </summary>
    GameMaster = 2,

    /// <summary>
    /// AI resolves disputes between players by citing the rulebook.
    /// Issue #5585: Arbiter Mode.
    /// </summary>
    Arbiter = 3
}
