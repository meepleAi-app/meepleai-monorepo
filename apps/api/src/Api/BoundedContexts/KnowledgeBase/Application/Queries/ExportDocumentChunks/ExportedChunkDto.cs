namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.ExportDocumentChunks;

/// <summary>
/// Represents a single text chunk with full (non-truncated) content for JSON export.
/// Issue #1653: F3-FU-4 — Export document chunks (full content).
/// </summary>
public sealed record ExportedChunkDto(
    Guid Id,
    int ChunkIndex,
    int? PageNumber,
    string? Heading,
    string Content);
