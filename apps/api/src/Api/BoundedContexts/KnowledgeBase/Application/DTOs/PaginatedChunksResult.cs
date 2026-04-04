namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// Paginated result for chunk previews.
/// RAG Sandbox: Supports chunk preview panel with pagination and search.
/// </summary>
internal record PaginatedChunksResult(
    IReadOnlyList<ChunkPreviewDto> Chunks,
    int Total,
    int Page,
    int PageSize
);
