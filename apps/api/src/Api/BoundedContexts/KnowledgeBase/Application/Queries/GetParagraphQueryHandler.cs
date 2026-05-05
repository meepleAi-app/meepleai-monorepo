using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Handler for <see cref="GetParagraphQuery"/>.
///
/// Implements a two-strategy retrieval approach:
/// 1. <b>Numbered lookup</b>: queries <c>photo_batch_pages</c> directly for the page text.
///    Fast and deterministic when OCR has already processed the page.
/// 2. <b>Semantic fallback</b>: when numbered text is absent, delegates to
///    <see cref="SearchQueryHandler"/> to perform a hybrid RAG search and surface the
///    closest matching passages from the game's knowledge base.
///
/// Authorization: validates batch ownership before any data access.
///
/// Libro Game AI Assistant MVP Phase 3 — G4.
///
/// NOTE on semantic-fallback unit testing: <see cref="SearchQueryHandler"/> is a concrete
/// class with a non-virtual <c>Handle</c> method and six infrastructure dependencies.
/// Mocking it in unit tests would require either extracting an interface (out of G4 scope)
/// or wiring all six dependencies — both add complexity without proportional value.
/// The fallback path is therefore covered by integration tests only.
/// See: GetParagraphQueryHandlerTests.cs — semantic fallback path is marked as deferred.
/// </summary>
internal sealed class GetParagraphQueryHandler(
    IPhotoBatchUploadRepository repo,
    SearchQueryHandler searchHandler,
    ILogger<GetParagraphQueryHandler> logger)
    : IQueryHandler<GetParagraphQuery, ParagraphDto>
{
    public async Task<ParagraphDto> Handle(
        GetParagraphQuery query,
        CancellationToken cancellationToken)
    {
        // Validate page number early — delegates to a static helper where 'pageNumber'
        // is a real method parameter (required by CA2208/MA0015/S3928).
        ValidatePageNumber(query.PageNumber);

        // Authorization: only the batch owner may read page text.
        var belongs = await repo.BelongsToUserAsync(
            query.PhotoBatchUploadId, query.UserId, cancellationToken).ConfigureAwait(false);

        if (!belongs)
            throw new NotFoundException("PhotoBatchUpload", query.PhotoBatchUploadId.ToString());

        // Strategy 1: Numbered lookup.
        var pageText = await repo.GetPageTextAsync(
            query.PhotoBatchUploadId, query.PageNumber, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(pageText))
        {
            logger.LogDebug(
                "[GetParagraphQuery] Page {Page} of batch {BatchId}: numbered lookup succeeded ({Chars} chars)",
                query.PageNumber, query.PhotoBatchUploadId, pageText.Length);

            return new ParagraphDto(
                PageNumber: query.PageNumber,
                Text: pageText,
                FallbackUsed: false,
                FallbackMethod: null);
        }

        // Strategy 2: Semantic fallback.
        // Numbered text absent (page not yet indexed or blank) — fetch the batch aggregate
        // to validate the page number is within bounds and to obtain the GameId needed
        // for the RAG search.
        var upload = await repo.GetByIdAsync(
            query.PhotoBatchUploadId, cancellationToken).ConfigureAwait(false);

        // GetByIdAsync returning null here would be an inconsistency (BelongsToUserAsync
        // confirmed the record exists moments ago), but guard defensively.
        if (upload is null)
            throw new NotFoundException("PhotoBatchUpload", query.PhotoBatchUploadId.ToString());

        ValidatePageNumber(query.PageNumber, upload.TotalPages);

        var hint = string.IsNullOrWhiteSpace(query.SemanticFallbackHint)
            ? $"page {query.PageNumber} content"
            : query.SemanticFallbackHint;

        try
        {
            var searchQuery = new SearchQuery(
                GameId: upload.GameId,
                Query: hint,
                TopK: 3,
                UserId: query.UserId);

            var results = await searchHandler.Handle(searchQuery, cancellationToken)
                .ConfigureAwait(false);

            var fallbackText = results.Count > 0
                ? string.Join("\n\n", results.Select(r => r.TextContent))
                : string.Empty;

            logger.LogInformation(
                "[GetParagraphQuery] Page {Page} of batch {BatchId}: numbered text absent, semantic fallback returned {Count} result(s)",
                query.PageNumber, query.PhotoBatchUploadId, results.Count);

            return new ParagraphDto(
                PageNumber: query.PageNumber,
                Text: fallbackText,
                FallbackUsed: true,
                FallbackMethod: "semantic");
        }
        catch (Exception ex) when (ex is not OperationCanceledException
                                   && ex is not ArgumentOutOfRangeException
                                   && ex is not NotFoundException)
        {
            // Graceful degradation: a RAG search failure should not surface as a 500.
            // Return empty text with fallback flag set so the caller can decide.
            logger.LogWarning(ex,
                "[GetParagraphQuery] Semantic fallback failed for page {Page} of batch {BatchId}; returning empty text",
                query.PageNumber, query.PhotoBatchUploadId);

            return new ParagraphDto(
                PageNumber: query.PageNumber,
                Text: string.Empty,
                FallbackUsed: true,
                FallbackMethod: "semantic");
        }
    }

    /// <summary>
    /// Validates <paramref name="pageNumber"/> is within the allowed range.
    /// </summary>
    /// <param name="pageNumber">The 1-based page number to validate.</param>
    /// <param name="totalPages">
    /// Optional upper bound (batch total). When <c>0</c> (default) only the lower-bound
    /// check is performed.
    /// </param>
    private static void ValidatePageNumber(int pageNumber, int totalPages = 0)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);

        if (totalPages > 0)
            ArgumentOutOfRangeException.ThrowIfGreaterThan(pageNumber, totalPages);
    }
}
