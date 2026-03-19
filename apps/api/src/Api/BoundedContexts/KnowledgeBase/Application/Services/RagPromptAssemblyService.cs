using System.Globalization;
using System.Text;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Enhancements;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Reranking;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Models;
using Api.Observability;
using Api.Services;
using Api.SharedKernel.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Orchestrates the full RAG prompt assembly pipeline.
/// Phase 0: embedding → Qdrant search → prompt assembly.
/// Phase 1: + reranking, query expansion, enhanced confidence scoring.
/// Phase 2: + chain-of-thought, sentence window, hybrid search (vector + FTS).
/// </summary>
internal sealed class RagPromptAssemblyService : IRagPromptAssemblyService
{
    private readonly IEmbeddingService _embeddingService;
    private readonly ICrossEncoderReranker _reranker;
    private readonly ILlmService _llmService;
    private readonly ITextChunkSearchService _textSearch;
    private readonly IExpansionGameResolver _expansionResolver;
    private readonly IRagEnhancementService _ragEnhancementService;
    private readonly IQueryComplexityClassifier _complexityClassifier;
    private readonly IRetrievalRelevanceEvaluator _relevanceEvaluator;
    private readonly IQueryExpander _queryExpander;
    private readonly IGraphRetrievalService _graphRetrievalService;
    private readonly ILogger<RagPromptAssemblyService> _logger;

    // RAG search parameters
    private const int RerankedTopK = 5;  // Final chunk count after reranking
    private const float DefaultMinScore = 0.55f;

    // Issue #5588: Expansion priority boost factor
    internal const float ExpansionBoostFactor = 1.3f;

    // Chat history thresholds
    private const int HistoryThreshold = 10;
    internal const int RecentMessageCount = 5;

    // Confidence scoring
    private const float HighScoreThreshold = 0.8f;
    private const float ConfidencePenalty = 0.1f;
    private static readonly string[] HedgeWords = [
        "forse", "probabilmente", "non sono sicuro", "potrebbe essere",
        "i'm not sure", "i think", "maybe", "perhaps", "possibly",
        "not certain", "it might", "it could"
    ];

    // Sentence window
    private const int SentenceWindowRadius = 1; // ±1 adjacent chunks

    // Hybrid search (RRF)
    private const int FtsTopK = 10; // Full-text search results to feed into RRF
    private const int RrfK = 60;    // Standard RRF constant

    // Query expansion
    private const string QueryExpansionSystemPrompt =
        "You are a query expansion assistant for board game documentation search. " +
        "Given a user question, generate 2-3 alternative phrasings that might match game rules documentation. " +
        "Return ONLY a JSON array of strings, no other text. Example: [\"alt1\", \"alt2\"]";

    public RagPromptAssemblyService(
        IEmbeddingService embeddingService,
        ICrossEncoderReranker reranker,
        ILlmService llmService,
        ITextChunkSearchService textSearch,
        IExpansionGameResolver expansionResolver,
        IRagEnhancementService ragEnhancementService,
        IQueryComplexityClassifier complexityClassifier,
        IRetrievalRelevanceEvaluator relevanceEvaluator,
        IQueryExpander queryExpander,
        IGraphRetrievalService graphRetrievalService,
        ILogger<RagPromptAssemblyService> logger)
    {
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _reranker = reranker ?? throw new ArgumentNullException(nameof(reranker));
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _textSearch = textSearch ?? throw new ArgumentNullException(nameof(textSearch));
        _expansionResolver = expansionResolver ?? throw new ArgumentNullException(nameof(expansionResolver));
        _ragEnhancementService = ragEnhancementService ?? throw new ArgumentNullException(nameof(ragEnhancementService));
        _complexityClassifier = complexityClassifier ?? throw new ArgumentNullException(nameof(complexityClassifier));
        _relevanceEvaluator = relevanceEvaluator ?? throw new ArgumentNullException(nameof(relevanceEvaluator));
        _queryExpander = queryExpander ?? throw new ArgumentNullException(nameof(queryExpander));
        _graphRetrievalService = graphRetrievalService ?? throw new ArgumentNullException(nameof(graphRetrievalService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AssembledPrompt> AssemblePromptAsync(
        string agentTypology,
        string gameTitle,
        GameState? gameState,
        string userQuestion,
        Guid gameId,
        ChatThread? chatThread,
        UserTier? userTier,
        CancellationToken ct,
        IRagDebugEventCollector? debugCollector = null)
    {
        ArgumentNullException.ThrowIfNull(agentTypology);
        ArgumentNullException.ThrowIfNull(gameTitle);
        ArgumentNullException.ThrowIfNull(userQuestion);

        // Step 0: Resolve expansion game IDs once (Issue #5588)
        var expansionGameIds = await _expansionResolver
            .GetExpansionGameIdsAsync(gameId, ct).ConfigureAwait(false);
        var hasExpansions = expansionGameIds.Count > 0;

        // Step 1: Retrieve RAG context (includes expansion boost), passing pre-resolved expansion IDs
        var (ragContext, citations) = await RetrieveRagContextAsync(userQuestion, gameId, expansionGameIds, userTier, ct, debugCollector).ConfigureAwait(false);

        // Step 2: Build system prompt (persona + RAG chunks + expansion priority)
        var systemPrompt = BuildSystemPrompt(agentTypology, gameTitle, gameState, ragContext, hasExpansions);

        // Step 3: Build user prompt (chat history + current question)
        var userPrompt = BuildUserPrompt(userQuestion, chatThread);

        // Step 4: Estimate tokens
        var estimatedTokens = EstimateTokens(systemPrompt) + EstimateTokens(userPrompt);

        // Context window usage debug event
        var modelContextLimit = 4096; // Default, could be made configurable
        var activeMessageCount = chatThread?.Messages.Count(m => !m.IsDeleted && !m.IsInvalidated);
        var historyCompressed = activeMessageCount > HistoryThreshold;
        debugCollector?.Add(StreamingEventType.DebugContextWindow,
            new DebugContextWindowData(
                SystemPromptTokens: EstimateTokens(systemPrompt),
                UserPromptTokens: EstimateTokens(userPrompt),
                TotalEstimatedTokens: estimatedTokens,
                ModelContextLimit: modelContextLimit,
                UsagePercentage: Math.Round((double)estimatedTokens / modelContextLimit * 100, 1),
                HistoryCompressed: historyCompressed,
                OriginalMessageCount: activeMessageCount,
                IncludedMessageCount: activeMessageCount != null
                    ? Math.Min(activeMessageCount.Value, HistoryThreshold) +
                      (historyCompressed ? RecentMessageCount : 0)
                    : null,
                CompressionReason: historyCompressed
                    ? $"History compressed: {activeMessageCount} messages → summary + last {RecentMessageCount}"
                    : null));

        _logger.LogInformation(
            "Assembled prompt: {AgentType} for {Game}, {ChunkCount} RAG chunks, {HistoryCount} history messages, ~{Tokens} tokens",
            agentTypology, gameTitle, citations.Count,
            activeMessageCount ?? 0,
            estimatedTokens);

        return new AssembledPrompt(systemPrompt, userPrompt, citations, estimatedTokens);
    }

    private async Task<(string ragContext, List<ChunkCitation> citations)> RetrieveRagContextAsync(
        string userQuestion, Guid gameId, IReadOnlyList<Guid> expansionGameIds, UserTier? userTier, CancellationToken ct,
        IRagDebugEventCollector? debugCollector = null)
    {
        var citations = new List<ChunkCitation>();

        try
        {
            // === ADAPTIVE RAG ===
            var activeEnhancements = RagEnhancement.None;
            if (userTier != null)
            {
                activeEnhancements = await _ragEnhancementService
                    .GetActiveEnhancementsAsync(userTier, ct).ConfigureAwait(false);
            }

            foreach (var flag in activeEnhancements.GetIndividualFlags())
            {
                MeepleAiMetrics.RagEnhancementActivations.Add(1,
                    new KeyValuePair<string, object?>("enhancement", flag.ToString()));
            }

            if (activeEnhancements.HasFlag(RagEnhancement.AdaptiveRouting))
            {
                var complexity = await _complexityClassifier.ClassifyAsync(userQuestion, ct).ConfigureAwait(false);
                _logger.LogInformation("Adaptive RAG: {Level} (confidence: {Confidence:F2})",
                    complexity.Level, complexity.Confidence);

                MeepleAiMetrics.RagAdaptiveRoutingDecisions.Add(1,
                    new KeyValuePair<string, object?>("level", complexity.Level.ToString()),
                    new KeyValuePair<string, object?>("skipped_retrieval", (!complexity.RequiresRetrieval).ToString()));

                debugCollector?.Add(StreamingEventType.DebugAdaptiveRouting,
                    new DebugAdaptiveRoutingData(
                        ComplexityLevel: complexity.Level.ToString(),
                        Confidence: complexity.Confidence,
                        Reason: complexity.Reason,
                        SkippedRetrieval: !complexity.RequiresRetrieval));

                if (!complexity.RequiresRetrieval)
                {
                    _logger.LogInformation("Adaptive RAG: skipping retrieval for simple query");
                    return (string.Empty, citations);
                }
            }

            // === RAG-FUSION or standard query expansion ===
            List<string> queries;
            if (activeEnhancements.HasFlag(RagEnhancement.RagFusionQueries))
            {
                var fusionStopwatch = System.Diagnostics.Stopwatch.StartNew();
                queries = await _queryExpander.ExpandAsync(userQuestion, ct).ConfigureAwait(false);
                fusionStopwatch.Stop();
                MeepleAiMetrics.RagFusionQueryCount.Record(queries.Count);
                _logger.LogInformation("RAG-Fusion: expanded to {Count} query variants", queries.Count);

                if (queries.Count > 1)
                {
                    debugCollector?.Add(StreamingEventType.DebugRagFusion,
                        new DebugRagFusionData(
                            QueryVariantCount: queries.Count,
                            Queries: queries,
                            DurationMs: fusionStopwatch.Elapsed.TotalMilliseconds));
                }
            }
            else
            {
                queries = await ExpandQueryAsync(userQuestion, ct).ConfigureAwait(false);
            }

            // Step 2: Generate embeddings for all queries
            var allChunks = new List<SearchResultItem>();

            // Vector search removed (Qdrant dependency removed).
            // Embeddings are still generated for potential future use.
            foreach (var query in queries)
            {
                var embeddingResult = await _embeddingService.GenerateEmbeddingAsync(query, ct).ConfigureAwait(false);
                if (!embeddingResult.Success || embeddingResult.Embeddings.Count == 0)
                {
                    if (string.Equals(query, userQuestion, StringComparison.Ordinal))
                    {
                        _logger.LogWarning("Embedding generation failed for primary query: {Error}", embeddingResult.ErrorMessage);
                        return (string.Empty, citations);
                    }
                }
            }

            // Step 2b: Hybrid search — PostgreSQL FTS + RRF fusion (graceful degradation)
            allChunks = await TryHybridSearchAsync(userQuestion, gameId, allChunks, ct).ConfigureAwait(false);

            // Deduplicate by PdfId+ChunkIndex, keep highest score
            var filteredChunks = allChunks
                .GroupBy(r => $"{r.PdfId}:{r.ChunkIndex}", StringComparer.Ordinal)
                .Select(g => g.OrderByDescending(r => r.Score).First())
                .Where(r => r.Score >= DefaultMinScore)
                .OrderByDescending(r => r.Score)
                .ToList();

            MeepleAiMetrics.RagRetrievalChunkCount.Record(filteredChunks.Count);
            if (filteredChunks.Count > 0)
            {
                MeepleAiMetrics.RagRetrievalAvgScore.Record(filteredChunks.Average(c => (double)c.Score));
            }

            if (filteredChunks.Count == 0)
            {
                _logger.LogInformation("No chunks above minScore {MinScore} for game {GameId}", DefaultMinScore, gameId);
                return (string.Empty, citations);
            }

            _logger.LogInformation("Retrieved {Count} relevant chunks from {QueryCount} queries (scores: {MinScore:F2}-{MaxScore:F2})",
                filteredChunks.Count, queries.Count,
                filteredChunks[^1].Score,
                filteredChunks[0].Score);

            // Step 3: Rerank if available (graceful degradation)
            filteredChunks = await TryRerankAsync(userQuestion, filteredChunks, ct).ConfigureAwait(false);

            // === CRAG: Evaluate retrieval relevance ===
            if (activeEnhancements.HasFlag(RagEnhancement.CragEvaluation) && filteredChunks.Count > 0)
            {
                var originalChunkCount = filteredChunks.Count;
                var scoredChunks = filteredChunks
                    .Select(c => new ScoredChunk(
                        $"{c.PdfId}:{c.ChunkIndex}", c.Text, c.Score))
                    .ToList();

                var evaluation = await _relevanceEvaluator
                    .EvaluateAsync(userQuestion, scoredChunks, ct).ConfigureAwait(false);

                MeepleAiMetrics.RagCragVerdicts.Add(1,
                    new KeyValuePair<string, object?>("verdict", evaluation.Verdict.ToString()));

                if (evaluation.ShouldRequery)
                {
                    _logger.LogInformation("CRAG: {Verdict} — expanding retrieval",
                        evaluation.Verdict);
                    var expandedQuery = await ExpandQueryAsync(userQuestion, ct).ConfigureAwait(false);
                    var expandedChunks = await TryHybridSearchAsync(
                        expandedQuery.FirstOrDefault() ?? userQuestion,
                        gameId, new List<SearchResultItem>(), ct).ConfigureAwait(false);

                    if (evaluation.UseRetrievedDocuments)
                    {
                        // Ambiguous: merge original + expanded, deduplicate
                        filteredChunks = filteredChunks
                            .Concat(expandedChunks)
                            .GroupBy(c => $"{c.PdfId}:{c.ChunkIndex}", StringComparer.Ordinal)
                            .Select(g => g.OrderByDescending(c => c.Score).First())
                            .OrderByDescending(c => c.Score)
                            .Take(RerankedTopK * 2)
                            .ToList();
                    }
                    else
                    {
                        // Incorrect: replace with expanded results
                        filteredChunks = expandedChunks
                            .Where(c => c.Score >= DefaultMinScore)
                            .OrderByDescending(c => c.Score)
                            .Take(RerankedTopK)
                            .ToList();
                    }

                    // Re-rerank the merged results
                    filteredChunks = await TryRerankAsync(userQuestion, filteredChunks, ct)
                        .ConfigureAwait(false);
                }

                debugCollector?.Add(StreamingEventType.DebugCragEvaluation,
                    new DebugCragEvaluationData(
                        Verdict: evaluation.Verdict.ToString(),
                        Confidence: evaluation.Confidence,
                        Reason: evaluation.Reason,
                        Requeried: evaluation.ShouldRequery,
                        OriginalChunkCount: originalChunkCount,
                        FinalChunkCount: filteredChunks.Count));
            }

            // === RAPTOR: Multi-granularity retrieval ===
            if (activeEnhancements.HasFlag(RagEnhancement.RaptorRetrieval))
            {
                var raptorChunks = await _textSearch.SearchRaptorSummariesAsync(
                    gameId, userQuestion, topK: 3, ct).ConfigureAwait(false);

                if (raptorChunks.Count > 0)
                {
                    foreach (var rc in raptorChunks)
                    {
                        filteredChunks.Add(new SearchResultItem
                        {
                            Score = rc.Rank * 1.1f, // Slight boost for RAPTOR summaries
                            Text = rc.Content,
                            PdfId = rc.PdfDocumentId.ToString(),
                            Page = rc.PageNumber ?? 0,
                            ChunkIndex = rc.ChunkIndex
                        });
                    }

                    filteredChunks = filteredChunks
                        .OrderByDescending(c => c.Score)
                        .Take(RerankedTopK + 2) // Allow slightly more chunks when RAPTOR active
                        .ToList();

                    _logger.LogInformation("RAPTOR: added {Count} summary chunks to context", raptorChunks.Count);
                }
            }

            // Step 4: Sentence window expansion — include adjacent chunks for more context
            filteredChunks = await TrySentenceWindowExpansionAsync(filteredChunks, ct).ConfigureAwait(false);

            // Format chunks and track citations
            var sb = new StringBuilder();
            foreach (var chunk in filteredChunks)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"[Source: Document {chunk.PdfId}, Page {chunk.Page}, Relevance: {chunk.Score:F2}]");
                sb.AppendLine(chunk.Text);
                sb.AppendLine("---");

                citations.Add(new ChunkCitation(
                    DocumentId: chunk.PdfId,
                    PageNumber: chunk.Page,
                    RelevanceScore: chunk.Score,
                    SnippetPreview: chunk.Text.Length > 120 ? string.Concat(chunk.Text.AsSpan(0, 117), "...") : chunk.Text));
            }

            var ragContext = sb.ToString().TrimEnd();

            // === Graph RAG: Inject entity context ===
            if (activeEnhancements.HasFlag(RagEnhancement.GraphTraversal))
            {
                var graphContext = await _graphRetrievalService
                    .GetEntityContextAsync(gameId, maxRelations: 15, ct).ConfigureAwait(false);

                if (!string.IsNullOrEmpty(graphContext))
                {
                    ragContext = string.Concat(ragContext, "\n", graphContext);
                    _logger.LogInformation("Graph RAG: injected {Length} chars of entity context for game {GameId}",
                        graphContext.Length, gameId);
                }
            }

            return (ragContext, citations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RAG retrieval failed for game {GameId}", gameId);
            return (string.Empty, citations);
        }
    }

    private async Task<List<string>> ExpandQueryAsync(string userQuestion, CancellationToken ct)
    {
        var queries = new List<string> { userQuestion }; // Always include original

        try
        {
            var result = await _llmService.GenerateCompletionAsync(
                QueryExpansionSystemPrompt,
                userQuestion,
                RequestSource.RagPipeline,
                ct).ConfigureAwait(false);

            if (result.Success && !string.IsNullOrWhiteSpace(result.Response))
            {
                var expansions = JsonSerializer.Deserialize<List<string>>(result.Response.Trim());
                if (expansions != null)
                {
                    queries.AddRange(expansions.Where(e => !string.IsNullOrWhiteSpace(e)).Take(3));
                    _logger.LogDebug("Query expanded: original + {ExpansionCount} alternatives", expansions.Count);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Query expansion failed, using original query only");
        }

        return queries;
    }

    private async Task<List<SearchResultItem>> TryRerankAsync(
        string userQuestion, List<SearchResultItem> chunks, CancellationToken ct)
    {
        if (chunks.Count <= RerankedTopK)
            return chunks.Take(RerankedTopK).ToList();

        try
        {
            var rerankChunks = chunks.Select(c => new RerankChunk(
                Id: $"{c.PdfId}:{c.ChunkIndex}",
                Content: c.Text,
                OriginalScore: c.Score
            )).ToList();

            var result = await _reranker.RerankAsync(userQuestion, rerankChunks, RerankedTopK, ct).ConfigureAwait(false);

            _logger.LogInformation("Reranked {Input} → {Output} chunks in {TimeMs:F1}ms",
                chunks.Count, result.Chunks.Count, result.ProcessingTimeMs);

            // Map reranked results back to SearchResultItems, updating scores
            var rerankedLookup = result.Chunks.ToDictionary(r => r.Id, StringComparer.Ordinal);
            return chunks
                .Where(c => rerankedLookup.ContainsKey($"{c.PdfId}:{c.ChunkIndex}"))
                .OrderByDescending(c => rerankedLookup[$"{c.PdfId}:{c.ChunkIndex}"].RerankScore)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Reranker failed, using raw Qdrant scores (top {TopK})", RerankedTopK);
            return chunks.Take(RerankedTopK).ToList();
        }
    }

    private async Task<List<SearchResultItem>> TryHybridSearchAsync(
        string userQuestion, Guid gameId, List<SearchResultItem> vectorChunks, CancellationToken ct)
    {
        try
        {
            var ftsResults = await _textSearch.FullTextSearchAsync(gameId, userQuestion, FtsTopK, ct).ConfigureAwait(false);
            if (ftsResults.Count == 0)
                return vectorChunks;

            // Convert FTS results to SearchResultItems for RRF fusion
            var ftsChunks = ftsResults.Select((r, index) => new SearchResultItem
            {
                Score = 0f, // Will be replaced by RRF score
                Text = r.Content,
                PdfId = r.PdfDocumentId.ToString(),
                Page = r.PageNumber ?? 0,
                ChunkIndex = r.ChunkIndex
            }).ToList();

            // Simple RRF fusion: combine vector and FTS rankings
            var fusedScores = new Dictionary<string, float>(StringComparer.Ordinal);

            for (int i = 0; i < vectorChunks.Count; i++)
            {
                var key = $"{vectorChunks[i].PdfId}:{vectorChunks[i].ChunkIndex}";
                fusedScores.TryAdd(key, 0f);
                fusedScores[key] += (float)(1.0 / (RrfK + i + 1));
            }

            for (int i = 0; i < ftsChunks.Count; i++)
            {
                var key = $"{ftsChunks[i].PdfId}:{ftsChunks[i].ChunkIndex}";
                fusedScores.TryAdd(key, 0f);
                fusedScores[key] += (float)(1.0 / (RrfK + i + 1));
            }

            // Merge all unique chunks, assign RRF score
            var allChunks = vectorChunks
                .Concat(ftsChunks)
                .GroupBy(c => $"{c.PdfId}:{c.ChunkIndex}", StringComparer.Ordinal)
                .Select(g =>
                {
                    var best = g.OrderByDescending(c => c.Score).First();
                    var key = $"{best.PdfId}:{best.ChunkIndex}";
                    return new SearchResultItem
                    {
                        // Use max of original score and normalized RRF score
                        Score = Math.Max(best.Score, NormalizeRrfScore(fusedScores[key])),
                        Text = best.Text,
                        PdfId = best.PdfId,
                        Page = best.Page,
                        ChunkIndex = best.ChunkIndex
                    };
                })
                .ToList();

            _logger.LogInformation("Hybrid search: {VectorCount} vector + {FtsCount} FTS → {FusedCount} fused chunks",
                vectorChunks.Count, ftsResults.Count, allChunks.Count);

            return allChunks;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Hybrid search failed, using vector-only results");
            return vectorChunks;
        }
    }

    private async Task<List<SearchResultItem>> TrySentenceWindowExpansionAsync(
        List<SearchResultItem> chunks, CancellationToken ct)
    {
        if (chunks.Count == 0)
            return chunks;

        try
        {
            var expandedChunks = new List<SearchResultItem>(chunks);
            var existingKeys = new HashSet<string>(
                chunks.Select(c => $"{c.PdfId}:{c.ChunkIndex}"),
                StringComparer.Ordinal);

            foreach (var chunk in chunks)
            {
                if (!Guid.TryParse(chunk.PdfId, out var pdfDocumentId))
                    continue;

                var adjacent = await _textSearch.GetAdjacentChunksAsync(
                    pdfDocumentId, chunk.ChunkIndex, SentenceWindowRadius, ct).ConfigureAwait(false);

                foreach (var adj in adjacent)
                {
                    var key = $"{adj.PdfDocumentId}:{adj.ChunkIndex}";
                    if (existingKeys.Add(key))
                    {
                        expandedChunks.Add(new SearchResultItem
                        {
                            Score = chunk.Score * 0.8f, // Adjacent chunks get 80% of parent score
                            Text = adj.Content,
                            PdfId = adj.PdfDocumentId.ToString(),
                            Page = adj.PageNumber ?? chunk.Page,
                            ChunkIndex = adj.ChunkIndex
                        });
                    }
                }
            }

            // Re-sort by PdfId then ChunkIndex for coherent reading order
            var sorted = expandedChunks
                .OrderByDescending(c => c.Score)
                .ThenBy(c => c.PdfId, StringComparer.Ordinal)
                .ThenBy(c => c.ChunkIndex)
                .ToList();

            if (sorted.Count > chunks.Count)
            {
                _logger.LogInformation("Sentence window: expanded {Original} → {Expanded} chunks",
                    chunks.Count, sorted.Count);
            }

            return sorted;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Sentence window expansion failed, using original chunks");
            return chunks;
        }
    }

    /// <summary>
    /// Normalizes RRF score to approximate 0-1 range.
    /// </summary>
    private static float NormalizeRrfScore(float rrfScore)
    {
        // RRF scores typically range from ~0.016 (1/(60+1)) to ~0.033 (2/(60+1))
        // Scale to 0-1 range; scores above ~0.025 indicate strong dual-source matches
        return Math.Min(rrfScore * 30f, 1.0f);
    }

    /// <summary>
    /// Applies expansion boost to a list of search results.
    /// Issue #5588: Expansion documents get 1.3x score multiplier because
    /// expansion rules override/modify base game rules and should have priority.
    /// This is a utility method for testing; the actual boost is applied inline
    /// during the search loop where we know which game collection each chunk came from.
    /// </summary>
    /// <param name="chunks">Chunks to boost.</param>
    /// <param name="boostFactor">Multiplicative boost factor (default: 1.3).</param>
    /// <returns>New list with boosted scores, re-sorted by descending score.</returns>
    internal static List<SearchResultItem> ApplyExpansionBoost(
        List<SearchResultItem> chunks,
        float boostFactor = ExpansionBoostFactor)
    {
        return chunks
            .Select(c => c with { Score = c.Score * boostFactor })
            .OrderByDescending(c => c.Score)
            .ToList();
    }

    /// <summary>
    /// Computes confidence score based on RAG chunk quality and response content.
    /// </summary>
    internal static double? ComputeConfidence(List<ChunkCitation> citations, string responseText)
    {
        if (citations.Count == 0)
            return null;

        // Base: average relevance score
        var confidence = (double)citations.Average(c => c.RelevanceScore);

        // Penalty: no chunks above high score threshold
        if (!citations.Any(c => c.RelevanceScore >= HighScoreThreshold))
            confidence -= ConfidencePenalty;

        // Penalty: hedge words in response
        var lowerResponse = responseText.ToLowerInvariant();
        if (HedgeWords.Any(hw => lowerResponse.Contains(hw, StringComparison.Ordinal)))
            confidence -= ConfidencePenalty;

        return Math.Max(0.0, Math.Min(1.0, confidence));
    }

    private static string BuildSystemPrompt(
        string agentTypology, string gameTitle, GameState? gameState, string ragContext,
        bool hasExpansions = false)
    {
        var sb = new StringBuilder();

        // Persona
        sb.AppendLine(CultureInfo.InvariantCulture, $"You are a {agentTypology} assistant for the board game \"{gameTitle}\".");
        sb.AppendLine("Answer questions accurately based on the game rules and documentation provided below.");
        sb.AppendLine("If the provided context does not contain enough information to answer, say so clearly.");
        sb.AppendLine("Always refer to specific rules when possible.");
        sb.AppendLine();

        // Issue #5588: Expansion priority instruction
        if (hasExpansions)
        {
            sb.AppendLine("## Expansion Priority");
            sb.AppendLine("Se una regola dell'espansione contraddice il gioco base, l'espansione prevale sempre.");
            sb.AppendLine("Quando una regola dell'espansione sovrascrive la base, segnalalo esplicitamente nella risposta.");
            sb.AppendLine();
        }

        // Issue #376: Rule dispute resolution instructions
        sb.AppendLine("## Rule Dispute Resolution");
        sb.AppendLine("When players disagree about a rule or ask you to settle a dispute:");
        sb.AppendLine("1. Search the provided documentation for the exact rule that applies.");
        sb.AppendLine("2. Quote the relevant rule text directly, including page/section if available.");
        sb.AppendLine("3. Give a clear verdict: state who is correct and why.");
        sb.AppendLine("4. If the rule is ambiguous or not explicitly covered, say so and suggest the most reasonable interpretation.");
        sb.AppendLine("5. Rate your confidence: HIGH (exact rule found), MEDIUM (inferred from related rules), LOW (no direct rule).");
        sb.AppendLine("Be fair and impartial — base your ruling only on the documented rules.");
        sb.AppendLine();

        // Chain-of-thought reasoning instructions
        sb.AppendLine("## Reasoning Approach");
        sb.AppendLine("Think step-by-step when answering:");
        sb.AppendLine("1. Identify the relevant rules from the provided context.");
        sb.AppendLine("2. Quote or reference the specific rule text that applies.");
        sb.AppendLine("3. Explain how the rule applies to the user's specific situation.");
        sb.AppendLine("4. State your conclusion clearly.");
        sb.AppendLine();

        // RAG context
        if (!string.IsNullOrWhiteSpace(ragContext))
        {
            sb.AppendLine("## Game Rules and Documentation");
            sb.AppendLine();
            sb.AppendLine(ragContext);
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("Note: No game documentation is currently available. Answer based on general knowledge and clearly indicate this limitation.");
            sb.AppendLine();
        }

        // Game state (if active session)
        if (gameState != null)
        {
            sb.AppendLine("## Current Game State");
            sb.AppendLine(CultureInfo.InvariantCulture, $"- Turn: {gameState.CurrentTurn}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"- Active player: {gameState.ActivePlayer}");

            if (gameState.PlayerScores.Count > 0)
            {
                var scores = string.Join(", ", gameState.PlayerScores.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
                sb.AppendLine(CultureInfo.InvariantCulture, $"- Scores: {scores}");
            }

            if (!string.IsNullOrWhiteSpace(gameState.GamePhase))
                sb.AppendLine(CultureInfo.InvariantCulture, $"- Phase: {gameState.GamePhase}");

            if (!string.IsNullOrWhiteSpace(gameState.LastAction))
                sb.AppendLine(CultureInfo.InvariantCulture, $"- Last action: {gameState.LastAction}");

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string BuildUserPrompt(string userQuestion, ChatThread? chatThread)
    {
        var sb = new StringBuilder();

        // Chat history
        if (chatThread != null)
        {
            var activeMessages = chatThread.Messages
                .Where(m => !m.IsDeleted && !m.IsInvalidated)
                .OrderBy(m => m.SequenceNumber)
                .ToList();

            if (activeMessages.Count > 0)
            {
                sb.AppendLine("## Conversation History");
                sb.AppendLine();

                if (activeMessages.Count <= HistoryThreshold)
                {
                    // Include all messages
                    foreach (var msg in activeMessages)
                    {
                        var role = msg.IsUserMessage ? "User" : "Assistant";
                        sb.AppendLine(CultureInfo.InvariantCulture, $"{role}: {msg.Content}");
                    }
                }
                else
                {
                    // Summary + recent messages
                    if (!string.IsNullOrWhiteSpace(chatThread.ConversationSummary))
                    {
                        sb.AppendLine("[Previous conversation summary]");
                        sb.AppendLine(chatThread.ConversationSummary);
                        sb.AppendLine();
                    }

                    sb.AppendLine("[Recent messages]");
                    var recentMessages = activeMessages.TakeLast(RecentMessageCount);
                    foreach (var msg in recentMessages)
                    {
                        var role = msg.IsUserMessage ? "User" : "Assistant";
                        sb.AppendLine(CultureInfo.InvariantCulture, $"{role}: {msg.Content}");
                    }
                }

                sb.AppendLine();
            }
        }

        // Current question
        sb.AppendLine("## Current Question");
        sb.AppendLine();
        sb.AppendLine(userQuestion);

        return sb.ToString();
    }

    private static int EstimateTokens(string text)
    {
        // Rough estimate: ~4 characters per token (GPT-style)
        return (int)Math.Ceiling(text.Length / 4.0);
    }
}
