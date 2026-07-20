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

    // Default PostgreSQL FTS configuration when a language is unknown/unspecified.
    // #2569 background: the GENERATED search_vector column on text_chunks/pdf_documents is built
    // with 'english', so a divergent query config against THAT column silently returns nothing.
    // This service now honours per-game language (see ResolveGameFtsConfigAsync): English keeps
    // using the indexed 'english' search_vector column, while non-english languages are matched
    // against a query-time to_tsvector(cfg, Content) so the query config and vector config always
    // agree (sidestepping the #2569 footgun without a multilingual column). See ResolveFtsConfig.
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

        // #2569 follow-up: detect the game's dominant language so Italian content is stemmed with
        // 'italian' instead of the caller's default 'en'. English keeps the indexed search_vector
        // column; non-english uses a query-time to_tsvector with the SAME config (see tsvectorExpr).
        var textSearchConfig = await ResolveGameFtsConfigAsync(gameId, language, cancellationToken).ConfigureAwait(false);

        try
        {
            // Build tsquery for full-text search
            var tsQuery = BuildTsQuery(query, phraseSearch, boostTerms);

            _logger.LogInformation(
                "Keyword search: query='{Query}', gameId={GameId}, phraseSearch={PhraseSearch}, boostTerms={BoostTerms}, limit={Limit}, ftsConfig={FtsConfig}",
                query, gameId, phraseSearch, boostTerms?.Count ?? 0, limit, textSearchConfig);

            // English uses the indexed english 'search_vector' GENERATED column (hot path). Non-english
            // computes the tsvector at query time with the resolved config so the query config always
            // matches the vector config (#2569). tsvectorExpr is one of two fixed internal literals
            // (never user input), so interpolating it into the SQL is injection-safe.
            var tsvectorExpr = string.Equals(textSearchConfig, "english", StringComparison.Ordinal)
                ? "search_vector"
                : "to_tsvector(@textSearchConfig::regconfig, \"Content\")";

            // Execute PostgreSQL full-text search with ts_rank_cd scoring
            // Using FromSqlRaw for complex tsvector queries (EF Core limitation with tsvector operators)
            // Issue #423: Add minScore filter to exclude low-relevance keyword matches (e.g., ToC entries)
            // Perf: subquery avoids double ts_rank_cd evaluation (computed once in inner SELECT, filtered in outer WHERE)
            // Phase D (D6): include role_tags in the projection so the hybrid re-ranker can
            // apply a role-match boost without an extra round-trip.
            var sql = $@"
                SELECT * FROM (
                    SELECT
                        ""Id"",
                        ""Content"",
                        ""PdfDocumentId"",
                        ""GameId"",
                        ""ChunkIndex"",
                        ""PageNumber"",
                        role_tags AS ""RoleTags"",
                        ts_rank_cd({tsvectorExpr}, to_tsquery(@textSearchConfig::regconfig, @tsQuery), @normalization) AS ""RelevanceScore""
                    FROM text_chunks
                    WHERE
                        ""GameId"" = @gameId::uuid
                        AND {tsvectorExpr} @@ to_tsquery(@textSearchConfig::regconfig, @tsQuery)
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

        // Default: OR query (any term may match). #3196 RAG fix: strict AND (" & ") returned 0
        // hits for natural-language questions like "setup per N giocatori" whenever the four
        // surface tokens don't co-occur in one chunk, collapsing hybrid search to vector-only.
        // OR keeps recall; ranking (ts_rank_cd + RRF fusion + reranker) sorts the candidates.
        return sanitizedQuery.Replace(" ", " | ");
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
    /// Maps a document/query language to a PostgreSQL FTS configuration.
    /// <para>
    /// English resolves to <c>'english'</c> so the query can keep using the indexed english
    /// <c>search_vector</c> GENERATED column (the common case). Non-english languages resolve to
    /// their snowball config and are matched against a query-time <c>to_tsvector(cfg, Content)</c>
    /// (see the SQL in <see cref="SearchAsync"/>), so the query config and the vector config
    /// always agree — this sidesteps the #2569 footgun (an <c>'italian'</c> query against the
    /// <c>'english'</c> column silently returns nothing) without needing a multilingual column.
    /// Unknown languages resolve to <c>'simple'</c> (tokenize, no stemming) which is safe under
    /// the same "query-time to_tsvector with the same config" rule.
    /// </para>
    /// </summary>
    internal static string ResolveFtsConfig(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return DefaultTextSearchConfig;
        }

        return language.Trim().ToLowerInvariant() switch
        {
            "en" or "eng" or "english" => "english",
            "it" or "ita" or "italian" or "italiano" => "italian",
            "de" or "deu" or "ger" or "german" or "deutsch" => "german",
            "fr" or "fra" or "french" or "francais" or "français" => "french",
            "es" or "spa" or "spanish" or "espanol" or "español" => "spanish",
            "pt" or "por" or "portuguese" or "portugues" or "português" => "portuguese",
            "nl" or "dut" or "nld" or "dutch" => "dutch",
            _ => "simple",
        };
    }

    /// <summary>
    /// Detects the dominant document language for a game (from <c>pdf_documents.Language</c>,
    /// joined via the game's chunks) and resolves it to an FTS config. Keyword-retrieval callers
    /// do not thread a per-game language, so it is detected here rather than pinned to english.
    /// Falls back to <paramref name="requestedLanguage"/> (then english) when unknown/unavailable.
    /// </summary>
    private async Task<string> ResolveGameFtsConfigAsync(Guid gameId, string requestedLanguage, CancellationToken cancellationToken)
    {
        try
        {
            var previousTimeout = _dbContext.Database.GetCommandTimeout();
            _dbContext.Database.SetCommandTimeout(3);
            var dominant = await _dbContext.Database
                .SqlQueryRaw<string>(@"
                    SELECT pd.""Language"" AS ""Value""
                    FROM text_chunks tc
                    JOIN pdf_documents pd ON pd.""Id"" = tc.""PdfDocumentId""
                    WHERE tc.""GameId"" = @gameId::uuid AND pd.""Language"" IS NOT NULL AND pd.""Language"" <> ''
                    GROUP BY pd.""Language""
                    ORDER BY count(*) DESC
                    LIMIT 1",
                    new NpgsqlParameter("@gameId", gameId.ToString()))
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            _dbContext.Database.SetCommandTimeout(previousTimeout);

            return ResolveFtsConfig(string.IsNullOrWhiteSpace(dominant) ? requestedLanguage : dominant);
        }
#pragma warning disable CA1031 // Do not catch general exception types - language detection is best-effort
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Game FTS language detection failed for {GameId}; falling back to requested language", gameId);
            return ResolveFtsConfig(requestedLanguage);
        }
#pragma warning restore CA1031
    }

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