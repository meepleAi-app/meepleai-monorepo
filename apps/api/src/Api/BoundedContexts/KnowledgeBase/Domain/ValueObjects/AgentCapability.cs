namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Describes the capability level of an AI agent based on available knowledge sources.
/// Used to determine whether the agent can operate in full RAG mode or degraded BGG-only mode.
/// </summary>
public sealed record AgentCapability
{
    public AgentCapabilityLevel Level { get; init; }
    public bool HasKbCards { get; init; }
    public bool HasBggMetadata { get; init; }
    public bool HasRulebookAnalysis { get; init; }
    public string Description { get; init; } = string.Empty;

    public static AgentCapability Full(bool hasRulebookAnalysis = false) => new()
    {
        Level = AgentCapabilityLevel.Full,
        HasKbCards = true,
        HasBggMetadata = true,
        HasRulebookAnalysis = hasRulebookAnalysis,
        Description = "Full RAG with knowledge base cards" + (hasRulebookAnalysis ? " and rulebook analysis" : "")
    };

    public static AgentCapability Degraded() => new()
    {
        Level = AgentCapabilityLevel.Degraded,
        HasKbCards = false,
        HasBggMetadata = true,
        HasRulebookAnalysis = false,
        Description = "BGG metadata only — no rulebook knowledge available"
    };

    public static AgentCapability None() => new()
    {
        Level = AgentCapabilityLevel.None,
        HasKbCards = false,
        HasBggMetadata = false,
        HasRulebookAnalysis = false,
        Description = "No knowledge sources available"
    };
}

/// <summary>
/// The capability level of an AI agent.
/// None: no knowledge sources; Degraded: BGG metadata only; Full: KB cards available.
/// </summary>
public enum AgentCapabilityLevel
{
    None,
    Degraded,
    Full
}
