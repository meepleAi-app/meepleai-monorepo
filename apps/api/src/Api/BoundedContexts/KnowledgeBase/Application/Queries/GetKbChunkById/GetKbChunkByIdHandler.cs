using Api.Infrastructure;
using Api.Infrastructure.Services;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbChunkById;

/// <summary>
/// Handler for <see cref="GetKbChunkByIdQuery"/> — Wave 3 Phase 3 spec-conformant
/// rewrite of <c>GET /api/v1/kb-docs/{id}/chunks/{chunkId}</c> per PR #732
/// §6.3.3 verbatim.
/// </summary>
/// <remarks>
/// <para>
/// <b>Prev/next navigation</b>: derived by lookup at <c>ChunkIndex - 1</c> and
/// <c>ChunkIndex + 1</c> within the same document. First chunk → <c>PrevChunkId == null</c>;
/// last chunk → <c>NextChunkId == null</c>.
/// </para>
///
/// <para>
/// <b>Markdown sanitization</b>: applied server-side via
/// <see cref="MarkdownSubsetSanitizer.Sanitize"/> before emission. The raw
/// content is preserved in the database — sanitization is presentation-layer.
/// </para>
///
/// <para>
/// <b>Caching</b>: 24h HybridCache. Chunks are immutable post-ingest, so the
/// cache is keyed only by <c>(docId, chunkId)</c> — no per-viewer dimension.
/// Access control is still enforced inside the factory before any data flows
/// into the cache, so per-viewer 403 cannot leak (a denied viewer would see a
/// fresh <c>ForbiddenException</c> on every request, never a cached payload).
/// </para>
///
/// <para>
/// <b>HeadingPath</b>: recursive CTE on PostgreSQL, empty array on InMemory
/// (mirrors <c>GetKbChunksHandler</c>).
/// </para>
/// </remarks>
internal sealed class GetKbChunkByIdHandler : IQueryHandler<GetKbChunkByIdQuery, KbChunkDetailDto>
{
    private const int MaxCteDepth = 10;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

    private static readonly string HeadingPathCte = @"
        WITH RECURSIVE chunk_path(id, ""Heading"", parent_chunk_id, depth) AS (
          SELECT
            t.""Id"",
            t.""Heading"",
            t.""ParentChunkId"",
            1 AS depth
          FROM text_chunks t
          WHERE t.""Id"" = {0}
          UNION ALL
          SELECT
            t.""Id"",
            t.""Heading"",
            t.""ParentChunkId"",
            cp.depth + 1
          FROM text_chunks t
          JOIN chunk_path cp ON t.""Id"" = cp.parent_chunk_id
          WHERE cp.depth < " + MaxCteDepth + @"
        )
        SELECT ""Heading"" AS ""Value""
        FROM chunk_path
        WHERE ""Heading"" IS NOT NULL
        ORDER BY depth DESC;";

    private readonly MeepleAiDbContext _dbContext;
    private readonly IHybridCacheService _cache;
    private readonly ILogger<GetKbChunkByIdHandler> _logger;

    public GetKbChunkByIdHandler(
        MeepleAiDbContext dbContext,
        IHybridCacheService cache,
        ILogger<GetKbChunkByIdHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<KbChunkDetailDto> Handle(GetKbChunkByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogDebug(
            "Fetching KB chunk {ChunkId} from doc {DocId} (admin={IsAdmin})",
            query.ChunkId, query.DocumentId, query.UserIsAdmin);

        // Access control runs OUTSIDE the cache so a denied viewer never receives
        // a cached success payload. The cache key omits the viewer dimension —
        // chunk content is immutable post-ingest, no per-viewer derivation.
        await EnforceAccessControlAsync(query, cancellationToken).ConfigureAwait(false);

        var cacheKey = $"kb:chunk:{query.DocumentId:N}:{query.ChunkId:N}";

        return await _cache.GetOrCreateAsync(
            cacheKey,
            async ct => await ComputeAsync(query, ct).ConfigureAwait(false),
            tags: ["kb", $"kbDoc:{query.DocumentId:N}", $"kbChunks:{query.DocumentId:N}"],
            expiration: CacheTtl,
            ct: cancellationToken).ConfigureAwait(false);
    }

    private async Task EnforceAccessControlAsync(
        GetKbChunkByIdQuery query,
        CancellationToken cancellationToken)
    {
        var docAccess = await _dbContext.PdfDocuments.AsNoTracking()
            .Where(p => p.Id == query.DocumentId)
            .Select(p => new { p.IsPublic, p.UploadedByUserId })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (docAccess is null)
        {
            throw new NotFoundException($"KB document {query.DocumentId} not found");
        }

        if (!docAccess.IsPublic
            && docAccess.UploadedByUserId != query.RequestingUserId
            && !query.UserIsAdmin)
        {
            throw new ForbiddenException($"Access denied to document {query.DocumentId}");
        }
    }

    private async Task<KbChunkDetailDto> ComputeAsync(
        GetKbChunkByIdQuery query,
        CancellationToken cancellationToken)
    {
        var chunk = await _dbContext.TextChunks.AsNoTracking()
            .Where(c => c.Id == query.ChunkId && c.PdfDocumentId == query.DocumentId)
            .Select(c => new
            {
                c.Id,
                c.Content,
                c.PageNumber,
                c.ChunkIndex
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (chunk is null)
        {
            throw new NotFoundException(
                $"Chunk {query.ChunkId} not found in document {query.DocumentId}");
        }

        var prevChunkId = await _dbContext.TextChunks.AsNoTracking()
            .Where(c => c.PdfDocumentId == query.DocumentId && c.ChunkIndex == chunk.ChunkIndex - 1)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var nextChunkId = await _dbContext.TextChunks.AsNoTracking()
            .Where(c => c.PdfDocumentId == query.DocumentId && c.ChunkIndex == chunk.ChunkIndex + 1)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var headingPath = await LoadHeadingPathAsync(chunk.Id, cancellationToken)
            .ConfigureAwait(false);

        return new KbChunkDetailDto(
            Id: chunk.Id,
            DocId: query.DocumentId,
            Position: chunk.ChunkIndex,
            HeadingPath: headingPath,
            Content: MarkdownSubsetSanitizer.Sanitize(chunk.Content),
            PageNumber: chunk.PageNumber,
            PrevChunkId: prevChunkId,
            NextChunkId: nextChunkId,
            // Gate B v1 carryover — TextChunkEntity has no Metadata column.
            Metadata: new Dictionary<string, object>(StringComparer.Ordinal)
        );
    }

    private async Task<IReadOnlyList<string>> LoadHeadingPathAsync(
        Guid chunkId,
        CancellationToken cancellationToken)
    {
        if (string.Equals(
                _dbContext.Database.ProviderName,
                "Microsoft.EntityFrameworkCore.InMemory",
                StringComparison.Ordinal))
        {
            return Array.Empty<string>();
        }

        var rows = await _dbContext.Database
            .SqlQueryRaw<HeadingPathRow>(HeadingPathCte, chunkId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(r => r.Value).ToList();
    }

    private sealed record HeadingPathRow(string Value);
}
