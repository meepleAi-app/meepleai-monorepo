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

    // PostgreSQL full-text search configuration
    // ADR-016 Phase 3: Default to Italian for MeepleAI board game rules
    private const string DefaultTextSearchConfig = "meepleai_italian";
    private const string EnglishTextSearchConfig = "english";
    private const int DefaultNormalization = 1; // ts_rank_cd normalization method (1 = divide by document length)

    /// <summary>
    /// Mapping of language codes to PostgreSQL text search configurations.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> LanguageToFtsConfig = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "it", "meepleai_italian" },
        { "italian", "meepleai_italian" },
        { "en", "english" },
        { "english", "english" }
    };

    public KeywordSearchService(
        MeepleAiDbContext dbContext,
        ILogger<KeywordSearchService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IReadOnlyList<KeywordSearchResult>> SearchAsync(
        string query,
        Guid gameId,
        int limit = 10,
        bool phraseSearch = false,
        IReadOnlyList<string>? boostTerms = null,
        string language = "it",
        CancellationToken cancellationToken = default)
    {
        // Issue #1445: Use centralized query validation
        var queryError = QueryValidator.ValidateQuery(query);
        if (queryError != null)
        {
            _logger.LogWarning("Invalid query provided to KeywordSearchService: {Error}", queryError);
            return Array.Empty<KeywordSearchResult>();
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
            var sql = @"
                SELECT
                    ""Id"",
                    ""Content"",
                    ""PdfDocumentId"",
                    ""GameId"",
                    ""ChunkIndex"",
                    ""PageNumber"",
                    ts_rank_cd(search_vector, to_tsquery(@textSearchConfig::regconfig, @tsQuery), @normalization) AS ""RelevanceScore""
                FROM text_chunks
                WHERE
                    ""GameId"" = @gameId::uuid
                    AND search_vector @@ to_tsquery(@textSearchConfig::regconfig, @tsQuery)
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

    public async Task<IReadOnlyList<KeywordDocumentResult>> SearchDocumentsAsync(
        string query,
        Guid gameId,
        int limit = 10,
        string language = "it",
        CancellationToken cancellationToken = default)
    {
        // Issue #1445: Use centralized query validation
        var queryError = QueryValidator.ValidateQuery(query);
        if (queryError != null)
        {
            // Return empty results for invalid queries (maintains existing behavior)
            return Array.Empty<KeywordDocumentResult>();
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
    private string BuildTsQuery(string query, bool phraseSearch, IReadOnlyList<string>? boostTerms)
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
    /// Resolves a language code to the corresponding PostgreSQL FTS configuration.
    /// ADR-016 Phase 3: Maps "it" → meepleai_italian (with game synonyms), "en" → english.
    /// </summary>
    /// <param name="language">Language code (e.g., "it", "en", "italian", "english")</param>
    /// <returns>PostgreSQL text search configuration name</returns>
    private string ResolveFtsConfig(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            _logger.LogDebug("Empty language provided, using default FTS config: {Config}", DefaultTextSearchConfig);
            return DefaultTextSearchConfig;
        }

        if (LanguageToFtsConfig.TryGetValue(language, out var ftsConfig))
        {
            return ftsConfig;
        }

        _logger.LogWarning(
            "Unknown language '{Language}' for FTS config, falling back to English",
            language);
        return EnglishTextSearchConfig;
    }

    /// <summary>
    /// Extracts matched terms from query for frontend highlighting.
    /// </summary>
    private IReadOnlyList<string> ExtractMatchedTerms(string query, bool phraseSearch)
    {
        if (phraseSearch)
        {
            return new[] { query.Trim('"').Trim() };
        }

        return query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => t.Length > 2)
            .ToArray();
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
