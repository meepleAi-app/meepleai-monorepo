using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.ExportDocumentChunks;

/// <summary>
/// Returns all text chunks for a document with full (non-truncated) content,
/// ordered ascending by <see cref="ExportedChunkDto.ChunkIndex"/>.
/// No pagination — callers receive the complete chunk list for JSON export.
/// Issue #1653: F3-FU-4 — Export document chunks (full content).
/// </summary>
internal sealed record ExportDocumentChunksQuery(Guid PdfDocumentId)
    : IQuery<IReadOnlyList<ExportedChunkDto>>;
