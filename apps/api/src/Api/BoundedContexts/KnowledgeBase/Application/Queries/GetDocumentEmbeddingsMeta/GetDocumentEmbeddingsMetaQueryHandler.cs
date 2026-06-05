using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetDocumentEmbeddingsMeta;

/// <summary>
/// Issue #1674: resolve embeddings metadata for the per-doc admin viewer.
/// Single-query JOIN over VectorDocuments + PdfDocuments (Language) avoids
/// extra round-trip on PgVectorEmbeddings — Model/Dimensions/ChunkCount are
/// denormalized onto VectorDocumentEntity (FIX-1 post-review).
/// </summary>
internal sealed class GetDocumentEmbeddingsMetaQueryHandler
    : IQueryHandler<GetDocumentEmbeddingsMetaQuery, DocumentEmbeddingsMetaDto>
{
    private readonly MeepleAiDbContext _db;
    private readonly ILogger<GetDocumentEmbeddingsMetaQueryHandler> _logger;

    public GetDocumentEmbeddingsMetaQueryHandler(
        MeepleAiDbContext db,
        ILogger<GetDocumentEmbeddingsMetaQueryHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<DocumentEmbeddingsMetaDto> Handle(
        GetDocumentEmbeddingsMetaQuery request,
        CancellationToken cancellationToken)
    {
        var data = await (
            from v in _db.VectorDocuments.AsNoTracking()
            join p in _db.PdfDocuments.AsNoTracking()
                on v.PdfDocumentId equals p.Id
            where v.PdfDocumentId == request.DocId
            select new
            {
                v.EmbeddingModel,
                v.EmbeddingDimensions,
                v.ChunkCount,
                v.IndexedAt,
                p.Language,
            }
        ).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (data is null)
        {
            _logger.LogWarning(
                "Embeddings meta requested for PdfDocument {DocId}: no VectorDocument row (not indexed)",
                request.DocId);
            throw new NotFoundException("Embeddings", request.DocId.ToString());
        }

        _logger.LogInformation(
            "Returning embeddings meta for PdfDocument {DocId}: model={Model}, dim={Dim}, chunks={Chunks}",
            request.DocId,
            data.EmbeddingModel,
            data.EmbeddingDimensions,
            data.ChunkCount);

        return new DocumentEmbeddingsMetaDto(
            DocId: request.DocId,
            Model: data.EmbeddingModel,
            Dimensions: data.EmbeddingDimensions,
            TotalChunks: data.ChunkCount,
            IndexedAt: data.IndexedAt,
            Language: data.Language);
    }
}
