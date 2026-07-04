using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Helpers;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

#pragma warning disable MA0048 // File name must match type name - Contains Service with Configuration classes
namespace Api.Services;

/// <summary>
/// PostgreSQL full-text keyword search service using tsvector and ts_rank_cd.
/// Implements BM25-style ranking with phrase search and terminology boosting.
/// Part of AI-14 hybrid search implementation.
/// ADR-016 Phase 3: Supports Italian (meepleai_italian) and English FTS configurations.
/// </summary>
internal class KeywordSearchService : IKeywordSearchService
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<KeywordSearchService> _logger;

    // PostgreSQL full-text search configuration.
    // #2569: keyword FTS is 'english' to match the content (PdfDocument.Language defaults to
    // "en"), the pgvector path (PgVectorStoreAdapter uses 'english'), and — critically — the
    // GENERATED search_vector column on text_chunks/pdf_documents (also 'english'). Query config
    // and column config MUST agree or the @@ operator silently returns nothing. There is NO
    // per-language mapping on purpose: the column is single-config, so an 'italian' query would
    // never match. A multilingual mapping returns only when a multilingual column exists
    // (ADR-016 follow-up). See ResolveFtsConfig.
    private const string DefaultTextSearchConfig = "english";
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
        string language = "en",
        double minScore = 0.0,
        CancellationToken cancellationToken = default)
    {
        // Issue #1445: Use centralized query validation
        var queryError = QueryValidator.ValidateQuery(query);
        if (queryError != null)
        {
            _logger.LogWarning("Invalid query provided to KeywordSearchService: {Error}", queryError);
            return new List<KeywordSearchResult>();
        }

        // Security: Cap limit parameter to prevent resource exhaustion
        var safeLimit = Math.Min(Math.Max(limit, 1), 100); // Min: 1, Max: 100
        if (safeLimit != limit)
        {
            _logger.LogInformation("Limit capped from {OriginalLimit} to {SafeLimit}", limit, safeLimit);
        }

        var gameIdString = gameId.ToString();

        // ADR-016 Phase 3: Resolve language to FTS configuration
        var textSearchConfig = ResolveFtsConfig(language);

        try
        {
            // Build tsquery for full-text search
            var tsQuery = BuildTsQuery(query, phraseSearch, boostTerms);

            _logger.LogInformation(
                "Keyword search: query='{Query}', gameId={GameId}, phraseSearch={PhraseSearch}, boostTerms={BoostTerms}, limit={Limit}, ftsConfig={FtsConfig}",
                query, gameId, phraseSearch, boostTerms?.Count ?? 0, limit, textSearchConfig);

            // Execute PostgreSQL full-text search with ts_rank_cd scoring
            // Using FromSqlRaw for complex tsvector queries (EF Core limitation with tsvector operators)
            // Issue #423: Add minScore filter to exclude low-relevance keyword matches (e.g., ToC entries)
            // Perf: subquery avoids double ts_rank_cd evaluation (computed once in inner SELECT, filtered in outer WHERE)
            // Phase D (D6): include role_tags in the projection so the hybrid re-ranker can
            // apply a role-match boost without an extra round-trip.
            var sql = @"
                SELECT * FROM (
                    SELECT
                        ""Id"",
                        ""Content"",
                        ""PdfDocumentId"",
                        ""GameId"",
                        ""ChunkIndex"",
                        ""PageNumber"",
                        role_tags AS ""RoleTags"",
                        ts_rank_cd(search_vector, to_tsquery(@textSearchConfig::regconfig, @tsQuery), @normalization) AS ""RelevanceScore""
                    FROM text_chunks
                    WHERE
                        ""GameId"" = @gameId::uuid
                        AND search_vector @@ to_tsquery(@textSearchConfig::regconfig, @tsQuery)
                ) ranked
                WHERE ""RelevanceScore"" >= @minScore
                ORDER BY ""RelevanceScore"" DESC
                LIMIT @limit";

            // Security: Set query timeout to prevent long-running queries (DoS protection)
            var previousTimeout = _dbContext.Database.GetCommandTimeout();
            _dbContext.Database.SetCommandTimeout(5); // 5 seconds max for search queries

            var results = await _dbContext.Database
                .SqlQueryRaw<KeywordSearchRawResult>(
                    sql,
                    new NpgsqlParameter("@textSearchConfig", textSearchConfig),
                    new NpgsqlParameter("@tsQuery", tsQuery),
                    new NpgsqlParameter("@normalization", DefaultNormalization),
                    new NpgsqlParameter("@gameId", gameIdString),
                    new NpgsqlParameter("@minScore", minScore),
                    new NpgsqlParameter("@limit", safeLimit)) // Use capped limit
                .AsNoTracking()
                .ToListAsync(cancellationToken).ConfigureAwait(false);

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
                MatchedTerms = matchedTerms,
                // Phase D (D6): SQL projects role_tags as int; cast to flag enum.
                RoleTags = (GameBookRole)r.RoleTags
            }).ToList();

            _logger.LogInformation(
                "Keyword search completed: found {ResultCount} results",
                keywordResults.Count);

            return keywordResults;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            // S2139: Logging removed. Wrapped for context.
            // CA1031: We catch all exceptions to ensure comprehensive error handling for the service boundary.
            throw new InvalidOperationException($"Error during keyword search for query '{query}': {ex.Message}", ex);
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    public async Task<List<KeywordDocumentResult>> SearchDocumentsAsync(
        string query,
        Guid gameId,
        int limit = 10,
        string language = "en",
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

        // ADR-016 Phase 3: Resolve language to FTS configuration
        var textSearchConfig = ResolveFtsConfig(language);

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
                    ts_rank_cd(search_vector, to_tsquery(@textSearchConfig::regconfig, @tsQuery), @normalization) AS ""RelevanceScore""
                FROM pdf_documents
                WHERE
                    ""GameId"" = @gameId::uuid
                    AND search_vector @@ to_tsquery(@textSearchConfig::regconfig, @tsQuery)
                ORDER BY ""RelevanceScore"" DESC
                LIMIT @limit";

            var results = await _dbContext.Database
                .SqlQueryRaw<KeywordDocumentRawResult>(
                    sql,
                    new NpgsqlParameter("@textSearchConfig", textSearchConfig),
                    new NpgsqlParameter("@tsQuery", tsQuery),
                    new NpgsqlParameter("@normalization", DefaultNormalization),
                    new NpgsqlParameter("@gameId", gameIdString),
                    new NpgsqlParameter("@limit", safeLimit)) // Use capped limit
                .AsNoTracking()
                .ToListAsync(cancellationToken).ConfigureAwait(false);

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
            // S2139: Logging removed. Wrapped for context.
            // CA1031: We catch all exceptions to ensure comprehensive error handling for the service boundary.
            throw new InvalidOperationException($"Error during document keyword search for query '{query}': {ex.Message}", ex);
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    /// <summary>
    /// Builds a PostgreSQL tsquery from a search query with phrase search and boost support.
    /// </summary>
    /// <remarks>
    /// Examples:
    /// - Simple: "castling" returns "castling"
    /// - Phrase: "en passant" with phraseSearch=true returns "en passant" with proximity operator
    /// - Boost: "check" with boostTerms=["check", "checkmate"] returns boosted query
    /// </remarks>
    private string BuildTsQuery(string query, bool phraseSearch, List<string>? boostTerms)
    {
        // Sanitize query to prevent SQL injection and tsquery syntax errors
        var sanitizedQuery = SanitizeQuery(query);

        // Handle phrase search with proximity operator <->
        if (phraseSearch && sanitizedQuery.Contains(' '))
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
    /// Resolves the PostgreSQL FTS configuration for keyword search.
    /// #2569 footgun guard: ALWAYS returns <see cref="DefaultTextSearchConfig"/> ('english'),
    /// ignoring <paramref name="language"/>. The <c>search_vector</c> column is a single-config
    /// GENERATED column built with 'english'; a divergent query config (e.g. 'italian') would
    /// make the <c>@@</c> operator silently return nothing. True per-query / multilingual FTS
    /// requires a multilingual <c>search_vector</c> column (ADR-016 follow-up); until that
    /// exists the query config is pinned to the column's config. The parameter is retained for
    /// forward-compatibility (and so existing call sites need no change).
    /// </summary>
    /// <param name="language">Reserved; currently ignored — keyword FTS is english-only (#2569).</param>
    /// <returns>The PostgreSQL text search configuration ('english').</returns>
    internal static string ResolveFtsConfig(string language) => DefaultTextSearchConfig;

    /// <summary>
    /// Extracts matched terms from query for frontend highlighting.
    /// </summary>
    private List<string> ExtractMatchedTerms(string query, bool phraseSearch)
    {
        if (phraseSearch)
        {
            return new List<string> { query.Trim('"').Trim() };
        }

        return query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => t.Length > 2)
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

    /// <summary>
    /// Phase D (D6): role_tags column from text_chunks (stored as int per <see cref="GameBookRole"/> bitflag).
    /// Defaults to 0 (None) when the chunk has not been classified.
    /// </summary>
    public int RoleTags { get; set; }
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