using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to retrieve chunk previews for a specific PDF document with pagination and search.
/// RAG Sandbox: Chunk preview panel showing indexed text content.
/// </summary>
internal record GetPdfChunksPreviewQuery(
    Guid PdfId,
    int Page = 1,
    int PageSize = 20,
    string? Search = null
) : IQuery<PaginatedChunksResult>;
