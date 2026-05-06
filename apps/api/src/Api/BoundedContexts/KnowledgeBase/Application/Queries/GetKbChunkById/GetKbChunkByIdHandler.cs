using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbChunkById;

/// <summary>
/// Handles <see cref="GetKbChunkByIdQuery"/>.
///
/// Returns the full content of a single text chunk identified by (DocumentId, ChunkId).
/// A 404 is thrown when the chunk does not exist or belongs to a different document —
/// preventing cross-document chunk enumeration leakage.
///
/// Prev/next navigation IDs are resolved by looking for chunks at ChunkIndex ± 1 in the
/// same document. This is a simple indexed lookup (one extra query each), consistent with
/// the offset-pagination approach used by <c>GetKbChunksHandler</c>.
///
/// <c>HeadingPath</c> is built by walking up <c>ParentChunkId</c> via a single recursive
/// CTE on PostgreSQL (one SQL round-trip). When the database is the InMemory provider
/// (unit tests), the CTE is skipped and an empty array is returned — matching the pattern
/// established in <c>GetKbChunksHandler.LoadHeadingPathsAsync</c>.
/// </summary>
internal sealed class GetKbChunkByIdHandler : IQueryHandler<GetKbChunkByIdQuery, KbChunkDetailDto>
{
    /// <summary>Maximum ancestor depth traversed by the recursive CTE.</summary>
    private const int MaxCteDepth = 10;

    // Recursive CTE that climbs the parent_chunk_id chain for a single seed chunk
    // and collects non-null headings ordered from root to leaf (depth DESC).
    // Column names are PascalCase — EF Core convention matches the migration
    // 20260506111654_AddChunkHierarchyToTextChunkEntity.
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
    private readonly ILogger<GetKbChunkByIdHandler> _logger;

    public GetKbChunkByIdHandler(MeepleAiDbContext dbContext, ILogger<GetKbChunkByIdHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<KbChunkDetailDto> Handle(GetKbChunkByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogDebug(
            "Fetching KB chunk {ChunkId} from doc {DocId} (admin={IsAdmin})",
            query.ChunkId, query.DocumentId, query.UserIsAdmin);

        // Primary fetch — also validates that the chunk belongs to the requested document.
        var chunk = await _dbContext.TextChunks.AsNoTracking()
            .Where(c => c.Id == query.ChunkId && c.PdfDocumentId == query.DocumentId)
            .Select(c => new
            {
                c.Id,
                c.Content,
                c.PageNumber,
                c.ChunkIndex,
                c.Level,
                c.Heading,
                c.ParentChunkId,
                c.CharacterCount,
                c.ElementType
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (chunk is null)
        {
            throw new NotFoundException(
                $"Chunk {query.ChunkId} not found in document {query.DocumentId}");
        }

        // Prev/next navigation — look for chunks at ChunkIndex ± 1 in the same document.
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
            ChunkId: chunk.Id,
            Content: chunk.Content,
            PageNumber: chunk.PageNumber,
            Position: chunk.ChunkIndex,
            Level: chunk.Level,
            HeadingPath: headingPath,
            PrevChunkId: prevChunkId,
            NextChunkId: nextChunkId,
            VectorId: query.UserIsAdmin ? chunk.Id : (Guid?)null,
            CharacterCount: query.UserIsAdmin ? chunk.CharacterCount : (int?)null,
            ElementType: query.UserIsAdmin ? chunk.ElementType : null,
            EmbeddingStatus: query.UserIsAdmin ? "indexed" : null,
            ParentChunkId: query.UserIsAdmin ? chunk.ParentChunkId : null
        );
    }

    /// <summary>
    /// Executes a recursive CTE on PostgreSQL to build the heading path for a single chunk
    /// in a single round-trip.  Returns an empty list when running against the EF InMemory
    /// provider (unit-test context) because raw SQL is not supported there.
    /// </summary>
    private async Task<IReadOnlyList<string>> LoadHeadingPathAsync(
        Guid chunkId,
        CancellationToken cancellationToken)
    {
        // InMemory provider does not support raw SQL — return empty paths so unit tests pass.
        // Use string comparison (same pattern as GetKbChunksHandler) because the InMemory
        // package is not referenced by the main project, making IsInMemory() unavailable.
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

    /// <summary>Projection type for the scalar heading-path CTE result.</summary>
    private sealed record HeadingPathRow(string Value);
}
