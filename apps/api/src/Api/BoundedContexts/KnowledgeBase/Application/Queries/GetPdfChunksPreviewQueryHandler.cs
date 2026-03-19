using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handles retrieving chunk previews for a PDF document.
/// Traverses PdfDocument -> VectorDocument -> Embeddings to gather text chunks.
/// RAG Sandbox: Chunk preview panel.
/// </summary>
internal class GetPdfChunksPreviewQueryHandler
    : IQueryHandler<GetPdfChunksPreviewQuery, IReadOnlyList<ChunkPreviewDto>>
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

    public async Task<IReadOnlyList<ChunkPreviewDto>> Handle(
        GetPdfChunksPreviewQuery query,
        CancellationToken cancellationToken)
    {
        // 1. Get the PDF document to find its GameId
        var pdfDoc = await _pdfDocumentRepository.GetByIdAsync(query.PdfId, cancellationToken).ConfigureAwait(false);
        if (pdfDoc is null)
        {
            return Array.Empty<ChunkPreviewDto>();
        }

        // 2. Find the VectorDocument linked to this PDF
        var vectorDoc = await _vectorDocumentRepository.GetByGameAndSourceAsync(
            pdfDoc.GameId,
            pdfDoc.Id,
            cancellationToken).ConfigureAwait(false);

        if (vectorDoc is null)
        {
            return Array.Empty<ChunkPreviewDto>();
        }

        // 3. Get embeddings (which contain the text chunks) for this VectorDocument
        var embeddings = await _embeddingRepository.GetByVectorDocumentIdAsync(
            vectorDoc.Id,
            cancellationToken).ConfigureAwait(false);

        // 4. Map to DTOs, ordered by chunk index, limited by query parameter
        var limit = Math.Clamp(query.Limit, 1, 100);

        return embeddings
            .OrderBy(e => e.ChunkIndex)
            .Take(limit)
            .Select(e => new ChunkPreviewDto(
                EmbeddingId: e.Id,
                TextContent: e.TextContent,
                ChunkIndex: e.ChunkIndex,
                PageNumber: e.PageNumber,
                Model: e.Model,
                CreatedAt: e.CreatedAt
            ))
            .ToList();
    }
}
