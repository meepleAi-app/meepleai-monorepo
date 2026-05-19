using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Handler for <see cref="GetParagraphQuery"/>.
///
/// Implements a two-strategy retrieval approach that dispatches on
/// <see cref="GetParagraphQuery.LookupKey"/>:
/// 1. <b>Numbered lookup</b>: queries <c>photo_batch_pages</c> directly for the page text.
///    - For <see cref="ParagraphLookupKey.ByPageNumber"/>: filter by physical page index.
///    - For <see cref="ParagraphLookupKey.ByParagraphNumber"/> (issue #747): filter by
///      the narrative paragraph identifier extracted into <c>paragraph_numbers</c>.
/// 2. <b>Semantic fallback</b>: when numbered text is absent, delegates to
///    <see cref="SearchQueryHandler"/> for a hybrid RAG search.
///
/// Authorization: validates batch ownership before any data access.
///
/// Libro Game AI Assistant MVP Phase 3 — G4. Issue #747 PR-B.
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
        ArgumentNullException.ThrowIfNull(query);

        if (query.LookupKey is null)
            throw new ArgumentException("LookupKey must not be null", nameof(query));

        // Eager validation: lookup-key shape is malformed → ArgumentOutOfRange before
        // any DB call. Keeps the test surface (and the 400 response shape) identical to
        // the pre-#747 page-number path.
        ValidateLookupKey(query.LookupKey);

        // Authorization: only the batch owner may read page text.
        var belongs = await repo.BelongsToUserAsync(
            query.PhotoBatchUploadId, query.UserId, cancellationToken).ConfigureAwait(false);

        if (!belongs)
            throw new NotFoundException("PhotoBatchUpload", query.PhotoBatchUploadId.ToString());

        // Strategy 1: Numbered lookup — dispatch on LookupKey type.
        var (resolvedPageNumber, pageText) = await ResolveNumberedLookupAsync(
            query.PhotoBatchUploadId, query.LookupKey, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(pageText))
        {
            logger.LogDebug(
                "[GetParagraphQuery] Numbered lookup succeeded on batch {BatchId} for {LookupKey} ({Chars} chars, page {PageNumber})",
                query.PhotoBatchUploadId, query.LookupKey, pageText.Length, resolvedPageNumber);

            return BuildResponse(
                pageNumber: resolvedPageNumber,
                text: pageText,
                fallbackUsed: false,
                fallbackMethod: null,
                lookupKey: query.LookupKey);
        }

        // Strategy 2: Semantic fallback.
        // Numbered text absent (page not yet indexed, blank, or paragraph not extracted).
        // For ByPageNumber we also validate the requested page is within the batch's
        // declared total — preserves the 400 contract from the legacy implementation.
        var upload = await repo.GetByIdAsync(
            query.PhotoBatchUploadId, cancellationToken).ConfigureAwait(false);

        // GetByIdAsync returning null here would be an inconsistency (BelongsToUserAsync
        // confirmed the record exists moments ago), but guard defensively.
        if (upload is null)
            throw new NotFoundException("PhotoBatchUpload", query.PhotoBatchUploadId.ToString());

        if (query.LookupKey is ParagraphLookupKey.ByPageNumber pageKey)
            ValidatePageInRange(pageKey.PageNumber, upload.TotalPages);

        var hint = string.IsNullOrWhiteSpace(query.SemanticFallbackHint)
            ? DefaultHintFor(query.LookupKey)
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
                "[GetParagraphQuery] Numbered text absent for {LookupKey} on batch {BatchId}; semantic fallback returned {Count} result(s)",
                query.LookupKey, query.PhotoBatchUploadId, results.Count);

            return BuildResponse(
                pageNumber: resolvedPageNumber,
                text: fallbackText,
                fallbackUsed: true,
                fallbackMethod: "semantic",
                lookupKey: query.LookupKey);
        }
        catch (Exception ex) when (ex is not OperationCanceledException
                                   && ex is not ArgumentOutOfRangeException
                                   && ex is not NotFoundException)
        {
            // Graceful degradation: a RAG search failure should not surface as a 500.
            // Return empty text with fallback flag set so the caller can decide.
            logger.LogWarning(ex,
                "[GetParagraphQuery] Semantic fallback failed for {LookupKey} on batch {BatchId}; returning empty text",
                query.LookupKey, query.PhotoBatchUploadId);

            return BuildResponse(
                pageNumber: resolvedPageNumber,
                text: string.Empty,
                fallbackUsed: true,
                fallbackMethod: "semantic",
                lookupKey: query.LookupKey);
        }
    }

    /// <summary>
    /// Routes the numbered lookup to the right repository call based on the discriminator.
    /// Returns the physical page number that backed the response (0 when no row matched)
    /// alongside the extracted text (null when the row exists but text is empty, or when
    /// no row matched).
    /// </summary>
    private async Task<(int PageNumber, string? Text)> ResolveNumberedLookupAsync(
        Guid uploadId,
        ParagraphLookupKey lookupKey,
        CancellationToken cancellationToken)
    {
        switch (lookupKey)
        {
            case ParagraphLookupKey.ByPageNumber byPage:
                var byPageText = await repo.GetPageTextAsync(
                    uploadId, byPage.PageNumber, cancellationToken).ConfigureAwait(false);
                return (byPage.PageNumber, byPageText);

            case ParagraphLookupKey.ByParagraphNumber byParagraph:
                var byParagraphMatch = await repo.GetPageTextByParagraphNumberAsync(
                    uploadId, byParagraph.ParagraphNumber, cancellationToken).ConfigureAwait(false);
                return byParagraphMatch is null ? (0, null) : byParagraphMatch.Value;

            default:
                // Compiler enforces exhaustiveness via the sealed hierarchy, but the runtime
                // guard catches a future record added without updating the switch.
                throw new ArgumentOutOfRangeException(
                    nameof(lookupKey),
                    lookupKey,
                    $"Unsupported {nameof(ParagraphLookupKey)} variant.");
        }
    }

    /// <summary>
    /// Validates the embedded number for each lookup variant. Lower bound only; the upper
    /// bound for <see cref="ParagraphLookupKey.ByPageNumber"/> requires the batch aggregate
    /// (validated lazily in the fallback branch).
    /// </summary>
    private static void ValidateLookupKey(ParagraphLookupKey lookupKey)
    {
        switch (lookupKey)
        {
            case ParagraphLookupKey.ByPageNumber byPage:
                if (byPage.PageNumber < 1)
                    throw new ArgumentOutOfRangeException(
                        nameof(lookupKey),
                        byPage.PageNumber,
                        $"PageNumber must be ≥ 1; got {byPage.PageNumber}.");
                break;
            case ParagraphLookupKey.ByParagraphNumber byParagraph:
                if (byParagraph.ParagraphNumber < 1)
                    throw new ArgumentOutOfRangeException(
                        nameof(lookupKey),
                        byParagraph.ParagraphNumber,
                        $"ParagraphNumber must be ≥ 1; got {byParagraph.ParagraphNumber}.");
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(lookupKey),
                    lookupKey,
                    $"Unsupported {nameof(ParagraphLookupKey)} variant.");
        }
    }

    private static void ValidatePageInRange(int pageNumber, int totalPages)
    {
        if (totalPages > 0)
            ArgumentOutOfRangeException.ThrowIfGreaterThan(pageNumber, totalPages);
    }

    /// <summary>
    /// Builds the response DTO, surfacing <see cref="ParagraphDto.ParagraphNumber"/> only
    /// when the caller used <see cref="ParagraphLookupKey.ByParagraphNumber"/> so existing
    /// page-based consumers see no schema change.
    /// </summary>
    private static ParagraphDto BuildResponse(
        int pageNumber,
        string text,
        bool fallbackUsed,
        string? fallbackMethod,
        ParagraphLookupKey lookupKey)
    {
        var paragraphNumber = lookupKey is ParagraphLookupKey.ByParagraphNumber byParagraph
            ? byParagraph.ParagraphNumber
            : (int?)null;

        return new ParagraphDto(
            PageNumber: pageNumber,
            Text: text,
            FallbackUsed: fallbackUsed,
            FallbackMethod: fallbackMethod,
            ParagraphNumber: paragraphNumber);
    }

    private static string DefaultHintFor(ParagraphLookupKey lookupKey)
        => lookupKey switch
        {
            ParagraphLookupKey.ByPageNumber byPage => $"page {byPage.PageNumber} content",
            ParagraphLookupKey.ByParagraphNumber byParagraph => $"paragraph {byParagraph.ParagraphNumber} content",
            _ => "page content",
        };
}
