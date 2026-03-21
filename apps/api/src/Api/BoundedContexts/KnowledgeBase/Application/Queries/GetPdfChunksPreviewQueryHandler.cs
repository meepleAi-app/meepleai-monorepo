using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Handles retrieving paginated chunk previews for a PDF document.
/// Traverses PdfDocument -> VectorDocument -> Embeddings to gather text chunks.
/// RAG Sandbox: Chunk preview panel with pagination and search support.
/// </summary>
internal class GetPdfChunksPreviewQueryHandler
    : IQueryHandler<GetPdfChunksPreviewQuery, PaginatedChunksResult>
{
    private readonly IPdfDocumentRepository _pdfDocumentRepository;
    private readonly IVectorDocumentRepository _vectorDocumentRepository;
    private readonly IEmbeddingRepository _embeddingRepository;

    public GetPdfChunksPreviewQueryHandler(
        IPdfDocumentRepository pdfDocumentRepository,
        IVectorDocumentRepository vectorDocumentRepository,
        IEmbeddingRepository embeddingRepository)
    {
        _pdfDocumentRepository = pdfDocumentRepository ?? throw new ArgumentNullException(nameof(pdfDocumentRepository));
        _vectorDocumentRepository = vectorDocumentRepository ?? throw new ArgumentNullException(nameof(vectorDocumentRepository));
        _embeddingRepository = embeddingRepository ?? throw new ArgumentNullException(nameof(embeddingRepository));
    }

    public async Task<PaginatedChunksResult> Handle(
        GetPdfChunksPreviewQuery query,
        CancellationToken cancellationToken)
    {
        var emptyResult = new PaginatedChunksResult(
            Chunks: Array.Empty<ChunkPreviewDto>(),
            Total: 0,
            Page: query.Page,
            PageSize: query.PageSize);

        // 1. Get the PDF document to find its GameId
        var pdfDoc = await _pdfDocumentRepository.GetByIdAsync(query.PdfId, cancellationToken).ConfigureAwait(false);
        if (pdfDoc is null)
        {
            return emptyResult;
        }

        // 2. Find the VectorDocument linked to this PDF
        var vectorDoc = await _vectorDocumentRepository.GetByGameAndSourceAsync(
            pdfDoc.GameId,
            pdfDoc.Id,
            cancellationToken).ConfigureAwait(false);

        if (vectorDoc is null)
        {
            return emptyResult;
        }

        // 3. Get all embeddings (which contain the text chunks) for this VectorDocument
        var embeddings = await _embeddingRepository.GetByVectorDocumentIdAsync(
            vectorDoc.Id,
            cancellationToken).ConfigureAwait(false);

        // 4. Apply optional search filter (case-insensitive)
        var filtered = string.IsNullOrWhiteSpace(query.Search)
            ? embeddings
            : embeddings
                .Where(e => e.TextContent.Contains(query.Search, StringComparison.OrdinalIgnoreCase))
                .ToList();

        // 5. Order by chunk index and count total before pagination
        var ordered = filtered.OrderBy(e => e.ChunkIndex);
        var total = filtered.Count;

        // 6. Apply pagination
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var chunks = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new ChunkPreviewDto(
                EmbeddingId: e.Id,
                TextContent: e.TextContent,
                ChunkIndex: e.ChunkIndex,
                PageNumber: e.PageNumber,
                Model: e.Model,
                CreatedAt: e.CreatedAt
            ))
            .ToList();

        return new PaginatedChunksResult(
            Chunks: chunks,
            Total: total,
            Page: page,
            PageSize: pageSize);
    }
}
