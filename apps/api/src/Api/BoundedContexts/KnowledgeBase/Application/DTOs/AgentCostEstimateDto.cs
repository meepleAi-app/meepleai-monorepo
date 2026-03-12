namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// DTO for pre-chat agent cost estimation.
/// Provides token and cost estimates before an admin starts a RAG chat session.
/// </summary>
internal record AgentCostEstimateDto(
    int TotalChunks,
    int EstimatedEmbeddingTokens,
    decimal EstimatedCostPerQuery,
    string Currency,
    string Model,
    string Note
);
