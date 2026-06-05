namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetDocumentEmbeddingsMeta;

/// <summary>
/// Per-document embeddings metadata for admin viewer (Issue #1674).
/// Whitelist-only DTO: zero raw vector values exposed (Newman corpus-reconstruction risk mitigation).
/// </summary>
internal sealed record DocumentEmbeddingsMetaDto(
    Guid DocId,
    string Model,
    int Dimensions,
    int TotalChunks,
    DateTime? IndexedAt,
    string? Language);
