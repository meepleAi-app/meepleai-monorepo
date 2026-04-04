namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// Aggregated RAG readiness status for a shared game across
/// SharedGameCatalog, DocumentProcessing, and KnowledgeBase bounded contexts.
/// </summary>
public record GameRagReadinessDto(
    Guid GameId,
    string GameTitle,
    string GameStatus,
    int TotalDocuments,
    int ReadyDocuments,
    int ProcessingDocuments,
    int FailedDocuments,
    List<DocumentStatusDto> Documents,
    AgentInfoDto? LinkedAgent,
    string OverallReadiness
);

/// <summary>
/// Processing status of a single PDF document in the RAG pipeline.
/// </summary>
public record DocumentStatusDto(
    Guid DocumentId,
    string FileName,
    string ProcessingState,
    int ProgressPercentage,
    bool IsActiveForRag,
    string? ErrorMessage
);

/// <summary>
/// Linked AI agent information for RAG readiness assessment.
/// </summary>
public record AgentInfoDto(
    Guid AgentId,
    string Name,
    string Type,
    bool IsActive,
    bool IsReady
);
