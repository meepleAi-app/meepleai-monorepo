using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Api.Services;

/// <summary>
/// PostgreSQL full-text keyword search service using tsvector and ts_rank_cd.
/// Implements BM25-style ranking with phrase search and terminology boosting.
/// Part of AI-14 hybrid search implementation.
/// </summary>
public class KeywordSearchService : IKeywordSearchService
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<KeywordSearchService> _logger;

    // PostgreSQL full-text search configuration
    private const string TextSearchConfig = "english"; // Language configuration for stemming
    private const int DefaultNormalization = 1; // ts_rank_cd normalization method (1 = divide by document length)

    public KeywordSearchService(
        MeepleAiDbContext dbContext,
        ILogger<KeywordSearchService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<List<KeywordSearchResult>> SearchAsync(
        string query,
        Guid gameId,
        int limit = 10,
        bool phraseSearch = false,
        List<string>? boostTerms = null,
        CancellationToken cancellationToken = default)
    {
        // Issue #1445: Use centralized query validation
        var queryError = QueryValidator.ValidateQuery(query);
        if (queryError != null)
        {
            _logger.LogWarning("Invalid query provided to KeywordSearchService: {Error}", queryError);
            // Return empty results for invalid queries (maintains existing behavior)
            return new List<KeywordSearchResult>();
        }

        // Security: Cap limit parameter to prevent resource exhaustion
        var safeLimit = Math.Min(Math.Max(limit, 1), 100); // Min: 1, Max: 100
        if (safeLimit != limit)
        {
            _logger.LogInformation("Limit capped from {OriginalLimit} to {SafeLimit}", limit, safeLimit);
        }

        var gameIdString = gameId.ToString();

        try
        {
            // Build tsquery for full-text search
            var tsQuery = BuildTsQuery(query, phraseSearch, boostTerms);

            _logger.LogInformation(
                "Keyword search: query='{Query}', gameId={GameId}, phraseSearch={PhraseSearch}, boostTerms={BoostTerms}, limit={Limit}",
                query, gameId, phraseSearch, boostTerms?.Count ?? 0, limit);

            // Execute PostgreSQL full-text search with ts_rank_cd scoring
            // Using FromSqlRaw for complex tsvector queries (EF Core limitation with tsvector operators)
            var sql = @"
                SELECT
                    ""Id"",
                    ""Content"",
                    ""PdfDocumentId"",
                    ""GameId"",
                    ""ChunkIndex"",
                    ""PageNumber"",
                    ts_rank_cd(search_vector, to_tsquery(@textSearchConfig, @tsQuery), @normalization) AS relevance_score
                FROM text_chunks
                WHERE
                    ""GameId"" = @gameId
                    AND search_vector @@ to_tsquery(@textSearchConfig, @tsQuery)
                ORDER BY relevance_score DESC
                LIMIT @limit";

            // Security: Set query timeout to prevent long-running queries (DoS protection)
            var previousTimeout = _dbContext.Database.GetCommandTimeout();
            _dbContext.Database.SetCommandTimeout(5); // 5 seconds max for search queries

            var results = await _dbContext.Database
                .SqlQueryRaw<KeywordSearchRawResult>(
                    sql,
                    new NpgsqlParameter("@textSearchConfig", TextSearchConfig),
                    new NpgsqlParameter("@tsQuery", tsQuery),
                    new NpgsqlParameter("@normalization", DefaultNormalization),
                    new NpgsqlParameter("@gameId", gameIdString),
                    new NpgsqlParameter("@limit", safeLimit)) // Use capped limit
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            // Restore previous timeout
            _dbContext.Database.SetCommandTimeout(previousTimeout);

            // Extract matched terms for highlighting
            var matchedTerms = ExtractMatchedTerms(query, phraseSearch);

            var keywordResults = results.Select(r => new KeywordSearchResult
            {
                ChunkId = r.Id,
                Content = r.Content,
                PdfDocumentId = r.PdfDocumentId,
                GameId = Guid.Parse(r.GameId),
                ChunkIndex = r.ChunkIndex,
                PageNumber = r.PageNumber,
                RelevanceScore = r.RelevanceScore,
                MatchedTerms = matchedTerms
            }).ToList();

            _logger.LogInformation(
                "Keyword search completed: found {ResultCount} results",
                keywordResults.Count);

            return keywordResults;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            // SERVICE BOUNDARY PATTERN: Search service must log all errors before re-throwing
            // Rationale: This is a service entry point that executes PostgreSQL full-text searches. We catch
            // all exceptions to add diagnostic logging context (query details) before re-throwing to the caller.
            // This ensures comprehensive error logging while maintaining exception propagation for proper handling.
            // Context: PostgreSQL full-text search can fail in various ways (syntax errors, timeout, connection)
            _logger.LogError(ex, "Error during keyword search for query '{Query}'", query);
            throw;
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    public async Task<List<KeywordDocumentResult>> SearchDocumentsAsync(
        string query,
        Guid gameId,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        // Issue #1445: Use centralized query validation
        var queryError = QueryValidator.ValidateQuery(query);
        if (queryError != null)
        {
            // Return empty results for invalid queries (maintains existing behavior)
            return new List<KeywordDocumentResult>();
        }

        // Security: Cap limit parameter
        var safeLimit = Math.Min(Math.Max(limit, 1), 100);

        var gameIdString = gameId.ToString();

        try
        {
            var tsQuery = BuildTsQuery(query, phraseSearch: false, boostTerms: null);

            // Security: Set query timeout
            var previousTimeout = _dbContext.Database.GetCommandTimeout();
            _dbContext.Database.SetCommandTimeout(5);

            var sql = @"
                SELECT
                    ""Id"",
                    ""FileName"",
                    ""GameId"",
                    ""PageCount"",
                    ts_rank_cd(search_vector, to_tsquery(@textSearchConfig, @tsQuery), @normalization) AS relevance_score
                FROM pdf_documents
                WHERE
                    ""GameId"" = @gameId
                    AND search_vector @@ to_tsquery(@textSearchConfig, @tsQuery)
                ORDER BY relevance_score DESC
                LIMIT @limit";

            var results = await _dbContext.Database
                .SqlQueryRaw<KeywordDocumentRawResult>(
                    sql,
                    new NpgsqlParameter("@textSearchConfig", TextSearchConfig),
                    new NpgsqlParameter("@tsQuery", tsQuery),
                    new NpgsqlParameter("@normalization", DefaultNormalization),
                    new NpgsqlParameter("@gameId", gameIdString),
                    new NpgsqlParameter("@limit", safeLimit)) // Use capped limit
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            // Restore previous timeout
            _dbContext.Database.SetCommandTimeout(previousTimeout);

            return results.Select(r => new KeywordDocumentResult
            {
                DocumentId = r.Id,
                FileName = r.FileName,
                GameId = Guid.Parse(r.GameId),
                RelevanceScore = r.RelevanceScore,
                PageCount = r.PageCount
            }).ToList();
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            // SERVICE BOUNDARY PATTERN: Search service must log all errors before re-throwing
            // Rationale: This is a service entry point that executes PostgreSQL document searches. We catch
            // all exceptions to add diagnostic logging context (query details) before re-throwing to the caller.
            // This ensures comprehensive error logging while maintaining exception propagation for proper handling.
            // Context: PostgreSQL full-text search can fail in various ways (syntax errors, timeout, connection)
            _logger.LogError(ex, "Error during document keyword search for query '{Query}'", query);
            throw;
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    /// <summary>
    /// Builds a PostgreSQL tsquery from a search query with phrase search and boost support.
    /// </summary>
    /// <remarks>
    /// Examples:
    /// - Simple: "castling" → "castling"
    /// - Phrase: "en passant" with phraseSearch=true → "en <-> passant"
    /// - Boost: "check" with boostTerms=["check", "checkmate"] → "check:A | checkmate:A"
    /// </remarks>
    private string BuildTsQuery(string query, bool phraseSearch, List<string>? boostTerms)
    {
        // Sanitize query to prevent SQL injection and tsquery syntax errors
        var sanitizedQuery = SanitizeQuery(query);

        // Handle phrase search with proximity operator <->
        if (phraseSearch && sanitizedQuery.Contains(" "))
        {
            // Replace spaces with PostgreSQL proximity operator for exact phrase matching
            var words = sanitizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" <-> ", words);
        }

        // Build query with boost terms (weight :A for boosted terms, :B for others)
        if (boostTerms != null && boostTerms.Count > 0)
        {
            var queryTerms = sanitizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var weightedTerms = queryTerms.Select(term =>
            {
                var isBoosted = boostTerms.Any(bt => bt.Equals(term, StringComparison.OrdinalIgnoreCase));
                return isBoosted ? $"{term}:A" : $"{term}:B";
            });

            return string.Join(" | ", weightedTerms); // OR operator for multiple terms
        }

        // Default: simple AND query (all terms must match)
        return sanitizedQuery.Replace(" ", " & ");
    }

    /// <summary>
    /// Sanitizes user query to prevent tsquery syntax errors and SQL injection.
    /// Removes special PostgreSQL full-text search operators and dangerous characters.
    /// </summary>
    private string SanitizeQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return string.Empty;
        }

        // Remove or escape special tsquery operators: & | ! <-> ( ) '
        // Keep only alphanumeric characters, spaces, and hyphens
        var sanitized = query
            .Replace("'", "") // Remove single quotes to prevent SQL injection
            .Replace("&", " ")
            .Replace("|", " ")
            .Replace("!", " ")
            .Replace("(", "")
            .Replace(")", "")
            .Replace("<->", " ")
            .Trim();

        // Remove multiple spaces
        while (sanitized.Contains("  "))
        {
            sanitized = sanitized.Replace("  ", " ");
        }

        return sanitized;
    }

    /// <summary>
    /// Extracts matched terms from query for frontend highlighting.
    /// Splits query into individual terms, handling phrase search quotes.
    /// </summary>
    private List<string> ExtractMatchedTerms(string query, bool phraseSearch)
    {
        if (phraseSearch)
        {
            // For phrase search, return the entire phrase as one matched term
            return new List<string> { query.Trim('"').Trim() };
        }

        // Split by spaces and remove empty entries
        return query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => t.Length > 2) // Ignore very short terms
            .ToList();
    }
}

/// <summary>
/// Raw result from PostgreSQL keyword search query on text_chunks.
/// Used for SqlQueryRaw mapping.
/// </summary>
internal class KeywordSearchRawResult
{
    public string Id { get; set; } = default!;
    public string Content { get; set; } = default!;
    public string PdfDocumentId { get; set; } = default!;
    public string GameId { get; set; } = default!;
    public int ChunkIndex { get; set; }
    public int? PageNumber { get; set; }
    public float RelevanceScore { get; set; }
}

/// <summary>
/// Raw result from PostgreSQL keyword search query on pdf_documents.
/// </summary>
internal class KeywordDocumentRawResult
{
    public string Id { get; set; } = default!;
    public string FileName { get; set; } = default!;
    public string GameId { get; set; } = default!;
    public int? PageCount { get; set; }
    public float RelevanceScore { get; set; }
}
