using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// Response for agent question answering with token/cost tracking
/// POC: Agent default behavior evaluation
/// </summary>
public record AgentChatResponse
{
    /// <summary>
    /// Strategy used for this query
    /// </summary>
    public required AgentSearchStrategy Strategy { get; init; }

    /// <summary>
    /// Human-readable strategy description
    /// </summary>
    public required string StrategyDescription { get; init; }

    /// <summary>
    /// Generated answer (null for RetrievalOnly strategy)
    /// </summary>
    public string? Answer { get; init; }

    /// <summary>
    /// Retrieved code/document chunks from RAG
    /// </summary>
    public required List<CodeChunkDto> RetrievedChunks { get; init; }

    /// <summary>
    /// Token usage statistics
    /// </summary>
    public required TokenUsageDto TokenUsage { get; init; }

    /// <summary>
    /// Cost breakdown in USD
    /// </summary>
    public required CostBreakdownDto CostBreakdown { get; init; }

    /// <summary>
    /// Total latency in milliseconds
    /// </summary>
    public required int LatencyMs { get; init; }

    /// <summary>
    /// Session ID for conversation tracking
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Query timestamp (UTC)
    /// </summary>
    public required DateTime Timestamp { get; init; }

    /// <summary>
    /// Content access level for the current user.
    /// FullAccess = owns the game (sees full text/images).
    /// ReferenceOnly = doesn't own the game (sees metadata only).
    /// </summary>
    public ContentAccessLevel ContentAccessLevel { get; init; } = ContentAccessLevel.FullAccess;

    /// <summary>
    /// Whether the user has full access to source content.
    /// Convenience property for frontend consumption.
    /// </summary>
    public bool HasFullAccess => ContentAccessLevel == ContentAccessLevel.FullAccess;
}

/// <summary>
/// Token usage statistics
/// </summary>
public record TokenUsageDto
{
    /// <summary>
    /// Tokens in the prompt/input
    /// </summary>
    public required int PromptTokens { get; init; }

    /// <summary>
    /// Tokens in the completion/output
    /// </summary>
    public required int CompletionTokens { get; init; }

    /// <summary>
    /// Total tokens (prompt + completion + embedding)
    /// </summary>
    public required int TotalTokens { get; init; }

    /// <summary>
    /// Tokens used for embedding generation
    /// </summary>
    public required int EmbeddingTokens { get; init; }

    /// <summary>
    /// Creates empty token usage (zero cost scenario)
    /// </summary>
    public static TokenUsageDto Empty => new()
    {
        PromptTokens = 0,
        CompletionTokens = 0,
        TotalTokens = 0,
        EmbeddingTokens = 0
    };
}

/// <summary>
/// Cost breakdown in USD
/// </summary>
public record CostBreakdownDto
{
    /// <summary>
    /// Cost for embedding generation (always $0 for local service)
    /// </summary>
    public required decimal EmbeddingCost { get; init; }

    /// <summary>
    /// Cost for vector search (always $0 for local Qdrant)
    /// </summary>
    public required decimal VectorSearchCost { get; init; }

    /// <summary>
    /// Cost for LLM inference ($0 for Ollama, $$$ for paid models)
    /// </summary>
    public required decimal LlmCost { get; init; }

    /// <summary>
    /// Total cost in USD
    /// </summary>
    public required decimal TotalCost { get; init; }

    /// <summary>
    /// Provider used (Local, Ollama, OpenRouter)
    /// </summary>
    public required string Provider { get; init; }

    /// <summary>
    /// Model identifier (e.g., llama-3.3-70b-versatile, gpt-4o-mini)
    /// </summary>
    public string? ModelUsed { get; init; }

    /// <summary>
    /// Creates zero-cost breakdown
    /// </summary>
    public static CostBreakdownDto Zero(string provider = "Local") => new()
    {
        EmbeddingCost = 0,
        VectorSearchCost = 0,
        LlmCost = 0,
        TotalCost = 0,
        Provider = provider,
        ModelUsed = null
    };
}

/// <summary>
/// Retrieved code/document chunk with relevance score
/// </summary>
public record CodeChunkDto
{
    /// <summary>
    /// File path or document identifier
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Starting line number (or page number for PDFs)
    /// </summary>
    public required int StartLine { get; init; }

    /// <summary>
    /// Ending line number (or page number for PDFs)
    /// </summary>
    public required int EndLine { get; init; }

    /// <summary>
    /// Code or text content preview
    /// </summary>
    public required string CodePreview { get; init; }

    /// <summary>
    /// Relevance score (0-1, from vector similarity)
    /// </summary>
    public required double RelevanceScore { get; init; }

    /// <summary>
    /// Bounded context or category
    /// </summary>
    public required string BoundedContext { get; init; }

    /// <summary>
    /// Chunk index in the document
    /// </summary>
    public required int ChunkIndex { get; init; }
}
