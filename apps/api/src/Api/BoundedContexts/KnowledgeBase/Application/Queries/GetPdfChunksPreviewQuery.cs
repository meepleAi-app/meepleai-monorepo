using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to retrieve chunk previews for a specific PDF document.
/// RAG Sandbox: Chunk preview panel showing indexed text content.
/// </summary>
internal record GetPdfChunksPreviewQuery(
    Guid PdfId,
    int Limit = 20
) : IQuery<IReadOnlyList<ChunkPreviewDto>>;
