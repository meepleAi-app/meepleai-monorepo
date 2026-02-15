namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.MultiAgentRouter;

/// <summary>
/// Classifies user query intent for multi-agent routing.
/// Issue #4336: Multi-Agent Router - Intent Classification.
/// </summary>
internal sealed class IntentClassifier
{
    public (AgentIntent intent, double confidence) ClassifyQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return (AgentIntent.Unknown, 0.0);

        var lowerQuery = query.ToLowerInvariant();

        // Move Validation (Arbitro) - Highest priority for action queries
        if (ContainsAny(lowerQuery, ["validate", "is this move", "can i move", "allowed to move", "legal move"]))
            return (AgentIntent.MoveValidation, 0.95);

        // Strategic Analysis (Decisore)
        if (ContainsAny(lowerQuery, ["suggest move", "best move", "what should i do", "analyze position", "strategy"]))
            return (AgentIntent.StrategicAnalysis, 0.90);

        // Rules Question (Tutor)
        if (ContainsAny(lowerQuery, ["rule", "how do i", "setup", "phase", "turn order", "allowed to"]))
            return (AgentIntent.RulesQuestion, 0.85);

        // Tutorial (Tutor)
        if (ContainsAny(lowerQuery, ["learn", "tutorial", "how to play", "explain", "teach me"]))
            return (AgentIntent.Tutorial, 0.90);

        // Default: unclear intent
        return (AgentIntent.Unknown, 0.50);
    }

    private static bool ContainsAny(string text, string[] keywords)
    {
        return keywords.Any(k => text.Contains(k, StringComparison.Ordinal));
    }
}

/// <summary>
/// Agent intent types for routing decisions.
/// </summary>
internal enum AgentIntent
{
    Unknown,
    Tutorial,
    RulesQuestion,
    MoveValidation,
    StrategicAnalysis
}
