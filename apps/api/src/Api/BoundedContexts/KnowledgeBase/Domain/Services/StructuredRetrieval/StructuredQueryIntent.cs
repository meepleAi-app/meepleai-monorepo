namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.StructuredRetrieval;

/// <summary>
/// Represents the classified intent of a user query for structured retrieval routing.
/// Issue #5453: Structured RAG fusion.
/// </summary>
public enum StructuredQueryIntent
{
    /// <summary>General question — use vector search only.</summary>
    General,

    /// <summary>Question about game mechanics (e.g., "What mechanics does Catan use?").</summary>
    Mechanics,

    /// <summary>Question about victory/winning conditions (e.g., "How do you win?").</summary>
    VictoryConditions,

    /// <summary>Question about a glossary term or game concept (e.g., "What is a settlement?").</summary>
    Glossary,

    /// <summary>Question matching a generated FAQ entry (e.g., "Can you trade on your first turn?").</summary>
    Faq
}

/// <summary>
/// Result of query intent classification with confidence.
/// Issue #5453: Structured RAG fusion.
/// </summary>
public sealed record QueryIntentClassification(
    StructuredQueryIntent Intent,
    double Confidence,
    string? MatchedTerm = null);
