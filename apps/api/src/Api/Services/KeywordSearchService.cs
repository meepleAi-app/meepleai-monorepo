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
    // The chunk search (SearchAsync) now honours per-game language (see ResolveGameFtsConfigAsync):
    // English keeps using the indexed 'english' search_vector column, while non-english languages
    // are matched against a query-time to_tsvector(cfg, Content) so the query config and vector
    // config always agree (sidestepping the #2569 footgun without a multilingual column). The
    // document search (SearchDocumentsAsync) stays english-pinned — see its own note. See
    // ResolveFtsConfig.
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

        // #3768: il titolo del gioco serve a ESCLUDERE i suoi token dalla tsquery, non a cercarli.
        var gameTitle = await ResolveGameTitleAsync(gameId, cancellationToken).ConfigureAwait(false);

        try
        {
            // Build tsquery for full-text search (Slice A: synonym expansion is language-aware,
            // so the resolved per-game FTS config is threaded through to BuildTsQuery).
            var tsQuery = BuildTsQuery(query, phraseSearch, textSearchConfig, gameTitle);

            // #3768: senza token residui non c'è niente da cercare. `to_tsquery(cfg, '')` è un
            // errore SQL, non un risultato vuoto, quindi il caso va intercettato qui — e restituire
            // zero risultati è corretto: un candidato senza segnale lessicale conserva il suo
            // VectorScore nella fusione, dove `absent = 0` è load-bearing (#3735).
            if (string.IsNullOrWhiteSpace(tsQuery))
            {
                _logger.LogInformation(
                    "Keyword search: query='{Query}', gameId={GameId} — nessun token oltre al nome del gioco, braccio lessicale saltato (#3768)",
                    query, gameId);
                return new List<KeywordSearchResult>();
            }

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
                        ""Heading"",
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
                ChunkId = r.Id.ToString(),
                Content = r.Content,
                PdfDocumentId = r.PdfDocumentId.ToString(),
                GameId = r.GameId,
                ChunkIndex = r.ChunkIndex,
                PageNumber = r.PageNumber,
                RelevanceScore = r.RelevanceScore,
                MatchedTerms = matchedTerms,
                // Phase D (D6): SQL projects role_tags as int; cast to flag enum.
                RoleTags = (GameBookRole)r.RoleTags,
                // #3270: carry the chunk heading for the heading-match boost.
                Heading = r.Heading
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

        // Document-level keyword search stays pinned to the english 'search_vector' column.
        // Unlike SearchAsync it does NOT use a query-time to_tsvector, so a non-english config
        // here would reintroduce the #2569 footgun (e.g. an 'italian' query against the english
        // column silently returns 0 rows). This path has no callers today (only SearchAsync is
        // used), so pinning to english keeps it correct and footgun-free.
        var textSearchConfig = DefaultTextSearchConfig;

        try
        {
            // English-pinned (DefaultTextSearchConfig) → no synonym expansion, by design.
            var tsQuery = BuildTsQuery(query, phraseSearch: false, DefaultTextSearchConfig);

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
                DocumentId = r.Id.ToString(),
                FileName = r.FileName,
                GameId = r.GameId,
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
    /// Builds a PostgreSQL tsquery from a search query. Non-phrase queries become a recall-first OR
    /// query with curated per-language intent-synonym expansion (see <see cref="ExpandTermsToTsQuery"/>);
    /// phrase queries (quoted) become an exact <c>&lt;-&gt;</c> proximity match.
    /// </summary>
    /// <remarks>
    /// Examples:
    /// - Simple (english): "castling" returns "castling"
    /// - Phrase: "en passant" with phraseSearch=true returns "en &lt;-&gt; passant"
    /// - Synonym (italian): "setup" returns "(setup | preparazione | allestimento)"
    /// </remarks>
    internal static string BuildTsQuery(string query, bool phraseSearch, string ftsConfig, string? gameTitle = null)
    {
        // Sanitize query to prevent SQL injection and tsquery syntax errors
        var sanitizedQuery = SanitizeQuery(query);

        // #3768: togli i token del NOME DEL GIOCO. Questa ricerca gira gia' filtrata per GameId,
        // quindi ogni candidato appartiene a quel gioco e il nome vi compare ovunque: e' un termine
        // a IDF nullo, che non sceglie fra i chunk ma premia quelli che lo ripetono di piu'.
        //
        // Misurato su staging per `catan-setup-it`: il rango 1 del braccio lessicale era la pagina
        // di copyright («Copyright © 2025 CATAN GmbH…», ts_rank_cd 0.2256) davanti alle regole vere,
        // e con ts_rank_cd normalizzato per lunghezza un chunk breve che ripete il nome vince
        // sistematicamente. Quei candidati sono quelli che il gioco manda alla fusione globale.
        //
        // Il filtro NON si applica al percorso senza gameId (gameTitle null): li' il nome del gioco
        // e' esattamente il segnale che sceglie il gioco (#3735).
        sanitizedQuery = RemoveGameTitleTokens(sanitizedQuery, gameTitle);

        // Handle phrase search with proximity operator <->
        if (phraseSearch && sanitizedQuery.Contains(' '))
        {
            // Replace spaces with PostgreSQL proximity operator for exact phrase matching
            var words = sanitizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" <-> ", words);
        }

        // Default: recall-first OR query. A #3241 follow-up switched strict AND to OR because
        // natural-language questions like "setup per N giocatori" returned 0 hits when the surface
        // tokens didn't co-occur in one chunk (collapsing hybrid to vector-only). Ranking
        // (ts_rank_cd + RRF fusion + reranker + legend-demotion) sorts the candidates.
        //
        // Slice A: also expand curated per-language intent synonyms (e.g. setup ->
        // preparazione/allestimento) so the query matches native rulebook lexemes. No-op for
        // non-tabled configs, so English recall is unchanged.
        //
        // NOTE: the previous ":A"/":B" boost-weighting branch was REMOVED here. Those query weight
        // labels only match lexemes carrying the same weight in the tsvector, but the generated
        // search_vector is a plain to_tsvector('english', Content) with NO setweight (all lexemes
        // are weight D), so ":A"/":B" matched *nothing* — silently killing the keyword arm (and
        // shadowing this expansion) for every real query on the production hybrid path, which
        // always passes a non-empty BoostTerms list. Re-introducing boost ranking requires
        // setweight on the tsvector (a migration + re-index), tracked separately.
        return ExpandTermsToTsQuery(sanitizedQuery, ftsConfig);
    }

    /// <summary>
    /// Curated, language-keyed intent synonym tables for keyword-arm expansion. Keyed by the
    /// resolved PostgreSQL FTS config (<see cref="ResolveFtsConfig"/>); only languages with a
    /// curated table are expanded. Kept deliberately small and high-signal (divergent-stem intent
    /// synonyms) to avoid recall noise. Head-token lookup is case-insensitive.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>> SynonymTablesByConfig =
        new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.Ordinal)
        {
            ["italian"] = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                // Setup intent — "setup" is an English loanword whose 'italian' stem diverges from
                // the native rulebook terms; many Italian rulebooks also use "Setup" as a section
                // title, so the mapping is kept symmetric (query in either lexeme matches both).
                ["setup"] = new[] { "preparazione", "allestimento" },
                ["preparazione"] = new[] { "setup", "allestimento" },
                ["allestimento"] = new[] { "setup", "preparazione" },
                // Player-count intent — "per N giocatori" <-> "numero (di) giocatori".
                ["giocatori"] = new[] { "numero giocatori" },
                ["giocatore"] = new[] { "numero giocatori" },
            },
        };

    /// <summary>
    /// #3338 WP1c: expands heading-match query terms with the SAME per-language intent synonyms the
    /// keyword arm uses (<see cref="SynonymTablesByConfig"/>), so a query lexeme like "setup" fires the
    /// #3270 heading-match boost on a chunk whose heading is the native rulebook term ("preparazione").
    /// Returns the original terms plus synonyms — lowercased, length ≥ 3, order-preserving,
    /// de-duplicated. No-op for a null/blank config or a config without a curated table
    /// (english/simple/…), so English retrieval is byte-identical.
    /// </summary>
    internal static IReadOnlyList<string> ExpandHeadingMatchTerms(IReadOnlyList<string>? terms, string? ftsConfig)
    {
        if (terms is null || terms.Count == 0
            || string.IsNullOrEmpty(ftsConfig)
            || !SynonymTablesByConfig.TryGetValue(ftsConfig, out var synonyms))
        {
            return terms ?? Array.Empty<string>();
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var expanded = new List<string>(terms.Count);

        void Add(string candidate)
        {
            var lower = candidate.ToLowerInvariant();
            if (lower.Length >= 3 && seen.Add(lower))
            {
                expanded.Add(lower);
            }
        }

        foreach (var term in terms)
        {
            Add(term);
            if (synonyms.TryGetValue(term, out var syns))
            {
                foreach (var s in syns)
                {
                    Add(s);
                }
            }
        }

        return expanded;
    }

    /// <summary>
    /// Expands keyword-arm query terms with curated, language-keyed intent synonyms, emitting a
    /// valid <c>to_tsquery</c> OR-fragment.
    /// <para>
    /// The failing "Setup per N giocatori" query missed the Italian rulebook lexemes
    /// ("preparazione"/"allestimento") because the English loanword "setup" stems to a different
    /// lexeme under the 'italian' FTS config. Expansion runs ONLY on the keyword arm (never the
    /// embedding vector, which is produced by a separate call in <c>HybridSearchService</c>), so it
    /// cannot dilute semantic search. Non-tabled configs (english/simple/…) reproduce the plain OR
    /// join verbatim — zero recall/precision drift outside the curated table.
    /// </para>
    /// <para>
    /// A token with a synonym entry becomes a grouped alternation <c>(head | syn1 | syn2)</c>;
    /// multi-word synonyms are joined with the proximity operator <c>&lt;-&gt;</c> (a bare space is
    /// a to_tsquery syntax error). <paramref name="sanitizedQuery"/> is already operator/paren
    /// stripped by <see cref="SanitizeQuery"/>, and the injected grouping characters come only from
    /// this controlled table, so the fragment is injection-safe.
    /// </para>
    /// </summary>
    internal static string ExpandTermsToTsQuery(string sanitizedQuery, string ftsConfig)
    {
        if (string.IsNullOrWhiteSpace(sanitizedQuery))
        {
            return string.Empty;
        }

        var tokens = sanitizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (!SynonymTablesByConfig.TryGetValue(ftsConfig, out var synonyms))
        {
            // No curated table for this language → preserve the pre-slice OR join verbatim.
            return string.Join(" | ", tokens);
        }

        var groups = tokens.Select(token =>
        {
            if (!synonyms.TryGetValue(token, out var alternatives) || alternatives.Count == 0)
            {
                return token; // no synonyms → bare token (never an empty group)
            }

            var members = new List<string>(alternatives.Count + 1) { token };
            members.AddRange(alternatives.Select(ToTsQueryPhrase));
            return $"({string.Join(" | ", members)})";
        });

        return string.Join(" | ", groups);
    }

    /// <summary>
    /// Renders a (possibly multi-word) synonym value as a to_tsquery term: single words pass
    /// through; multi-word values are joined with the <c>&lt;-&gt;</c> proximity operator.
    /// </summary>
    private static string ToTsQueryPhrase(string synonym)
    {
        var words = synonym.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length == 1 ? words[0] : string.Join(" <-> ", words);
    }

    /// <summary>
    /// Sanitizes user query to prevent tsquery syntax errors and SQL injection.
    /// Removes special PostgreSQL full-text search operators and dangerous characters.
    /// </summary>
    /// <summary>
    /// Rimuove dai token della query quelli che compongono il titolo del gioco (#3768).
    /// </summary>
    /// <remarks>
    /// Restituisce la query invariata quando <paramref name="gameTitle"/> è null o vuoto — il
    /// percorso non filtrato per gioco. Può restituire stringa vuota se la query era fatta solo del
    /// nome del gioco: il chiamante deve trattarla come «niente da cercare» e saltare la ricerca
    /// lessicale, perché <c>to_tsquery(cfg, '')</c> è un errore SQL, non un risultato vuoto.
    /// </remarks>
    private static string RemoveGameTitleTokens(string sanitizedQuery, string? gameTitle)
    {
        if (string.IsNullOrWhiteSpace(sanitizedQuery) || string.IsNullOrWhiteSpace(gameTitle))
        {
            return sanitizedQuery;
        }

        var titleTokens = SanitizeQuery(gameTitle)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (titleTokens.Count == 0)
        {
            return sanitizedQuery;
        }

        var kept = sanitizedQuery
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(token => !titleTokens.Contains(token));

        return string.Join(' ', kept);
    }

    private static string SanitizeQuery(string query)
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

    /// <inheritdoc />
    public Task<string> ResolveFtsConfigAsync(Guid gameId, string language = "en", CancellationToken cancellationToken = default)
        => ResolveGameFtsConfigAsync(gameId, language, cancellationToken);

    /// <summary>
    /// Titolo del gioco, usato solo per escluderne i token dalla tsquery (#3768).
    /// </summary>
    /// <remarks>
    /// Fallisce in silenzio restituendo null: senza titolo il comportamento torna a quello
    /// precedente, che è degradato ma non rotto. Far fallire una ricerca perché non si è potuto
    /// leggere un nome sarebbe sproporzionato.
    /// </remarks>
    private async Task<string?> ResolveGameTitleAsync(Guid gameId, CancellationToken cancellationToken)
    {
        var previousTimeout = _dbContext.Database.GetCommandTimeout();
        try
        {
            _dbContext.Database.SetCommandTimeout(3);
            return await _dbContext.Database
                .SqlQueryRaw<string>(
                    @"SELECT title AS ""Value"" FROM shared_games WHERE id = @gameId::uuid LIMIT 1",
                    new NpgsqlParameter("@gameId", gameId.ToString()))
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // il titolo è un'ottimizzazione del ranking, non un requisito
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogWarning(ex,
                "Keyword search: titolo del gioco {GameId} non risolto, la tsquery conserverà il nome del gioco (#3768)",
                gameId);
            return null;
        }
        finally
        {
            _dbContext.Database.SetCommandTimeout(previousTimeout);
        }
    }

    /// <summary>
    /// Detects the dominant document language for a game (from <c>pdf_documents.Language</c>,
    /// joined via the game's chunks) and resolves it to an FTS config. Keyword-retrieval callers
    /// do not thread a per-game language, so it is detected here rather than pinned to english.
    /// Falls back to <paramref name="requestedLanguage"/> (then english) when unknown/unavailable.
    /// </summary>
    private async Task<string> ResolveGameFtsConfigAsync(Guid gameId, string requestedLanguage, CancellationToken cancellationToken)
    {
        var previousTimeout = _dbContext.Database.GetCommandTimeout();
        try
        {
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

            return ResolveFtsConfig(string.IsNullOrWhiteSpace(dominant) ? requestedLanguage : dominant);
        }
#pragma warning disable CA1031 // Do not catch general exception types - language detection is best-effort
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Game FTS language detection failed for {GameId}; falling back to requested language", gameId);
            return ResolveFtsConfig(requestedLanguage);
        }
#pragma warning restore CA1031
        finally
        {
            // Restore even on exception (incl. the 3s timeout above) — the request-scoped
            // DbContext is reused by SearchAsync; leaking the 3s cap would throttle the request.
            _dbContext.Database.SetCommandTimeout(previousTimeout);
        }
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
    // text_chunks."Id"/"PdfDocumentId"/"GameId" are Postgres `uuid` columns. Npgsql refuses to read
    // a uuid field into a `string` (InvalidCastException), so these MUST be typed as Guid — projecting
    // them as string silently broke every keyword search that returned rows (uncovered by #3269's FTS
    // integration test, since only the pure helpers had unit coverage).
    public Guid Id { get; set; }
    public string Content { get; set; } = default!;
    public Guid PdfDocumentId { get; set; }
    public Guid GameId { get; set; }
    public int ChunkIndex { get; set; }
    public int? PageNumber { get; set; }
    public float RelevanceScore { get; set; }

    /// <summary>
    /// Phase D (D6): role_tags column from text_chunks (stored as int per <see cref="GameBookRole"/> bitflag).
    /// Defaults to 0 (None) when the chunk has not been classified.
    /// </summary>
    public int RoleTags { get; set; }

    /// <summary>#3270: heading-path label from text_chunks."Heading" (nullable).</summary>
    public string? Heading { get; set; }
}

/// <summary>
/// Raw result from PostgreSQL keyword search query on pdf_documents.
/// </summary>
internal class KeywordDocumentRawResult
{
    // pdf_documents."Id"/"GameId" are Postgres `uuid` columns — same Npgsql uuid→string cast trap as
    // KeywordSearchRawResult (they must be Guid, not string).
    public Guid Id { get; set; }
    public string FileName { get; set; } = default!;
    public Guid GameId { get; set; }
    public int? PageCount { get; set; }
    public float RelevanceScore { get; set; }
}