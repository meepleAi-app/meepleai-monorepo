using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbChunks;

/// <summary>
/// Handles <see cref="GetKbChunksQuery"/>.
///
/// Returns a paginated list of text chunks for the given document, ordered by ChunkIndex.
/// Offset pagination is clamped to <see cref="MaxTake"/> = 100 per page.
///
/// Admin-only fields (<c>VectorId</c>, <c>CharacterCount</c>, <c>ElementType</c>, <c>EmbeddingStatus</c>)
/// are populated only when <see cref="GetKbChunksQuery.UserIsAdmin"/> is <c>true</c>.
///
/// <c>HeadingPath</c> is built by walking up <c>ParentChunkId</c> via a single recursive
/// CTE on PostgreSQL (one SQL round-trip for the whole page).  When the database is an
/// InMemory provider (unit tests), the CTE is skipped and an empty array is returned.
/// </summary>
internal sealed class GetKbChunksHandler : IQueryHandler<GetKbChunksQuery, KbChunkListDto>
{
    private const int MaxTake = 100;
    private const int SnippetMaxLength = 200;

    /// <summary>Maximum ancestor depth traversed by the recursive CTE.</summary>
    private const int MaxCteDepth = 10;

    // Recursive CTE that, for every seed id in the input array, climbs the
    // parent_chunk_id chain up to MaxCteDepth levels and collects non-null headings.
    // Column names are PascalCase — EF Core convention maps C# property names to
    // identical DB column names unless [Column(...)] is used (confirmed: migration
    // 20260506111654_AddChunkHierarchyToTextChunkEntity uses PascalCase names).
    // Static readonly (not const) because C# does not allow string concatenation in const fields.
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
    private readonly ILogger<GetKbChunksHandler> _logger;

    public GetKbChunksHandler(MeepleAiDbContext dbContext, ILogger<GetKbChunksHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<KbChunkListDto> Handle(GetKbChunksQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var skip = Math.Max(0, query.Skip);
        var take = Math.Clamp(query.Take, 1, MaxTake);

        _logger.LogDebug(
            "Fetching KB chunks for doc {DocId} (skip={Skip}, take={Take}, admin={IsAdmin})",
            query.DocumentId, skip, take, query.UserIsAdmin);

        var processingState = await _dbContext.PdfDocuments.AsNoTracking()
            .Where(p => p.Id == query.DocumentId)
            .Select(p => p.ProcessingState)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var processingStateLower = processingState?.ToLowerInvariant() ?? "unknown";

        var totalCount = await _dbContext.TextChunks.AsNoTracking()
            .CountAsync(c => c.PdfDocumentId == query.DocumentId, cancellationToken)
            .ConfigureAwait(false);

        var rows = await _dbContext.TextChunks.AsNoTracking()
            .Where(c => c.PdfDocumentId == query.DocumentId)
            .OrderBy(c => c.ChunkIndex)
            .Skip(skip)
            .Take(take)
            .Select(c => new
            {
                c.Id,
                c.PageNumber,
                c.ChunkIndex,
                c.Level,
                c.Heading,
                c.ParentChunkId,
                c.Content,
                c.CharacterCount,
                c.ElementType
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Phase 2: load heading paths for this page in a single SQL round-trip.
        var chunkIds = rows.Select(r => r.Id).ToList();
        var headingPaths = chunkIds.Count == 0
            ? new Dictionary<Guid, IReadOnlyList<string>>()
            : await LoadHeadingPathsAsync(chunkIds, cancellationToken).ConfigureAwait(false);

        var chunks = rows.Select(r => new KbChunkSummaryDto(
            ChunkId: r.Id,
            PageNumber: r.PageNumber,
            Position: r.ChunkIndex,
            Level: r.Level,
            HeadingPath: headingPaths.TryGetValue(r.Id, out var path) ? path : Array.Empty<string>(),
            Snippet: TruncateSnippet(r.Content),
            VectorId: query.UserIsAdmin ? r.Id : (Guid?)null,
            CharacterCount: query.UserIsAdmin ? r.CharacterCount : (int?)null,
            ElementType: query.UserIsAdmin ? r.ElementType : null,
            EmbeddingStatus: query.UserIsAdmin ? "indexed" : null
        )).ToList();

        return new KbChunkListDto(
            Chunks: chunks,
            TotalCount: totalCount,
            Skip: skip,
            Take: take,
            HasMore: skip + take < totalCount,
            ProcessingState: processingStateLower
        );
    }

    /// <summary>
    /// Executes a recursive CTE on PostgreSQL to build heading paths for all chunk IDs
    /// in a single round-trip.  Returns an empty dictionary when running against the
    /// EF InMemory provider (unit-test context) because raw SQL is not supported there.
    /// </summary>
    private async Task<Dictionary<Guid, IReadOnlyList<string>>> LoadHeadingPathsAsync(
        IReadOnlyList<Guid> chunkIds,
        CancellationToken cancellationToken)
    {
        // InMemory provider does not support raw SQL — return empty paths so unit tests pass.
        // Use provider name comparison (same pattern as HandleOAuthCallbackCommandHandler) because
        // the InMemory package is not referenced by the main project, making IsInMemory() unavailable.
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
                    .OrderByDescending(x => x.Depth)   // root (deepest depth value) first
                    .Select(x => x.Heading!)
                    .ToList());
    }

    private static string TruncateSnippet(string content) =>
        content.Length <= SnippetMaxLength ? content : content[..SnippetMaxLength];

    /// <summary>Projection type for the heading-path recursive CTE result.</summary>
    private sealed record HeadingPathRow(Guid StartId, string? Heading, int Depth);
}
