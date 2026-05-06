using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.SearchKbChunks;

/// <summary>
/// Handles <see cref="SearchKbChunksQuery"/>.
///
/// Performs PostgreSQL full-text search within a single KB document using:
/// <list type="bullet">
///   <item><c>plainto_tsquery('simple', ...)</c> — sanitises user input, preventing tsquery operator injection</item>
///   <item><c>ts_rank_cd</c> — cover-density ranking for result ordering</item>
///   <item><c>ts_headline</c> — generates text snippets with <c>&lt;mark&gt;</c> tags</item>
/// </list>
///
/// <para>
/// <b>InMemory note</b>: PostgreSQL FTS functions (<c>ts_rank_cd</c>, <c>ts_headline</c>,
/// <c>plainto_tsquery</c>) are not available in the EF InMemory provider.
/// All G3 tests must be integration tests using the Testcontainers fixture.
/// </para>
///
/// <para>
/// <b>Validation</b>: query length is validated to 1–200 characters by the endpoint before
/// reaching this handler, but is re-validated here defensively.
/// </para>
/// </summary>
internal sealed class SearchKbChunksHandler : IQueryHandler<SearchKbChunksQuery, KbChunkSearchResultDto>
{
    private const int MaxQueryLength = 200;
    private const int MaxTake = 100;

    /// <summary>Maximum ancestor depth traversed by the heading-path recursive CTE.</summary>
    private const int MaxCteDepth = 10;

    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<SearchKbChunksHandler> _logger;

    public SearchKbChunksHandler(MeepleAiDbContext dbContext, ILogger<SearchKbChunksHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<KbChunkSearchResultDto> Handle(SearchKbChunksQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrWhiteSpace(query.Query) || query.Query.Length > MaxQueryLength)
        {
            throw new ArgumentException("query must be between 1 and 200 characters", nameof(query));
        }

        var skip = Math.Max(0, query.Skip);
        var take = Math.Clamp(query.Take, 1, MaxTake);

        _logger.LogDebug(
            "SearchKbChunks for doc {DocId} query='{Query}' skip={Skip} take={Take}",
            query.DocumentId, query.Query, skip, take);

        // Verify document exists and enforce access control before issuing the FTS query.
        // Issue #730 final review: fetch ownership fields alongside existence check.
        var docAccess = await _dbContext.PdfDocuments.AsNoTracking()
            .Where(p => p.Id == query.DocumentId)
            .Select(p => new { p.IsPublic, p.UploadedByUserId })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (docAccess is null)
        {
            throw new NotFoundException("Document", query.DocumentId.ToString());
        }

        if (!docAccess.IsPublic
            && docAccess.UploadedByUserId != query.RequestingUserId
            && !query.UserIsAdmin)
        {
            throw new ForbiddenException($"Access denied to document {query.DocumentId}");
        }

        // FTS ranked query using plainto_tsquery (prevents operator injection).
        // Column names: PdfDocumentId, Id, PageNumber, ChunkIndex are PascalCase (migration convention).
        // search_vector is lowercase snake_case via [Column("search_vector")] on the entity.
        const string ftsSQL = @"
            WITH ranked AS (
              SELECT
                tc.""Id"",
                tc.""PageNumber"",
                tc.""ChunkIndex"",
                ts_rank_cd(tc.search_vector, q) AS ""Rank"",
                ts_headline('simple', tc.""Content"", q,
                  'StartSel=<mark>, StopSel=</mark>, MaxFragments=2, MaxWords=20, MinWords=5') AS ""Snippet""
              FROM text_chunks tc, plainto_tsquery('simple', {1}) q
              WHERE tc.""PdfDocumentId"" = {0}
                AND tc.search_vector @@ q
            )
            SELECT ""Id"", ""PageNumber"", ""ChunkIndex"", ""Rank"", ""Snippet""
            FROM ranked
            ORDER BY ""Rank"" DESC
            OFFSET {2} LIMIT {3};";

        var rows = await _dbContext.Database
            .SqlQueryRaw<SearchRow>(ftsSQL, query.DocumentId, query.Query, skip, take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Count total matches (without pagination).
        const string countSQL = @"
            SELECT COUNT(*)::int AS ""Value""
            FROM text_chunks tc, plainto_tsquery('simple', {1}) q
            WHERE tc.""PdfDocumentId"" = {0} AND tc.search_vector @@ q;";

        var totalCount = await _dbContext.Database
            .SqlQueryRaw<IntValueRow>(countSQL, query.DocumentId, query.Query)
            .Select(r => r.Value)
            .FirstAsync(cancellationToken)
            .ConfigureAwait(false);

        // Build heading paths for matched chunks in a single round-trip (same CTE pattern as G1/G2).
        var matchIds = rows.Select(r => r.Id).ToList();
        var headingPaths = matchIds.Count == 0
            ? new Dictionary<Guid, IReadOnlyList<string>>()
            : await LoadHeadingPathsAsync(matchIds, cancellationToken).ConfigureAwait(false);

        var matches = rows.Select(r => new KbChunkMatchDto(
            ChunkId: r.Id,
            HeadingPath: headingPaths.TryGetValue(r.Id, out var hp) ? hp : Array.Empty<string>(),
            Snippet: r.Snippet,
            Rank: r.Rank,
            PageNumber: r.PageNumber,
            Position: r.ChunkIndex
        )).ToList();

        return new KbChunkSearchResultDto(matches, totalCount, skip, take);
    }

    /// <summary>
    /// Builds heading paths for a batch of chunk IDs using a recursive CTE (one SQL round-trip).
    /// Matches the pattern established in <c>GetKbChunksHandler.LoadHeadingPathsAsync</c>.
    /// </summary>
    private async Task<Dictionary<Guid, IReadOnlyList<string>>> LoadHeadingPathsAsync(
        IReadOnlyList<Guid> chunkIds,
        CancellationToken cancellationToken)
    {
        var headingPathCte = @"
            WITH RECURSIVE chunk_path(start_id, id, ""Heading"", parent_chunk_id, depth) AS (
              SELECT
                t.""Id"" AS start_id,
                t.""Id"",
                t.""Heading"",
                t.""ParentChunkId"",
                1 AS depth
              FROM text_chunks t
              WHERE t.""Id"" = ANY({0})
              UNION ALL
              SELECT
                cp.start_id,
                t.""Id"",
                t.""Heading"",
                t.""ParentChunkId"",
                cp.depth + 1
              FROM text_chunks t
              JOIN chunk_path cp ON t.""Id"" = cp.parent_chunk_id
              WHERE cp.depth < " + MaxCteDepth + @"
            )
            SELECT start_id AS ""StartId"", ""Heading"", depth AS ""Depth""
            FROM chunk_path
            WHERE ""Heading"" IS NOT NULL
            ORDER BY start_id, depth DESC;";

        var raw = await _dbContext.Database
            .SqlQueryRaw<HeadingPathRow>(headingPathCte, chunkIds.ToArray())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return raw
            .GroupBy(r => r.StartId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g
                    .OrderByDescending(x => x.Depth)
                    .Select(x => x.Heading!)
                    .ToList());
    }

    // ─── Raw SQL projection types ─────────────────────────────────────────────

    /// <summary>Projection for the FTS ranked query result row.</summary>
    private sealed record SearchRow(
        Guid Id,
        int? PageNumber,
        int ChunkIndex,
        float Rank,
        string Snippet);

    /// <summary>Projection for the scalar COUNT query using SqlQueryRaw&lt;T&gt;.</summary>
    private sealed record IntValueRow(int Value);

    /// <summary>Projection for the heading-path recursive CTE result.</summary>
    private sealed record HeadingPathRow(Guid StartId, string? Heading, int Depth);
}
