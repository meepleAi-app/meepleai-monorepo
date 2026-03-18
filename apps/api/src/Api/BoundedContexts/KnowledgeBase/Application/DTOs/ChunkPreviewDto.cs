namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// DTO for a text chunk preview from a PDF document's embeddings.
/// RAG Sandbox: Chunk preview panel for inspecting indexed content.
/// </summary>
internal record ChunkPreviewDto(
    Guid EmbeddingId,
    string TextContent,
    int ChunkIndex,
    int PageNumber,
    string Model,
    DateTime CreatedAt
);
