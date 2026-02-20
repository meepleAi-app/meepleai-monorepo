namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// Knowledge Base embedding status response.
/// Returned by GET /api/v1/knowledge-base/{gameId}/status.
/// Maps PDF processing state to embedding pipeline stages expected by the frontend.
/// Status values: Pending, Extracting, Chunking, Embedding, Completed, Failed.
/// Issue #4065: RAG readiness polling.
/// </summary>
internal record KnowledgeBaseStatusDto(
    string Status,
    int Progress,
    int TotalChunks,
    int ProcessedChunks,
    string? ErrorMessage,
    string? GameName
);
