using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.SearchDocumentChunks;

/// <summary>
/// Handles <see cref="SearchDocumentChunksByVectorQuery"/>.
///
/// Flow:
///   1. Resolve the VectorDocument for the given PdfDocumentId — get its Id (= VectorDocumentId)
///      and GameId (required to call IEmbeddingRepository which filters by game_id).
///   2. Embed the query string via IEmbeddingService.
///   3. Call IEmbeddingRepository.SearchByVectorWithScoresAsync using the resolved VectorDocumentId
///      as a documentIds filter (pgvector_embeddings.vector_document_id — NOT PdfDocumentId).
///   4. Map results → DocChunkSearchHit (score desc, 240-char snippet).
///
/// Issue #1653: F3-FU-4 — per-document scored similarity-search.
/// </summary>
internal sealed class SearchDocumentChunksByVectorQueryHandler
    : IQueryHandler<SearchDocumentChunksByVectorQuery, SearchDocumentChunksResultDto>
{
    private static readonly SearchDocumentChunksResultDto NotIndexedResult =
        new([], "Document not indexed");

    private readonly MeepleAiDbContext _db;
    private readonly IEmbeddingService _embeddingService;
    private readonly IEmbeddingRepository _embeddingRepo;
    private readonly ILogger<SearchDocumentChunksByVectorQueryHandler> _logger;

    public SearchDocumentChunksByVectorQueryHandler(
        MeepleAiDbContext db,
        IEmbeddingService embeddingService,
        IEmbeddingRepository embeddingRepo,
        ILogger<SearchDocumentChunksByVectorQueryHandler> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _embeddingRepo = embeddingRepo ?? throw new ArgumentNullException(nameof(embeddingRepo));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SearchDocumentChunksResultDto> Handle(
        SearchDocumentChunksByVectorQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Step 1: Resolve VectorDocument → VectorDocumentId (the FK used in pgvector_embeddings)
        // and GameId (required by the search index filter).
        var vectorDoc = await _db.VectorDocuments
            .AsNoTracking()
            .Where(v => v.PdfDocumentId == query.PdfDocumentId)
            .Select(v => new { v.Id, v.GameId })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (vectorDoc is null || vectorDoc.GameId is null)
        {
            _logger.LogInformation(
                "[SearchDocumentChunksByVectorQueryHandler] PdfDocumentId={PdfDocId} not indexed (no VectorDocument or missing GameId)",
                query.PdfDocumentId);
            return NotIndexedResult;
        }

        var vectorDocumentId = vectorDoc.Id;
        var gameId = vectorDoc.GameId.Value;

        // Step 2: Embed the query string.
        var embResult = await _embeddingService
            .GenerateEmbeddingAsync(query.Query, cancellationToken)
            .ConfigureAwait(false);

        if (!embResult.Success || embResult.Embeddings.Count == 0)
        {
            var errorMessage = embResult.ErrorMessage ?? "Embedding generation returned no vectors";
            _logger.LogWarning(
                "[SearchDocumentChunksByVectorQueryHandler] Embedding failed for query={Query}: {ErrorMessage}",
                query.Query, errorMessage);
            return new SearchDocumentChunksResultDto([], errorMessage);
        }

        var queryVector = new Vector(embResult.Embeddings[0]);
        var topK = Math.Clamp(query.TopK, 1, 100);

        // Step 3: Search pgvector filtered to this document's VectorDocumentId.
        var hits = await _embeddingRepo
            .SearchByVectorWithScoresAsync(
                gameId,
                queryVector,
                topK,
                query.MinScore,
                documentIds: new[] { vectorDocumentId },
                cancellationToken)
            .ConfigureAwait(false);

        // Step 4: Map and sort by score descending (pgvector already sorts by distance asc,
        // but we ensure desc here for the caller's convenience).
        var results = hits
            .OrderByDescending(h => h.Score)
            .Select(h => new DocChunkSearchHit(
                ChunkIndex: h.Embedding.ChunkIndex,
                PageNumber: h.Embedding.PageNumber,
                Score: h.Score,
                Snippet: Truncate(h.Embedding.TextContent, 240)))
            .ToList();

        _logger.LogInformation(
            "[SearchDocumentChunksByVectorQueryHandler] Returned {Count} scored hits for PdfDocumentId={PdfDocId}",
            results.Count, query.PdfDocumentId);

        return new SearchDocumentChunksResultDto(results, null);
    }

    private static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;
        return string.Concat(text.AsSpan(0, maxLength), "…");
    }
}
