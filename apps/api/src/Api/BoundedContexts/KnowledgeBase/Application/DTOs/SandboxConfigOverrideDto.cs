namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// Sandbox configuration override for RAG pipeline parameters.
/// Applied during admin sandbox debug chat sessions.
/// </summary>
internal record SandboxConfigOverrideDto(
    string? Strategy = null,
    double? DenseWeight = null,
    int? TopK = null,
    bool? RerankingEnabled = null,
    double? Temperature = null,
    int? MaxTokens = null,
    string? Model = null,
    string? SystemPromptOverride = null,
    string? ChunkingStrategy = null,
    int? ChunkSize = null,
    int? ChunkOverlap = null
);
