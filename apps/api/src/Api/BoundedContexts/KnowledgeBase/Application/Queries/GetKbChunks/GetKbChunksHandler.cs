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
/// <c>HeadingPath</c> returns an empty array in this skeleton; recursive CTE to be
/// added in the next commit (Task 7).
/// </summary>
internal sealed class GetKbChunksHandler : IQueryHandler<GetKbChunksQuery, KbChunkListDto>
{
    private const int MaxTake = 100;
    private const int SnippetMaxLength = 200;

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

        var chunks = rows.Select(r => new KbChunkSummaryDto(
            ChunkId: r.Id,
            PageNumber: r.PageNumber,
            Position: r.ChunkIndex,
            Level: r.Level,
            HeadingPath: Array.Empty<string>(), // Stub: recursive CTE heading path populated in Task 7
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

    private static string TruncateSnippet(string content) =>
        content.Length <= SnippetMaxLength ? content : content[..SnippetMaxLength];
}
