using System.Text;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Infrastructure;
using Api.Observability;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GlobalKbSearch;

/// <summary>
/// Handles <see cref="GlobalKbSearchQuery"/>.
/// Orchestration pipeline:
/// 1. Resolve accessible game IDs via <see cref="IRagAccessService.GetAccessibleGameIdsAsync"/> (RBAC).
/// 2. Guard EC-1: empty game list → 200 empty response, no search call.
/// 3. Call <see cref="IMultiGameHybridSearchService.SearchAsync"/> with <c>Limit+1</c> probe.
/// 4. Detect <c>hasMore</c> and truncate to <c>Limit</c>.
/// 5. Batch-enrich in <b>ONE</b> EF query: join VectorDocument → PdfDocument + SharedGame
///    keyed by the distinct <c>PdfDocumentId</c> values from the result set (no N+1, EC-7).
/// 6. Map to <see cref="GlobalKbSearchResultDto"/>; skip results whose enrichment row is
///    missing (race-with-delete, EC-2).
/// 7. Compute opaque Base64 cursor for the next page when <c>hasMore == true</c> (EC-4).
///
/// Issue #1661: cross-game KB search (PR-1, Task 4).
/// </summary>
internal sealed class GlobalKbSearchQueryHandler
    : IQueryHandler<GlobalKbSearchQuery, GlobalKbSearchResponseDto>
{
    private readonly IRagAccessService _ragAccessService;
    private readonly IMultiGameHybridSearchService _multiGameSearch;
    private readonly MeepleAiDbContext _db;
    private readonly ILogger<GlobalKbSearchQueryHandler> _logger;

    public GlobalKbSearchQueryHandler(
        IRagAccessService ragAccessService,
        IMultiGameHybridSearchService multiGameSearch,
        MeepleAiDbContext db,
        ILogger<GlobalKbSearchQueryHandler> logger)
    {
        _ragAccessService = ragAccessService ?? throw new ArgumentNullException(nameof(ragAccessService));
        _multiGameSearch = multiGameSearch ?? throw new ArgumentNullException(nameof(multiGameSearch));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GlobalKbSearchResponseDto> Handle(
        GlobalKbSearchQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // 1. RBAC: resolve accessible games
        var accessibleGameIds = await _ragAccessService
            .GetAccessibleGameIdsAsync(query.UserId, query.Role, cancellationToken)
            .ConfigureAwait(false);

        // EC-1: no accessible games → return empty response without calling search
        if (accessibleGameIds.Count == 0)
        {
            _logger.LogDebug(
                "[GlobalKbSearch] UserId={UserId} has no accessible games — returning empty (EC-1)",
                query.UserId);
            return new GlobalKbSearchResponseDto(Array.Empty<GlobalKbSearchResultDto>(), false, null);
        }

        // Issue #1686 D-5: GameId facet intersects with RBAC.
        // When provided AND ∉ accessibleGameIds → 200 empty (NOT 403, avoid info leak).
        // When provided AND ∈ accessibleGameIds → narrow search to that game only.
        if (query.GameId.HasValue)
        {
            if (!accessibleGameIds.Contains(query.GameId.Value))
            {
                _logger.LogDebug(
                    "[GlobalKbSearch] UserId={UserId} requested GameId={GameId} not in accessible set — returning empty (D-5)",
                    query.UserId, query.GameId.Value);
                MeepleAiMetrics.RecordKbGlobalSearchFacet(
                    MeepleAiMetrics.KbGlobalSearchFacetTypes.GameId,
                    MeepleAiMetrics.KbGlobalSearchFacetStates.Rejected);
                return new GlobalKbSearchResponseDto(Array.Empty<GlobalKbSearchResultDto>(), false, null);
            }

            // Narrow the accessible set to exactly the requested game.
            accessibleGameIds = new[] { query.GameId.Value };
            MeepleAiMetrics.RecordKbGlobalSearchFacet(
                MeepleAiMetrics.KbGlobalSearchFacetTypes.GameId,
                MeepleAiMetrics.KbGlobalSearchFacetStates.Applied);
        }

        // Issue #1686 D-4: push-down DocType + Language facets BEFORE vector search.
        // When ANY of these is set, compute an allowlist of PdfDocument.Id values via a
        // single EF query filtered by (SharedGameId ∈ accessibleGameIds) AND (DocumentType IN docTypes)
        // AND (Language == language). Forwarded to IMultiGameHybridSearchService.SearchAsync
        // as the documentIds parameter, which each per-game hybrid search uses to restrict its hits.
        IReadOnlyList<Guid>? facetDocumentIds = null;
        var hasDocTypeFacet = query.DocType is { Count: > 0 };
        var hasLanguageFacet = !string.IsNullOrWhiteSpace(query.Language);

        if (hasDocTypeFacet || hasLanguageFacet)
        {
            facetDocumentIds = await BuildFacetDocumentIdsAsync(
                accessibleGameIds, query.DocType, query.Language, cancellationToken)
                .ConfigureAwait(false);

            // D-11: empty documentIds → short-circuit to 200 empty (no wasted RPC).
            // D-14: D-11 short-circuit does NOT increment applied — the facet matched zero docs,
            // providing no narrowing value. Counter signal stays meaningful: high applied count
            // = real adoption; low count + many requests = facet UI rarely used.
            if (facetDocumentIds.Count == 0)
            {
                _logger.LogDebug(
                    "[GlobalKbSearch] UserId={UserId} facets matched zero documents — returning empty (D-11)",
                    query.UserId);
                return new GlobalKbSearchResponseDto(Array.Empty<GlobalKbSearchResultDto>(), false, null);
            }

            // facetDocumentIds.Count > 0 here — facets actually narrow the candidate set (D-14).
            if (hasDocTypeFacet)
            {
                MeepleAiMetrics.RecordKbGlobalSearchFacet(
                    MeepleAiMetrics.KbGlobalSearchFacetTypes.DocType,
                    MeepleAiMetrics.KbGlobalSearchFacetStates.Applied);
            }

            if (hasLanguageFacet)
            {
                MeepleAiMetrics.RecordKbGlobalSearchFacet(
                    MeepleAiMetrics.KbGlobalSearchFacetTypes.Language,
                    MeepleAiMetrics.KbGlobalSearchFacetStates.Applied);
            }
        }

        // 2. Decode cursor (best-effort; invalid cursor → start from beginning)
        var cursorScore = double.MaxValue;
        var cursorChunkId = string.Empty;
        if (!string.IsNullOrEmpty(query.Cursor))
        {
            (cursorScore, cursorChunkId) = DecodeCursor(query.Cursor);
        }

        // 3. Search: probe with Limit+1 to detect hasMore (EC-4)
        var probeLimit = query.Limit + 1;
        var rawResults = await _multiGameSearch
            .SearchAsync(
                query: query.Query,
                gameIds: accessibleGameIds,
                limit: probeLimit,
                mode: query.Mode,
                minScore: query.MinScore,
                documentIds: facetDocumentIds, // Issue #1686 D-4: push-down allowlist (null when no facets)
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        // 4. Apply cursor filter (keyset pagination: skip items before the cursor position)
        //    Ordering: score DESC, chunkId ASC (stable cursor EC-4)
        IReadOnlyList<MultiGameSearchResultItem> filtered = rawResults;
        if (!string.IsNullOrEmpty(query.Cursor) && cursorScore < double.MaxValue)
        {
            filtered = rawResults
                .Where(r =>
                    r.HybridScore < cursorScore ||
                    (Math.Abs(r.HybridScore - cursorScore) < 1e-9 &&
                     string.Compare(r.ChunkId, cursorChunkId, StringComparison.Ordinal) > 0))
                .ToList();
        }

        // 5. Detect hasMore and truncate to Limit
        var hasMore = filtered.Count > query.Limit;
        var page = hasMore ? filtered.Take(query.Limit).ToList() : filtered.ToList();

        if (page.Count == 0)
        {
            return new GlobalKbSearchResponseDto(Array.Empty<GlobalKbSearchResultDto>(), false, null);
        }

        // 6. Batch enrichment — ONE EF query: join VectorDocument → PdfDocument → SharedGame
        //    Extract distinct PdfDocumentId Guids from the page
        var pdfDocIds = page
            .Select(r => Guid.TryParse(r.PdfDocumentId, out var g) ? g : (Guid?)null)
            .Where(g => g.HasValue)
            .Select(g => g!.Value)
            .Distinct()
            .ToList();

        var enrichmentMap = await BuildEnrichmentMapAsync(pdfDocIds, cancellationToken)
            .ConfigureAwait(false);

        // 7. Map results to DTOs — skip items whose enrichment row is missing (EC-2)
        var dtos = new List<GlobalKbSearchResultDto>(page.Count);
        foreach (var item in page)
        {
            if (!Guid.TryParse(item.PdfDocumentId, out var docId))
            {
                _logger.LogWarning(
                    "[GlobalKbSearch] Cannot parse PdfDocumentId '{Id}' as Guid — skipping (EC-2)",
                    item.PdfDocumentId);
                continue;
            }

            if (!enrichmentMap.TryGetValue(docId, out var enrichment))
            {
                _logger.LogDebug(
                    "[GlobalKbSearch] No enrichment row for docId={DocId} — skipping (race-with-delete EC-2)",
                    docId);
                continue;
            }

            dtos.Add(new GlobalKbSearchResultDto(
                ChunkId: item.ChunkId,
                DocId: docId,
                DocTitle: enrichment.FileName,
                GameId: enrichment.GameId,
                GameName: enrichment.GameName,
                DocType: enrichment.DocumentType,
                HeadingPath: null, // EC-6: not materialised in vector result (D2 known limitation)
                Snippet: item.Content,
                PageNumber: item.PageNumber,
                Score: item.HybridScore));
        }

        // 8. Compute next cursor (encode last item in page)
        string? nextCursor = null;
        if (hasMore && dtos.Count > 0)
        {
            var last = dtos[^1];
            nextCursor = EncodeCursor(last.Score, last.ChunkId);
        }

        _logger.LogInformation(
            "[GlobalKbSearch] UserId={UserId} query='{Query}' → {Count} results, hasMore={HasMore}",
            query.UserId, query.Query, dtos.Count, hasMore);

        return new GlobalKbSearchResponseDto(dtos, hasMore, nextCursor);
    }

    // ─── Private helpers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Issue #1686 D-4: computes the document allowlist for the push-down facet filter.
    /// Single EF query against <c>PdfDocuments</c> filtered by
    /// <c>SharedGameId ∈ accessibleGameIds</c> AND (when provided)
    /// <c>DocumentType ∈ docType</c> AND <c>Language == language</c>.
    /// Values are normalised to lower-case before comparison (D-8).
    /// </summary>
    /// <param name="accessibleGameIds">RBAC-resolved game set (post D-5 intersection).</param>
    /// <param name="docType">Optional list of doc types (case-insensitive). Null/empty = no filter.</param>
    /// <param name="language">Optional ISO 639-1 language code (case-insensitive). Null = no filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of matching <c>PdfDocument.Id</c> values (may be empty).</returns>
    private async Task<IReadOnlyList<Guid>> BuildFacetDocumentIdsAsync(
        IReadOnlyList<Guid> accessibleGameIds,
        IReadOnlyList<string>? docType,
        string? language,
        CancellationToken cancellationToken)
    {
        // Materialise normalised filter values once. The entity already stores DocumentType
        // and Language in lower-case (see PdfDocumentEntity defaults: "base", "en", …), and the
        // validator's allowlist match is case-insensitive. So we normalise user input to
        // lower-case in-memory and compare to entity values WITHOUT EF-side ToLower() — keeping
        // the SQL provider-agnostic (CA1311-safe).
        List<string>? docTypesLower = null;
        if (docType is { Count: > 0 })
        {
            docTypesLower = docType
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Select(d => d.Trim().ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .ToList();

            // After dedup, the list might be empty if all entries were whitespace; treat as no filter.
            if (docTypesLower.Count == 0)
            {
                docTypesLower = null;
            }
        }

        string? languageLower = null;
        if (!string.IsNullOrWhiteSpace(language))
        {
            languageLower = language.Trim().ToLowerInvariant();
        }

        // Composable query: start with the RBAC invariant, then conditionally narrow.
        var q = _db.PdfDocuments
            .AsNoTracking()
            .Where(p => p.SharedGameId.HasValue && accessibleGameIds.Contains(p.SharedGameId.Value));

        if (docTypesLower != null)
        {
            // EF translates Contains() → SQL IN (...). Entity values are already lower-case.
            q = q.Where(p => docTypesLower.Contains(p.DocumentType));
        }

        if (languageLower != null)
        {
            // Entity stores ISO 639-1 in lower-case already.
            q = q.Where(p => p.Language == languageLower);
        }

        var ids = await q
            .Select(p => p.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return ids;
    }

    /// <summary>
    /// Executes a single EF query that joins VectorDocument → PdfDocument → SharedGame
    /// for all requested PdfDocument IDs. Returns a lookup keyed by PdfDocument.Id.
    /// No N+1: one SQL round-trip for the entire result-set page.
    /// </summary>
    private async Task<Dictionary<Guid, EnrichmentRow>> BuildEnrichmentMapAsync(
        IReadOnlyList<Guid> pdfDocIds,
        CancellationToken cancellationToken)
    {
        if (pdfDocIds.Count == 0)
            return new Dictionary<Guid, EnrichmentRow>();

        // Single LINQ join: PdfDocuments → SharedGames (inner join on SharedGameId)
        // We only have PdfDocumentIds from the search results. Each PDF has a SharedGameId.
        var rows = await _db.PdfDocuments
            .AsNoTracking()
            .Where(p => pdfDocIds.Contains(p.Id) && p.SharedGameId.HasValue)
            .Join(
                _db.SharedGames.AsNoTracking().Where(sg => !sg.IsDeleted),
                pdf => pdf.SharedGameId!.Value,
                sg => sg.Id,
                (pdf, sg) => new EnrichmentRow(
                    pdf.Id,
                    pdf.FileName,
                    sg.Id,
                    sg.Title,
                    pdf.DocumentType))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Build dictionary — first wins if duplicate (shouldn't happen, PdfDocument.Id is PK)
        var map = new Dictionary<Guid, EnrichmentRow>(rows.Count);
        foreach (var row in rows)
        {
            map.TryAdd(row.PdfDocId, row);
        }
        return map;
    }

    /// <summary>
    /// Encodes a Base64 cursor: UTF-8 bytes of <c>"{score}|{chunkId}"</c>.
    /// The score is formatted as a culture-invariant round-trip string (R format).
    /// </summary>
    private static string EncodeCursor(float score, string chunkId)
    {
        var raw = $"{score.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}|{chunkId}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }

    /// <summary>
    /// Decodes a cursor produced by <see cref="EncodeCursor"/>.
    /// Returns <c>(double.MaxValue, string.Empty)</c> on any parse failure so the
    /// caller can safely skip cursor-filtering and start from the beginning.
    /// </summary>
    private static (double Score, string ChunkId) DecodeCursor(string cursor)
    {
        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var sep = raw.IndexOf('|');
            if (sep < 0)
                return (double.MaxValue, string.Empty);

            var scoreStr = raw[..sep];
            var chunkId = raw[(sep + 1)..];

            if (double.TryParse(scoreStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var score))
                return (score, chunkId);
        }
        catch (Exception)
        {
            // Invalid cursor format — best-effort: ignore and start from beginning
        }

        return (double.MaxValue, string.Empty);
    }

    // ─── Private projection type ─────────────────────────────────────────────────

    /// <summary>Enrichment data for a single PDF document joined to its SharedGame.</summary>
    private sealed record EnrichmentRow(
        Guid PdfDocId,
        string FileName,
        Guid GameId,
        string GameName,
        string DocumentType);
}
