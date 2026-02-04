namespace Api.BoundedContexts.KnowledgeBase.Domain.Enums;

/// <summary>
/// Defines the search strategy for agent question answering
/// POC: Agent default behavior with cost/token tracking
/// </summary>
public enum AgentSearchStrategy
{
    /// <summary>
    /// Retrieval-Only: No LLM generation, return raw chunks
    /// Cost: $0.00 | Latency: ~300ms
    /// Use: Test chunk quality, fast responses
    /// </summary>
    RetrievalOnly = 0,

    /// <summary>
    /// Single Model: RAG + LLM synthesis (80% Ollama free, 20% paid)
    /// Cost: $0.00-0.0009 | Latency: ~2-5s
    /// Use: Production question answering with cost optimization
    /// </summary>
    SingleModel = 1,

    /// <summary>
    /// Multi-Model Consensus: RAG + GPT-4 + Claude validation
    /// Cost: ~$0.027 | Latency: ~5-10s
    /// Use: Critical decisions requiring high confidence
    /// </summary>
    MultiModelConsensus = 2
}
