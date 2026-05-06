using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbChunks;

/// <summary>
/// Handler for <see cref="GetKbChunksQuery"/> — Wave 3 Phase 3 spec-conformant
/// rewrite of <c>GET /api/v1/kb-docs/{id}/chunks</c> per PR #732 §6.3.2 verbatim.
/// </summary>
/// <remarks>
/// <para>
/// <b>Cursor pagination</b>: <c>(Position, Id)</c> tuple, <c>WHERE (Position, Id) > (cursor.Position, cursor.Id)
/// ORDER BY Position ASC, Id ASC LIMIT n+1</c>. The (n+1)th row signals a next
/// page; we drop it from <c>Items</c> and emit its cursor as <c>NextCursor</c>.
/// </para>
///
/// <para>
/// <b>HeadingPath</b>: built by walking up <c>ParentChunkId</c> via a single
/// recursive CTE on PostgreSQL (one SQL round-trip for the whole page). When
/// the database is the InMemory provider (unit tests), CTE is skipped and an
/// empty array is returned per the established pattern.
/// </para>
///
/// <para>
/// <b>Caching</b>: 30min HybridCache keyed by
/// <c>kb:chunks:{docId}:{viewerId}:c={cursor}:l={limit}</c>. Per-viewer because
/// access-control may differ. Tags <c>["kb", "kbDoc:{id}", "kbChunks:{id}"]</c>
/// for invalidation when the doc is re-indexed.
/// </para>
///
/// <para>
/// <b>VectorId</b> de-gating: emitted as a string for all viewers per spec
/// §6.3.2 (security review documented on <see cref="KbChunkSummaryDto"/>).
/// </para>
/// </remarks>
internal sealed class GetKbChunksHandler : IQueryHandler<GetKbChunksQuery, KbChunksListResponse>
{
    private const int SnippetMaxLength = 200;
    private const int MaxCteDepth = 10;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);

    // Recursive CTE — see baseline GetKbChunksHandler for column-name rationale
    // (PascalCase per migration AddChunkHierarchyToTextChunkEntity).
    private static readonly string HeadingPathCte = @"
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

    private readonly MeepleAiDbContext _dbContext;
    private readonly IHybridCacheService _cache;
    private readonly ILogger<GetKbChunksHandler> _logger;

    public GetKbChunksHandler(
        MeepleAiDbContext dbContext,
        IHybridCacheService cache,
        ILogger<GetKbChunksHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<KbChunksListResponse> Handle(GetKbChunksQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogDebug(
            "Fetching KB chunks for doc {DocId} (cursor={Cursor}, limit={Limit}, admin={IsAdmin})",
            query.DocumentId,
            query.Cursor is null ? "<null>" : $"{query.Cursor.Position}:{query.Cursor.Id}",
            query.Limit,
            query.UserIsAdmin);

        var cursorKey = query.Cursor is null
            ? "first"
            : $"{query.Cursor.Position}:{query.Cursor.Id:N}";
        var cacheKey = $"kb:chunks:{query.DocumentId:N}:{query.RequestingUserId:N}:c={cursorKey}:l={query.Limit}";

        return await _cache.GetOrCreateAsync(
            cacheKey,
            async ct => await ComputeAsync(query, ct).ConfigureAwait(false),
            tags: ["kb", $"kbDoc:{query.DocumentId:N}", $"kbChunks:{query.DocumentId:N}"],
            expiration: CacheTtl,
            ct: cancellationToken).ConfigureAwait(false);
    }

    private async Task<KbChunksListResponse> ComputeAsync(
        GetKbChunksQuery query,
        CancellationToken cancellationToken)
    {
        // Validate doc existence + access control before chunk fetch.
        var doc = await _dbContext.PdfDocuments.AsNoTracking()
            .Where(p => p.Id == query.DocumentId)
            .Select(p => new { p.IsPublic, p.UploadedByUserId })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (doc is null)
        {
            throw new NotFoundException($"KB document {query.DocumentId} not found");
        }

        if (!doc.IsPublic
            && doc.UploadedByUserId != query.RequestingUserId
            && !query.UserIsAdmin)
        {
            throw new ForbiddenException($"Access denied to document {query.DocumentId}");
        }

        var totalCount = await _dbContext.TextChunks.AsNoTracking()
            .CountAsync(c => c.PdfDocumentId == query.DocumentId, cancellationToken)
            .ConfigureAwait(false);

        // Cursor predicate: WHERE (Position, Id) > (cursorPosition, cursorId).
        // EF translates the tuple comparison into the row-comparison the spec
        // expects when targeting PostgreSQL.
        var queryable = _dbContext.TextChunks.AsNoTracking()
            .Where(c => c.PdfDocumentId == query.DocumentId);

        if (query.Cursor is not null)
        {
            var cursorPosition = query.Cursor.Position;
            var cursorId = query.Cursor.Id;
            queryable = queryable.Where(c =>
                c.ChunkIndex > cursorPosition
                || (c.ChunkIndex == cursorPosition && c.Id.CompareTo(cursorId) > 0));
        }

        // Fetch limit + 1 to detect next page.
        var rows = await queryable
            .OrderBy(c => c.ChunkIndex)
            .ThenBy(c => c.Id)
            .Take(query.Limit + 1)
            .Select(c => new
            {
                c.Id,
                c.PageNumber,
                c.ChunkIndex,
                c.Content
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        string? nextCursor = null;
        if (rows.Count > query.Limit)
        {
            // Drop the lookahead row (position n+1) and emit a cursor pointing
            // to the LAST returned row (position n) — the predicate on next
            // request is `(Position, Id) > (cursor.Position, cursor.Id)` so
            // the lookahead becomes the first item of the subsequent page.
            rows = rows.Take(query.Limit).ToList();
            var lastReturned = rows[^1];
            nextCursor = KbChunksCursor.Encode(
                new KbChunksCursor.CursorPayload(lastReturned.ChunkIndex, lastReturned.Id));
        }

        var chunkIds = rows.Select(r => r.Id).ToList();
        var headingPaths = chunkIds.Count == 0
            ? new Dictionary<Guid, IReadOnlyList<string>>()
            : await LoadHeadingPathsAsync(chunkIds, cancellationToken).ConfigureAwait(false);

        var items = rows.Select(r => new KbChunkSummaryDto(
            Id: r.Id,
            Position: r.ChunkIndex,
            HeadingPath: headingPaths.TryGetValue(r.Id, out var path) ? path : Array.Empty<string>(),
            Snippet: TruncateSnippet(r.Content),
            PageNumber: r.PageNumber,
            VectorId: r.Id.ToString("N")
        )).ToList();

        return new KbChunksListResponse(items, nextCursor, totalCount);
    }

    /// <summary>
    /// Executes recursive CTE on PostgreSQL; returns empty dict for InMemory.
    /// Mirrors the baseline pattern from <c>GetKbChunksHandler.LoadHeadingPathsAsync</c>.
    /// </summary>
    private async Task<Dictionary<Guid, IReadOnlyList<string>>> LoadHeadingPathsAsync(
        IReadOnlyList<Guid> chunkIds,
        CancellationToken cancellationToken)
    {
        if (string.Equals(
                _dbContext.Database.ProviderName,
                "Microsoft.EntityFrameworkCore.InMemory",
                StringComparison.Ordinal))
        {
            return new Dictionary<Guid, IReadOnlyList<string>>();
        }

        var raw = await _dbContext.Database
            .SqlQueryRaw<HeadingPathRow>(HeadingPathCte, chunkIds.ToArray())
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

    private static string TruncateSnippet(string content) =>
        content.Length <= SnippetMaxLength ? content : content[..SnippetMaxLength];

    private sealed record HeadingPathRow(Guid StartId, string? Heading, int Depth);
}
