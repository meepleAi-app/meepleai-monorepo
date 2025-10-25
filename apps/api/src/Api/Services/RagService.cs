using Api.Infrastructure;
using Api.Models;
using Api.Observability;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.Services;

public class RagService : IRagService
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IEmbeddingService _embeddingService;
    private readonly IQdrantService _qdrantService;
    private readonly ILlmService _llmService;
    private readonly IAiResponseCacheService _cache;
    private readonly IPromptTemplateService _promptTemplateService;
    private readonly ILogger<RagService> _logger;

    public RagService(
        MeepleAiDbContext dbContext,
        IEmbeddingService embeddingService,
        IQdrantService qdrantService,
        ILlmService llmService,
        IAiResponseCacheService cache,
        IPromptTemplateService promptTemplateService,
        ILogger<RagService> logger)
    {
        _dbContext = dbContext;
        _embeddingService = embeddingService;
        _qdrantService = qdrantService;
        _llmService = llmService;
        _cache = cache;
        _promptTemplateService = promptTemplateService;
        _logger = logger;
    }

    /// <summary>
    /// AI-04: Answer question using RAG with LLM generation and anti-hallucination
    /// AI-05: Now with caching support for reduced latency
    /// PERF-03: Added bypassCache parameter to force fresh LLM responses
    /// OPS-02: Now with OpenTelemetry metrics tracking and distributed tracing
    /// AI-09: Added language parameter for multilingual support
    /// PERF-08: Query expansion for improved recall (15-25% better retrieval)
    /// </summary>
    public async Task<QaResponse> AskAsync(string gameId, string query, string? language = null, bool bypassCache = false, CancellationToken cancellationToken = default)
    {
        // AI-09: Default to English if no language specified
        language ??= "en";

        // OPS-02: Create distributed trace span for RAG Q&A operation
        using var activity = MeepleAiActivitySources.Rag.StartActivity("RagService.Ask");
        activity?.SetTag("game.id", gameId);
        activity?.SetTag("query.length", query?.Length ?? 0);
        activity?.SetTag("operation", "qa");
        activity?.SetTag("language", language); // AI-09: Track language
        activity?.SetTag("cache.bypass", bypassCache);

        // OPS-02: Start tracking duration
        var stopwatch = Stopwatch.StartNew();
        var success = false;

        if (string.IsNullOrWhiteSpace(query))
        {
            return new QaResponse("Please provide a question.", Array.Empty<Snippet>());
        }

        // AI-05: Check cache first (unless bypassed)
        // AI-09: Include language in cache key to avoid cross-language cache hits
        var cacheKey = $"{_cache.GenerateQaCacheKey(gameId, query)}:lang:{language}";
        if (!bypassCache)
        {
            var cachedResponse = await _cache.GetAsync<QaResponse>(cacheKey, cancellationToken);
            if (cachedResponse != null)
            {
                LogInformation("Returning cached QA response for game {GameId}", gameId);
                return cachedResponse;
            }
        }
        else
        {
            LogInformation("Cache bypassed for game {GameId} (Fresh Answer requested)", gameId);
        }

        try
        {
            // Step 1: PERF-08 - Generate query variations for improved recall
            var queryVariations = await GenerateQueryVariationsAsync(query, language, cancellationToken);
            activity?.SetTag("query.variations.count", queryVariations.Count);

            _logger.LogDebug("Generated {Count} query variations: {Variations}",
                queryVariations.Count, string.Join(", ", queryVariations.Take(3)));

            // Step 2: Generate embeddings for all query variations
            // AI-09: Use language-aware embedding service
            var embeddingTasks = queryVariations
                .Select(q => _embeddingService.GenerateEmbeddingAsync(q, language, cancellationToken))
                .ToList();
            var embeddingResults = await Task.WhenAll(embeddingTasks);

            var embeddings = embeddingResults
                .Where(r => r.Success && r.Embeddings.Count > 0)
                .Select(r => r.Embeddings[0])
                .ToList();

            if (embeddings.Count == 0)
            {
                _logger.LogError("Failed to generate query embeddings for language {Language}", language);
                return new QaResponse("Unable to process query.", Array.Empty<Snippet>());
            }

            // Step 3: PERF-08 - Search Qdrant with all query variations (parallel execution)
            var searchTasks = embeddings
                .Select(embedding => _qdrantService.SearchAsync(gameId, embedding, language, limit: 5, cancellationToken))
                .ToList();
            var searchResults = await Task.WhenAll(searchTasks);

            // Step 4: PERF-08 - Fuse and deduplicate results using Reciprocal Rank Fusion (RRF)
            var fusedResults = FuseSearchResults(searchResults.Where(r => r.Success).ToList());

            if (fusedResults.Count == 0)
            {
                LogInformation("No vector results found for query in game {GameId}", gameId);
                return new QaResponse("Not specified", Array.Empty<Snippet>());
            }

            // Take top 3 results after fusion
            var topResults = fusedResults.Take(3).ToList();
            activity?.SetTag("results.fused.count", fusedResults.Count);
            activity?.SetTag("results.final.count", topResults.Count);

            // Step 5: Build snippets from fused results
            var snippets = topResults.Select(r => new Snippet(
                r.Text,
                $"PDF:{r.PdfId}",
                r.Page,
                0, // line number not tracked in chunks
                r.Score // AI-11: Include actual search score for quality tracking
            )).ToList();

            // Step 6: Build context from retrieved chunks
            var context = string.Join("\n\n---\n\n", topResults.Select(r =>
                $"[Page {r.Page}]\n{r.Text}"));

            // AI-07.1: Use PromptTemplateService for advanced prompt engineering
            var questionType = _promptTemplateService.ClassifyQuestion(query);
            Guid? gameGuid = Guid.TryParse(gameId, out var guid) ? guid : null;
            var template = await _promptTemplateService.GetTemplateAsync(gameGuid, questionType);

            var systemPrompt = _promptTemplateService.RenderSystemPrompt(template);
            var userPrompt = _promptTemplateService.RenderUserPrompt(template, context, query);

            _logger.LogDebug(
                "Using prompt template for game {GameId}, question type {QuestionType}",
                gameId, questionType);

            var llmResult = await _llmService.GenerateCompletionAsync(systemPrompt, userPrompt, cancellationToken);

            if (!llmResult.Success || string.IsNullOrWhiteSpace(llmResult.Response))
            {
                _logger.LogError("Failed to generate LLM response: {Error}", llmResult.ErrorMessage);
                return new QaResponse("Unable to generate answer.", snippets);
            }

            var answer = llmResult.Response.Trim();
            var confidence = topResults.Count > 0
                ? (double?)topResults.Max(r => r.Score)
                : null;

            // OPS-02: Add trace attributes for successful operation
            activity?.SetTag("response.tokens", llmResult.Usage.TotalTokens);
            activity?.SetTag("response.confidence", confidence ?? 0.0);
            activity?.SetTag("snippets.count", snippets.Count);
            activity?.SetTag("success", true);

            LogInformation(
                "RAG query answered with {SnippetCount} snippets, LLM generated answer: {AnswerPreview}",
                snippets.Count, answer.Length > 50 ? answer.Substring(0, 50) + "..." : answer);

            var metadata = llmResult.Metadata.Count > 0
                ? new Dictionary<string, string>(llmResult.Metadata)
                : null;

            var response = new QaResponse(
                answer,
                snippets,
                llmResult.Usage.PromptTokens,
                llmResult.Usage.CompletionTokens,
                llmResult.Usage.TotalTokens,
                confidence,
                metadata);

            // AI-05: Cache the response for future requests
            await _cache.SetAsync(cacheKey, response, 86400, cancellationToken);

            // OPS-02: Record metrics
            success = true;
            stopwatch.Stop();
            MeepleAiMetrics.RecordRagRequest(stopwatch.Elapsed.TotalMilliseconds, gameId, success);
            MeepleAiMetrics.TokensUsed.Record(llmResult.Usage.TotalTokens, new System.Diagnostics.TagList { { "game.id", gameId }, { "operation", "qa" } });
            if (confidence.HasValue)
            {
                MeepleAiMetrics.ConfidenceScore.Record(confidence.Value, new System.Diagnostics.TagList { { "game.id", gameId }, { "operation", "qa" } });
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during RAG query for game {GameId}", gameId);

            // OPS-02: Record exception in trace span
            activity?.SetTag("success", false);
            activity?.SetTag("error.type", ex.GetType().Name);
            activity?.SetTag("error.message", ex.Message);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            // OPS-02: Record error metrics
            stopwatch.Stop();
            MeepleAiMetrics.RagErrorsTotal.Add(1, new System.Diagnostics.TagList { { "game.id", gameId }, { "operation", "qa" }, { "error.type", ex.GetType().Name } });
            MeepleAiMetrics.RecordRagRequest(stopwatch.Elapsed.TotalMilliseconds, gameId, success: false);

            return new QaResponse("An error occurred while processing your question.", Array.Empty<Snippet>());
        }
    }

    /// <summary>
    /// AI-02: Generate structured explanation with outline, script, and citations
    /// AI-05: Now with caching support for reduced latency
    /// OPS-02: Now with OpenTelemetry metrics tracking and distributed tracing
    /// AI-09: Added language parameter for multilingual support
    /// </summary>
    public async Task<ExplainResponse> ExplainAsync(string gameId, string topic, string? language = null, CancellationToken cancellationToken = default)
    {
        // AI-09: Default to English if no language specified
        language ??= "en";

        // OPS-02: Create distributed trace span for RAG Explain operation
        using var activity = MeepleAiActivitySources.Rag.StartActivity("RagService.Explain");
        activity?.SetTag("game.id", gameId);
        activity?.SetTag("topic.length", topic?.Length ?? 0);
        activity?.SetTag("operation", "explain");
        activity?.SetTag("language", language); // AI-09: Track language

        // OPS-02: Start tracking duration
        var stopwatch = Stopwatch.StartNew();
        var success = false;

        if (string.IsNullOrWhiteSpace(topic))
        {
            return CreateEmptyExplainResponse("Please provide a topic to explain.");
        }

        // AI-05: Check cache first
        // AI-09: Include language in cache key
        var cacheKey = $"{_cache.GenerateExplainCacheKey(gameId, topic)}:lang:{language}";
        var cachedResponse = await _cache.GetAsync<ExplainResponse>(cacheKey, cancellationToken);
        if (cachedResponse != null)
        {
            LogInformation("Returning cached Explain response for game {GameId}, topic: {Topic}, language: {Language}", gameId, topic, language);
            return cachedResponse;
        }

        try
        {
            // Step 1: Generate embedding for the topic
            // AI-09: Use language-aware embedding service
            var embeddingResult = await _embeddingService.GenerateEmbeddingAsync(topic, language, cancellationToken);
            if (!embeddingResult.Success || embeddingResult.Embeddings.Count == 0)
            {
                _logger.LogError("Failed to generate topic embedding for language {Language}: {Error}", language, embeddingResult.ErrorMessage);
                return CreateEmptyExplainResponse("Unable to process topic.");
            }

            var topicEmbedding = embeddingResult.Embeddings[0];

            // Step 2: Search Qdrant for relevant chunks (get more for comprehensive explanation)
            // AI-09: Filter search by language
            var searchResult = await _qdrantService.SearchAsync(gameId, topicEmbedding, language, limit: 5, cancellationToken);

            if (!searchResult.Success || searchResult.Results.Count == 0)
            {
                LogInformation("No vector results found for topic {Topic} in game {GameId}", topic, gameId);
                return CreateEmptyExplainResponse($"No relevant information found about '{topic}' in the rulebook.");
            }

            // Step 3: Build outline from retrieved chunks
            var outline = BuildOutline(topic, searchResult.Results);

            // Step 4: Build script from chunks with proper structure
            var script = BuildScript(topic, searchResult.Results);

            // Step 5: Create citations
            var citations = searchResult.Results.Select(r => new Snippet(
                r.Text,
                $"PDF:{r.PdfId}",
                r.Page,
                0, // line number not tracked in chunks
                r.Score // AI-11: Include actual search score for quality tracking
            )).ToList();

            // Step 6: Calculate estimated reading time (average reading speed: 200 words/minute)
            var wordCount = script.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            var estimatedMinutes = Math.Max(1, (int)Math.Ceiling(wordCount / 200.0));

            LogInformation(
                "RAG explain generated for topic '{Topic}' with {SectionCount} sections, {CitationCount} citations, ~{Minutes} min read",
                topic, outline.sections.Count, citations.Count, estimatedMinutes);

            var confidence = searchResult.Results.Count > 0
                ? (double?)searchResult.Results.Max(r => r.Score)
                : null;

            // OPS-02: Add trace attributes for successful operation
            activity?.SetTag("sections.count", outline.sections.Count);
            activity?.SetTag("citations.count", citations.Count);
            activity?.SetTag("estimated.minutes", estimatedMinutes);
            activity?.SetTag("response.confidence", confidence ?? 0.0);
            activity?.SetTag("success", true);

            var response = new ExplainResponse(
                outline,
                script,
                citations,
                estimatedMinutes,
                0,
                0,
                0,
                confidence);

            // AI-05: Cache the response for future requests
            await _cache.SetAsync(cacheKey, response, 86400, cancellationToken);

            // OPS-02: Record metrics
            success = true;
            stopwatch.Stop();
            MeepleAiMetrics.RecordRagRequest(stopwatch.Elapsed.TotalMilliseconds, gameId, success);
            if (confidence.HasValue)
            {
                MeepleAiMetrics.ConfidenceScore.Record(confidence.Value, new System.Diagnostics.TagList { { "game.id", gameId }, { "operation", "explain" } });
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during RAG explain for topic {Topic} in game {GameId}", topic, gameId);

            // OPS-02: Record exception in trace span
            activity?.SetTag("success", false);
            activity?.SetTag("error.type", ex.GetType().Name);
            activity?.SetTag("error.message", ex.Message);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            // OPS-02: Record error metrics
            stopwatch.Stop();
            MeepleAiMetrics.RagErrorsTotal.Add(1, new System.Diagnostics.TagList { { "game.id", gameId }, { "operation", "explain" }, { "error.type", ex.GetType().Name } });
            MeepleAiMetrics.RecordRagRequest(stopwatch.Elapsed.TotalMilliseconds, gameId, success: false);

            return CreateEmptyExplainResponse("An error occurred while generating the explanation.");
        }
    }

    private ExplainResponse CreateEmptyExplainResponse(string message)
    {
        return new ExplainResponse(
            new ExplainOutline("", new List<string>()),
            message,
            Array.Empty<Snippet>(),
            0
        );
    }

    private ExplainOutline BuildOutline(string topic, IReadOnlyList<SearchResultItem> results)
    {
        // Extract key sections from the retrieved chunks
        // For now, create sections based on the number of chunks retrieved
        var sections = new List<string>();

        for (int i = 0; i < results.Count && i < 5; i++)
        {
            var result = results[i];
            // Extract first sentence or first 50 chars as section title
            var text = result.Text.Trim();
            var firstSentence = text.Split('.')[0];
            if (firstSentence.Length > 60)
            {
                firstSentence = firstSentence.Substring(0, 57) + "...";
            }
            sections.Add(firstSentence);
        }

        return new ExplainOutline(topic, sections);
    }

    private void LogInformation(string message, params object?[] args)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(message, args);
        }
    }

    private string BuildScript(string topic, IReadOnlyList<SearchResultItem> results)
    {
        // Build a structured explanation script with citations
        var scriptParts = new List<string>();

        scriptParts.Add($"# Explanation: {topic}");
        scriptParts.Add("");
        scriptParts.Add("## Overview");
        scriptParts.Add("");

        // Combine the most relevant chunks into a coherent explanation
        for (int i = 0; i < results.Count; i++)
        {
            var result = results[i];
            scriptParts.Add($"### Section {i + 1} (Page {result.Page})");
            scriptParts.Add("");
            scriptParts.Add(result.Text.Trim());
            scriptParts.Add("");
        }

        return string.Join("\n", scriptParts);
    }

    /// <summary>
    /// PERF-08: Generate query variations for improved recall
    /// Uses rule-based expansion with synonyms and reformulations
    /// </summary>
    private async Task<List<string>> GenerateQueryVariationsAsync(string query, string language, CancellationToken cancellationToken)
    {
        var variations = new List<string> { query }; // Always include original query

        // Rule-based query expansion patterns for board games domain
        var expansionRules = new Dictionary<string, string[]>
        {
            // Setup-related synonyms
            { "setup", new[] { "initial setup", "game setup", "starting position", "prepare" } },
            { "prepare", new[] { "setup", "initial setup", "getting started" } },

            // Movement-related synonyms
            { "move", new[] { "movement", "moving", "how to move", "can move" } },
            { "movement", new[] { "move", "moving pieces", "piece movement" } },

            // Action-related synonyms
            { "play", new[] { "playing", "take action", "perform action" } },
            { "action", new[] { "move", "play", "turn action" } },

            // Turn-related synonyms
            { "turn", new[] { "player turn", "round", "phase" } },
            { "round", new[] { "turn", "game round", "playing round" } },

            // Win condition synonyms
            { "win", new[] { "winning", "victory", "how to win", "win condition" } },
            { "victory", new[] { "win", "winning condition", "game end" } },

            // Rule-related synonyms
            { "rule", new[] { "rules", "regulation", "how does" } },
            { "allowed", new[] { "can I", "is it legal", "permitted" } }
        };

        // Apply expansion rules (case-insensitive)
        var queryLower = query.ToLowerInvariant();
        foreach (var rule in expansionRules)
        {
            if (queryLower.Contains(rule.Key))
            {
                foreach (var synonym in rule.Value.Take(2)) // Limit to 2 synonyms per rule
                {
                    var expandedQuery = query.Replace(rule.Key, synonym, StringComparison.OrdinalIgnoreCase);
                    if (!variations.Contains(expandedQuery, StringComparer.OrdinalIgnoreCase))
                    {
                        variations.Add(expandedQuery);
                    }
                }
            }
        }

        // Add question reformulations for common patterns
        if (queryLower.StartsWith("how") || queryLower.StartsWith("what") || queryLower.StartsWith("can"))
        {
            // "How do I X?" → "X rules", "X instructions"
            var baseQuery = query.Replace("how do i ", "", StringComparison.OrdinalIgnoreCase)
                                 .Replace("how to ", "", StringComparison.OrdinalIgnoreCase)
                                 .Replace("what is ", "", StringComparison.OrdinalIgnoreCase)
                                 .Replace("can i ", "", StringComparison.OrdinalIgnoreCase)
                                 .TrimEnd('?').Trim();

            if (!string.IsNullOrWhiteSpace(baseQuery))
            {
                variations.Add($"{baseQuery} rules");
                variations.Add($"{baseQuery} instructions");
            }
        }

        // Limit total variations to avoid excessive API calls (original + 3 expansions)
        var finalVariations = variations.Distinct(StringComparer.OrdinalIgnoreCase).Take(4).ToList();

        _logger.LogDebug("Query expansion: '{Original}' → {Count} variations", query, finalVariations.Count);

        return await Task.FromResult(finalVariations);
    }

    /// <summary>
    /// PERF-08: Fuse search results using Reciprocal Rank Fusion (RRF)
    /// Combines results from multiple queries with deduplication
    /// </summary>
    private List<SearchResultItem> FuseSearchResults(List<SearchResult> searchResults)
    {
        const int k = 60; // RRF constant (common value from literature)

        // Dictionary to store RRF scores for each unique document
        var rrfScores = new Dictionary<string, (SearchResultItem item, double score)>();

        // Process each search result list
        for (int queryIndex = 0; queryIndex < searchResults.Count; queryIndex++)
        {
            var results = searchResults[queryIndex].Results;

            // Calculate RRF score for each result: 1 / (k + rank)
            for (int rank = 0; rank < results.Count; rank++)
            {
                var result = results[rank];
                var docKey = $"{result.PdfId}_{result.Page}_{result.Text.GetHashCode()}";

                var rrfScore = 1.0 / (k + rank + 1); // rank is 0-indexed, add 1 for proper formula

                if (rrfScores.ContainsKey(docKey))
                {
                    // Document appears in multiple result sets - accumulate RRF scores
                    var (existingItem, existingScore) = rrfScores[docKey];
                    rrfScores[docKey] = (existingItem, existingScore + rrfScore);
                }
                else
                {
                    // First time seeing this document
                    rrfScores[docKey] = (result, rrfScore);
                }
            }
        }

        // Sort by RRF score (descending) and return items
        var fusedResults = rrfScores.Values
            .OrderByDescending(x => x.score)
            .Select(x => new SearchResultItem
            {
                Text = x.item.Text,
                PdfId = x.item.PdfId,
                Page = x.item.Page,
                ChunkIndex = x.item.ChunkIndex,
                Score = (float)x.score // Use RRF score as the final relevance score
            })
            .ToList();

        _logger.LogDebug(
            "Result fusion: {InputLists} lists with {TotalResults} results → {FusedResults} unique results",
            searchResults.Count,
            searchResults.Sum(r => r.Results.Count),
            fusedResults.Count);

        return fusedResults;
    }
}
