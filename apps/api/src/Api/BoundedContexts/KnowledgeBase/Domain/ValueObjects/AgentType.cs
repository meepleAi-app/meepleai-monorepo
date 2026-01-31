namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Value object representing the classification of an AI agent.
/// </summary>
/// <remarks>
/// AgentType determines the agent's primary capability and use case.
/// Design Decision (ADR-004): Knowledge-domain agents reside in KnowledgeBase context.
/// </remarks>
internal sealed record AgentType
{
    /// <summary>
    /// RAG agent: Hybrid search + LLM generation for general questions.
    /// </summary>
    public static AgentType RagAgent => new("RAG", "Retrieval-Augmented Generation for general game rules questions");

    /// <summary>
    /// Citation agent: Source validation and attribution.
    /// </summary>
    public static AgentType CitationAgent => new("Citation", "Validates sources and formats citations");

    /// <summary>
    /// Confidence agent: Multi-layer quality assessment.
    /// </summary>
    public static AgentType ConfidenceAgent => new("Confidence", "Assesses answer confidence and quality");

    /// <summary>
    /// Rules interpreter agent: Game rules semantic search and interpretation.
    /// </summary>
    public static AgentType RulesInterpreter => new("RulesInterpreter", "Specialized in game rules interpretation");

    /// <summary>
    /// Conversation agent: Chat thread management and context assembly.
    /// </summary>
    public static AgentType ConversationAgent => new("Conversation", "Manages chat threads and conversation context");

    public string Value { get; }
    public string Description { get; }

    private AgentType(string value, string description)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("AgentType value cannot be empty", nameof(value));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("AgentType description cannot be empty", nameof(description));

        Value = value;
        Description = description;
    }

    /// <summary>
    /// Creates a custom agent type (for extensibility).
    /// </summary>
    public static AgentType Custom(string value, string description)
        => new(value, description);

    /// <summary>
    /// Parses an agent type from string value.
    /// </summary>
    public static AgentType Parse(string value)
    {
        return value?.ToUpperInvariant() switch
        {
            "RAG" => RagAgent,
            "CITATION" => CitationAgent,
            "CONFIDENCE" => ConfidenceAgent,
            "RULESINTERPRETER" => RulesInterpreter,
            "CONVERSATION" => ConversationAgent,
            _ => throw new ArgumentException($"Unknown AgentType: {value}", nameof(value))
        };
    }

    /// <summary>
    /// Tries to parse an agent type from string value.
    /// </summary>
    public static bool TryParse(string value, out AgentType? agentType)
    {
        try
        {
            agentType = Parse(value);
            return true;
        }
        catch
        {
            agentType = null;
            return false;
        }
    }

    public override string ToString() => Value;
}
