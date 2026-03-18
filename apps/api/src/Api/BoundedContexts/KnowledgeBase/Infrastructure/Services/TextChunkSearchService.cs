using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Services;

/// <summary>
/// PostgreSQL-backed implementation of ITextChunkSearchService.
/// Uses full-text search (tsvector) for keyword matching and
/// EF Core for adjacent chunk retrieval.
/// </summary>
internal sealed class TextChunkSearchService : ITextChunkSearchService
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<TextChunkSearchService> _logger;

    public TextChunkSearchService(
        MeepleAiDbContext dbContext,
        ILogger<TextChunkSearchService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<TextChunkMatch>> FullTextSearchAsync(
        Guid gameId,
        string query,
        int limit,
        CancellationToken ct)
    {
        try
        {
            // Sanitize query for tsquery: replace special chars, join with & for AND search
            var sanitizedQuery = SanitizeForTsQuery(query);
            if (string.IsNullOrWhiteSpace(sanitizedQuery))
                return [];

            // Raw SQL for PostgreSQL full-text search using tsvector
            var results = await _dbContext.TextChunks
                .FromSqlInterpolated($@"
                    SELECT tc.*
                    FROM text_chunks tc
                    WHERE tc.""GameId"" = {gameId}
                      AND tc.search_vector @@ plainto_tsquery('english', {sanitizedQuery})
                    ORDER BY ts_rank(tc.search_vector, plainto_tsquery('english', {sanitizedQuery})) DESC
                    LIMIT {limit}")
                .AsNoTracking()
                .Select(tc => new TextChunkMatch(
                    tc.PdfDocumentId,
                    tc.Content,
                    tc.ChunkIndex,
                    tc.PageNumber,
                    0f)) // Rank will be assigned during RRF fusion
                .ToListAsync(ct)
                .ConfigureAwait(false);

            _logger.LogDebug("FTS returned {Count} results for game {GameId}", results.Count, gameId);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Full-text search failed for game {GameId}, returning empty", gameId);
            return [];
        }
    }

    public async Task<List<TextChunkMatch>> GetAdjacentChunksAsync(
        Guid pdfDocumentId,
        int chunkIndex,
        int radius,
        CancellationToken ct)
    {
        try
        {
            var minIndex = chunkIndex - radius;
            var maxIndex = chunkIndex + radius;

            var results = await _dbContext.TextChunks
                .AsNoTracking()
                .Where(tc => tc.PdfDocumentId == pdfDocumentId
                    && tc.ChunkIndex >= minIndex
                    && tc.ChunkIndex <= maxIndex
                    && tc.ChunkIndex != chunkIndex) // Exclude the matched chunk itself
                .OrderBy(tc => tc.ChunkIndex)
                .Select(tc => new TextChunkMatch(
                    tc.PdfDocumentId,
                    tc.Content,
                    tc.ChunkIndex,
                    tc.PageNumber,
                    0f))
                .ToListAsync(ct)
                .ConfigureAwait(false);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Adjacent chunk retrieval failed for doc {DocId} chunk {ChunkIndex}",
                pdfDocumentId, chunkIndex);
            return [];
        }
    }

    public async Task<List<TextChunkMatch>> SearchRaptorSummariesAsync(
        Guid gameId,
        string query,
        int topK,
        CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query) || topK <= 0)
                return [];

            var queryTerms = query
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(t => t.ToLowerInvariant())
                .Where(t => t.Length > 1)
                .ToArray();

            if (queryTerms.Length == 0)
                return [];

            var summaries = await _dbContext.RaptorSummaries
                .AsNoTracking()
                .Where(s => s.GameId == gameId)
                .OrderByDescending(s => s.TreeLevel)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var scored = summaries
                .Select(s =>
                {
                    var lowerText = s.SummaryText.ToLowerInvariant();
                    var matchCount = queryTerms.Count(term => lowerText.Contains(term));
                    var score = queryTerms.Length > 0 ? (float)matchCount / queryTerms.Length : 0f;
                    return new { Summary = s, Score = score };
                })
                .Where(x => x.Score > 0f)
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.Summary.TreeLevel)
                .Take(topK)
                .Select(x => new TextChunkMatch(
                    x.Summary.PdfDocumentId,
                    x.Summary.SummaryText,
                    ChunkIndex: -1, // RAPTOR summaries don't have chunk indices
                    PageNumber: null,
                    x.Score))
                .ToList();

            _logger.LogDebug(
                "RAPTOR summary search returned {Count} results for game {GameId}",
                scored.Count, gameId);

            return scored;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "RAPTOR summary search failed for game {GameId}, returning empty", gameId);
            return [];
        }
    }

    /// <summary>
    /// Sanitizes a user query for safe use with plainto_tsquery.
    /// plainto_tsquery handles most sanitization, but we trim excess whitespace.
    /// </summary>
    private static string SanitizeForTsQuery(string query)
    {
        // plainto_tsquery is safe against injection and handles punctuation
        // Just trim and limit length
        var trimmed = query.Trim();
        return trimmed.Length > 500 ? trimmed[..500] : trimmed;
    }
}
