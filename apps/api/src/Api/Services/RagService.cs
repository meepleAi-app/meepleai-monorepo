using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Api.Helpers;
using Api.Infrastructure;
using Api.Models;
using Api.Observability;
using Api.Services.Rag;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Api.Services;

public class RagService : IRagService
{
    // CONFIG-04: Hardcoded defaults (lowest priority fallback)
    private const int DefaultTopK = 5;
    private const double DefaultMinScore = 0.55; // Adjusted for mxbai-embed-large model

    private readonly MeepleAiDbContext _dbContext;
    private readonly IEmbeddingService _embeddingService;
    private readonly IQdrantService _qdrantService;
    private readonly IHybridSearchService _hybridSearchService; // AI-14: Hybrid search service
    private readonly ILlmService _llmService;
    private readonly IAiResponseCacheService _cache;
    private readonly IPromptTemplateService _promptTemplateService;
    private readonly ILogger<RagService> _logger;

    // SOLID Refactoring Phase 3: Extracted specialized services
    private readonly IQueryExpansionService _queryExpansion;
    private readonly ISearchResultReranker _reranker;
    private readonly ICitationExtractorService _citationExtractor;
    private readonly IRagConfigurationProvider _configProvider; // Issue #1441: Centralized configuration provider

    public RagService(
        MeepleAiDbContext dbContext,
        IEmbeddingService embeddingService,
        IQdrantService qdrantService,
        IHybridSearchService hybridSearchService, // AI-14: Inject hybrid search service
        ILlmService llmService,
        IAiResponseCacheService cache,
        IPromptTemplateService promptTemplateService,
        ILogger<RagService> logger,
        IQueryExpansionService queryExpansion,
        ISearchResultReranker reranker,
        ICitationExtractorService citationExtractor,
        IRagConfigurationProvider configProvider) // Issue #1441: Centralized configuration provider
    {
        _dbContext = dbContext;
        _embeddingService = embeddingService;
        _qdrantService = qdrantService;
        _hybridSearchService = hybridSearchService; // AI-14
        _llmService = llmService;
        _cache = cache;
        _promptTemplateService = promptTemplateService;
        _logger = logger;
        _queryExpansion = queryExpansion;
        _reranker = reranker;
        _citationExtractor = citationExtractor;
        _configProvider = configProvider; // Issue #1441
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

        var queryError = QueryValidator.ValidateQuery(query);
        if (queryError != null)
        {
            return new QaResponse(queryError, Array.Empty<Snippet>());
        }

        // AI-05: Check cache first (unless bypassed)
        // AI-09: Include language in cache key to avoid cross-language cache hits
        var cacheKey = $"{_cache.GenerateQaCacheKey(gameId, query!)}:lang:{language}";
        if (!bypassCache)
        {
            var cachedResponse = await _cache.GetAsync<QaResponse>(cacheKey, cancellationToken).ConfigureAwait(false);
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

        // Issue #1441: Use centralized exception handling to eliminate 5 duplicate catch blocks
        return await ExecuteRagOperationAsync(
            async () =>
            {
                // CONFIG-04: Load dynamic RAG configuration
                var topK = await _configProvider.GetRagConfigAsync("TopK", DefaultTopK).ConfigureAwait(false);
                activity?.SetTag("rag.config.topK", topK);

                // Step 1: PERF-08 - Generate query variations for improved recall
                var queryVariations = await _queryExpansion.GenerateQueryVariationsAsync(query!, language, cancellationToken).ConfigureAwait(false);
                activity?.SetTag("query.variations.count", queryVariations.Count);

                _logger.LogDebug("Generated {Count} query variations: {Variations}",
                    queryVariations.Count, string.Join(", ", queryVariations.Take(3)));

                // Step 2: Generate embeddings for all query variations
                // AI-09: Use language-aware embedding service
                var embeddingTasks = queryVariations
                    .Select(q => _embeddingService.GenerateEmbeddingAsync(q, language, cancellationToken))
                    .ToList();
                var embeddingResults = await Task.WhenAll(embeddingTasks).ConfigureAwait(false);

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
                // CONFIG-04: Use dynamic topK from configuration
                var searchTasks = embeddings
                    .Select(embedding => _qdrantService.SearchAsync(gameId, embedding, language, limit: topK, cancellationToken))
                    .ToList();
                var searchResults = await Task.WhenAll(searchTasks).ConfigureAwait(false);

                // Step 4: PERF-08 - Fuse and deduplicate results using Reciprocal Rank Fusion (RRF)
                var fusedResults = await _reranker.FuseSearchResultsAsync(searchResults.Where(r => r.Success).ToList()).ConfigureAwait(false);

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
                var questionType = _promptTemplateService.ClassifyQuestion(query!);
                Guid? gameGuid = Guid.TryParse(gameId, out var guid) ? guid : null;
                var template = await _promptTemplateService.GetTemplateAsync(gameGuid, questionType).ConfigureAwait(false);

                var systemPrompt = _promptTemplateService.RenderSystemPrompt(template);
                var userPrompt = _promptTemplateService.RenderUserPrompt(template, context, query!);

                _logger.LogDebug(
                    "Using prompt template for game {GameId}, question type {QuestionType}",
                    gameId, questionType);

                var llmResult = await _llmService.GenerateCompletionAsync(systemPrompt, userPrompt, cancellationToken).ConfigureAwait(false);

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
                    snippets.Count, StringHelper.Truncate(answer, 50));

                var metadata = llmResult.Metadata.Count > 0
                    ? new Dictionary<string, string>(llmResult.Metadata, StringComparer.Ordinal)
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
                await _cache.SetAsync(cacheKey, response, 86400, cancellationToken).ConfigureAwait(false);

                // OPS-02: Record metrics
                stopwatch.Stop();
                MeepleAiMetrics.RecordRagRequest(stopwatch.Elapsed.TotalMilliseconds, gameId, success: true);
                MeepleAiMetrics.TokensUsed.Record(llmResult.Usage.TotalTokens, new System.Diagnostics.TagList { { "game.id", gameId }, { "operation", "qa" } });
                if (confidence.HasValue)
                {
                    MeepleAiMetrics.ConfidenceScore.Record(confidence.Value, new System.Diagnostics.TagList { { "game.id", gameId }, { "operation", "qa" } });
                }

                return response;
            },
            "RAG query",
            gameId,
            "qa",
            activity,
            stopwatch,
            GetQaErrorResponseFactories());
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

        if (string.IsNullOrWhiteSpace(topic))
        {
            return CreateEmptyExplainResponse("Please provide a topic to explain.");
        }

        // AI-05: Check cache first
        // AI-09: Include language in cache key
        var cacheKey = $"{_cache.GenerateExplainCacheKey(gameId, topic)}:lang:{language}";
        var cachedResponse = await _cache.GetAsync<ExplainResponse>(cacheKey, cancellationToken).ConfigureAwait(false);
        if (cachedResponse != null)
        {
            LogInformation("Returning cached Explain response for game {GameId}, topic: {Topic}, language: {Language}", gameId, topic, language);
            return cachedResponse;
        }

        // Issue #1441: Use centralized exception handling to eliminate 5 duplicate catch blocks
        return await ExecuteRagOperationAsync(
            async () =>
            {
                // CONFIG-04: Load dynamic RAG configuration
                var topK = await _configProvider.GetRagConfigAsync("TopK", DefaultTopK).ConfigureAwait(false);

                // Step 1: Generate embedding for the topic
                // AI-09: Use language-aware embedding service
                var embeddingResult = await _embeddingService.GenerateEmbeddingAsync(topic, language, cancellationToken).ConfigureAwait(false);
                if (!embeddingResult.Success || embeddingResult.Embeddings.Count == 0)
                {
                    _logger.LogError("Failed to generate topic embedding for language {Language}: {Error}", language, embeddingResult.ErrorMessage);
                    return CreateEmptyExplainResponse("Unable to process topic.");
                }

                var topicEmbedding = embeddingResult.Embeddings[0];

                // Step 2: Search Qdrant for relevant chunks (get more for comprehensive explanation)
                // AI-09: Filter search by language
                // CONFIG-04: Use dynamic topK from configuration
                var searchResult = await _qdrantService.SearchAsync(gameId, topicEmbedding, language, limit: topK, cancellationToken).ConfigureAwait(false);

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
                await _cache.SetAsync(cacheKey, response, 86400, cancellationToken).ConfigureAwait(false);

                // OPS-02: Record metrics
                stopwatch.Stop();
                MeepleAiMetrics.RecordRagRequest(stopwatch.Elapsed.TotalMilliseconds, gameId, success: true);
                if (confidence.HasValue)
                {
                    MeepleAiMetrics.ConfidenceScore.Record(confidence.Value, new System.Diagnostics.TagList { { "game.id", gameId }, { "operation", "explain" } });
                }

                return response;
            },
            "RAG explain",
            gameId,
            "explain",
            activity,
            stopwatch,
            GetExplainErrorResponseFactories(),
            $"topic: {topic}");
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
            sections.Add(StringHelper.Truncate(firstSentence, 60));
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

    /// <summary>
    /// Issue #1441: Centralized exception handling wrapper for RAG operations.
    /// Eliminates 20+ duplicate catch blocks across service methods.
    /// </summary>
    /// <typeparam name="TResponse">Response type (QaResponse, ExplainResponse, etc.)</typeparam>
    /// <param name="operationFunc">The RAG operation to execute</param>
    /// <param name="context">Context description for logging (e.g., "RAG query", "RAG explain")</param>
    /// <param name="gameId">Game ID for metrics and logging</param>
    /// <param name="operation">Operation name for metrics (e.g., "qa", "explain")</param>
    /// <param name="activity">OpenTelemetry activity for tracing</param>
    /// <param name="stopwatch">Stopwatch for measuring duration</param>
    /// <param name="errorResponseFactories">Map of exception types to error response factories</param>
    /// <param name="additionalInfo">Optional additional context for logging</param>
    /// <returns>Operation result or error response</returns>
    private async Task<TResponse> ExecuteRagOperationAsync<TResponse>(
        Func<Task<TResponse>> operationFunc,
        string context,
        string gameId,
        string operation,
        Activity? activity,
        Stopwatch stopwatch,
        Dictionary<string, Func<TResponse>> errorResponseFactories,
        string? additionalInfo = null)
    {
        try
        {
            return await operationFunc().ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: Service boundary - must return error response instead of throwing
        // This single catch block replaces 5+ duplicate catch blocks per method (Issue #1441)
        catch (Exception ex)
        {
            return RagExceptionHandler.HandleExceptionDispatch(
                ex,
                _logger,
                context,
                gameId,
                operation,
                activity,
                stopwatch,
                errorResponseFactories,
                additionalInfo);
        }
#pragma warning restore CA1031
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
    /// Issue #1441: Error response factories for QA operations.
    /// Centralizes error message creation to reduce duplication.
    /// </summary>
    private Dictionary<string, Func<QaResponse>> GetQaErrorResponseFactories()
    {
        return new Dictionary<string, Func<QaResponse>>(StringComparer.OrdinalIgnoreCase)
        {
            ["HttpRequestException"] = () => new QaResponse("Network error while processing your question.", Array.Empty<Snippet>()),
            ["TaskCanceledException"] = () => new QaResponse("Request timed out. Please try again.", Array.Empty<Snippet>()),
            ["InvalidOperationException"] = () => new QaResponse("Configuration error. Please contact support.", Array.Empty<Snippet>()),
            ["DbUpdateException"] = () => new QaResponse("Database error. Please try again.", Array.Empty<Snippet>()),
            ["Exception"] = () => new QaResponse("An error occurred while processing your question.", Array.Empty<Snippet>())
        };
    }

    /// <summary>
    /// Issue #1441: Error response factories for Explain operations.
    /// Centralizes error message creation to reduce duplication.
    /// </summary>
    private Dictionary<string, Func<ExplainResponse>> GetExplainErrorResponseFactories()
    {
        return new Dictionary<string, Func<ExplainResponse>>(StringComparer.OrdinalIgnoreCase)
        {
            ["HttpRequestException"] = () => CreateEmptyExplainResponse("Network error while generating explanation."),
            ["TaskCanceledException"] = () => CreateEmptyExplainResponse("Request timed out. Please try again."),
            ["InvalidOperationException"] = () => CreateEmptyExplainResponse("Configuration error. Please contact support."),
            ["DbUpdateException"] = () => CreateEmptyExplainResponse("Database error. Please try again."),
            ["Exception"] = () => CreateEmptyExplainResponse("An error occurred while generating the explanation.")
        };
    }

    /// <summary>
    /// AI-14: Answer question using hybrid search (vector + keyword) with configurable search mode.
    /// Combines Qdrant vector similarity with PostgreSQL full-text search using RRF fusion.
    /// AI-05: Includes caching support for reduced latency
    /// OPS-02: OpenTelemetry metrics tracking and distributed tracing
    /// AI-09: Multilingual support
    /// </summary>
    public async Task<QaResponse> AskWithHybridSearchAsync(
        string gameId,
        string query,
        SearchMode searchMode = SearchMode.Hybrid,
        string? language = null,
        bool bypassCache = false,
        CancellationToken cancellationToken = default)
    {
        // AI-09: Default to English if no language specified
        language ??= "en";

        // OPS-02: Create distributed trace span for hybrid search Q&A operation
        using var activity = MeepleAiActivitySources.Rag.StartActivity("RagService.AskWithHybridSearch");
        activity?.SetTag("game.id", gameId);
        activity?.SetTag("query.length", query?.Length ?? 0);
        activity?.SetTag("operation", "qa_hybrid");
        activity?.SetTag("search.mode", searchMode.ToString());
        activity?.SetTag("language", language);
        activity?.SetTag("cache.bypass", bypassCache);

        // OPS-02: Start tracking duration
        var stopwatch = Stopwatch.StartNew();

        var queryError = QueryValidator.ValidateQuery(query);
        if (queryError != null)
        {
            return new QaResponse(queryError, Array.Empty<Snippet>());
        }

        // AI-05: Check cache first (unless bypassed)
        // AI-09: Include language in cache key to avoid cross-language cache hits
        // AI-14: Include search mode in cache key to differentiate cached responses
        var cacheKey = $"{_cache.GenerateQaCacheKey(gameId, query!)}:lang:{language}:mode:{searchMode}";
        if (!bypassCache)
        {
            var cachedResponse = await _cache.GetAsync<QaResponse>(cacheKey, cancellationToken).ConfigureAwait(false);
            if (cachedResponse != null)
            {
                LogInformation("Returning cached hybrid search QA response for game {GameId}, mode {SearchMode}", gameId, searchMode);
                return cachedResponse;
            }
        }
        else
        {
            LogInformation("Cache bypassed for game {GameId} (Fresh Hybrid Answer requested)", gameId);
        }

        // Issue #1441: Use centralized exception handling to eliminate 5 duplicate catch blocks
        return await ExecuteRagOperationAsync(
            async () =>
            {
                // CONFIG-04: Load dynamic RAG configuration for top K
                var topK = await _configProvider.GetRagConfigAsync("TopK", DefaultTopK).ConfigureAwait(false);
                activity?.SetTag("rag.config.topK", topK);

                // Step 1: Parse gameId to Guid for hybrid search
                if (!Guid.TryParse(gameId, out var gameGuid))
                {
                    _logger.LogError("Invalid game ID format: {GameId}", gameId);
                    return new QaResponse("Invalid game ID format.", Array.Empty<Snippet>());
                }

                // Step 2: AI-14 - Use hybrid search service to retrieve results
                var hybridResults = await _hybridSearchService.SearchAsync(
                    query!,
                    gameGuid,
                    mode: searchMode,
                    limit: topK,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                if (hybridResults.Count == 0)
                {
                    LogInformation("No hybrid search results found for query in game {GameId}, mode {SearchMode}", gameId, searchMode);
                    return new QaResponse("Not specified", Array.Empty<Snippet>());
                }

                // Take top 3-5 results for context building (configurable via topK)
                var topResults = hybridResults.Take(Math.Min(5, topK)).ToList();
                activity?.SetTag("results.hybrid.count", hybridResults.Count);
                activity?.SetTag("results.final.count", topResults.Count);

                _logger.LogDebug("Hybrid search returned {Count} results, using top {TopCount} for context",
                    hybridResults.Count, topResults.Count);

                // Step 3: Convert HybridSearchResult to Snippet format
                var snippets = topResults.Select(r => new Snippet(
                    r.Content,
                    $"PDF:{r.PdfDocumentId}",
                    r.PageNumber ?? 0, // Use page number from hybrid result
                    0, // line number not tracked in chunks
                    r.HybridScore // AI-11: Use hybrid RRF score for quality tracking
                )).ToList();

                // Step 4: Build context from retrieved chunks
                var context = string.Join("\n\n---\n\n", topResults.Select(r =>
                    $"[Page {r.PageNumber ?? 0}]\n{r.Content}"));

                // AI-07.1: Use PromptTemplateService for advanced prompt engineering
                var questionType = _promptTemplateService.ClassifyQuestion(query!);
                Guid? gameGuidNullable = gameGuid;
                var template = await _promptTemplateService.GetTemplateAsync(gameGuidNullable, questionType).ConfigureAwait(false);

                var systemPrompt = _promptTemplateService.RenderSystemPrompt(template);
                var userPrompt = _promptTemplateService.RenderUserPrompt(template, context, query!);

                _logger.LogDebug(
                    "Using prompt template for game {GameId}, question type {QuestionType}, search mode {SearchMode}",
                    gameId, questionType, searchMode);

                // Step 5: Generate LLM response
                var llmResult = await _llmService.GenerateCompletionAsync(systemPrompt, userPrompt, cancellationToken).ConfigureAwait(false);

                if (!llmResult.Success || string.IsNullOrWhiteSpace(llmResult.Response))
                {
                    _logger.LogError("Failed to generate LLM response: {Error}", llmResult.ErrorMessage);
                    return new QaResponse("Unable to generate answer.", snippets);
                }

                var answer = llmResult.Response.Trim();
                var confidence = topResults.Count > 0
                    ? (double?)topResults.Max(r => r.HybridScore)
                    : null;

                // OPS-02: Add trace attributes for successful operation
                activity?.SetTag("response.tokens", llmResult.Usage.TotalTokens);
                activity?.SetTag("response.confidence", confidence ?? 0.0);
                activity?.SetTag("snippets.count", snippets.Count);
                activity?.SetTag("success", true);

                LogInformation(
                    "Hybrid search ({Mode}) QA answered with {SnippetCount} snippets, LLM generated answer: {AnswerPreview}",
                    searchMode, snippets.Count, StringHelper.Truncate(answer, 50));

                var metadata = llmResult.Metadata.Count > 0
                    ? new Dictionary<string, string>(llmResult.Metadata, StringComparer.Ordinal)
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
                await _cache.SetAsync(cacheKey, response, 86400, cancellationToken).ConfigureAwait(false);

                // OPS-02: Record metrics
                stopwatch.Stop();
                MeepleAiMetrics.RecordRagRequest(stopwatch.Elapsed.TotalMilliseconds, gameId, success: true);
                MeepleAiMetrics.TokensUsed.Record(llmResult.Usage.TotalTokens, new System.Diagnostics.TagList { { "game.id", gameId }, { "operation", "qa_hybrid" } });
                if (confidence.HasValue)
                {
                    MeepleAiMetrics.ConfidenceScore.Record(confidence.Value, new System.Diagnostics.TagList { { "game.id", gameId }, { "operation", "qa_hybrid" } });
                }

                return response;
            },
            "hybrid search RAG query",
            gameId,
            "qa_hybrid",
            activity,
            stopwatch,
            GetQaErrorResponseFactories(),
            $"mode: {searchMode}");
    }

    /// <summary>
    /// ADMIN-01 Phase 4: Answer a question using a custom system prompt for evaluation purposes.
    /// This method bypasses normal prompt retrieval and uses the provided custom prompt.
    /// Used exclusively by PromptEvaluationService to test different prompt versions.
    /// </summary>
    public async Task<QaResponse> AskWithCustomPromptAsync(
        string gameId,
        string query,
        string customSystemPrompt,
        SearchMode searchMode = SearchMode.Hybrid,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        // AI-09: Default to English if no language specified
        language ??= "en";

        // OPS-02: Create distributed trace span
        using var activity = MeepleAiActivitySources.Rag.StartActivity("RagService.AskWithCustomPrompt");
        activity?.SetTag("game.id", gameId);
        activity?.SetTag("query.length", query?.Length ?? 0);
        activity?.SetTag("operation", "qa_custom_prompt");
        activity?.SetTag("search.mode", searchMode.ToString());
        activity?.SetTag("language", language);

        var stopwatch = Stopwatch.StartNew();

        var queryError = QueryValidator.ValidateQuery(query);
        if (queryError != null)
        {
            return new QaResponse(queryError, Array.Empty<Snippet>());
        }

        if (string.IsNullOrWhiteSpace(customSystemPrompt))
        {
            return new QaResponse("Custom system prompt is required for evaluation.", Array.Empty<Snippet>());
        }

        // Issue #1441: Use centralized exception handling to eliminate 5 duplicate catch blocks
        return await ExecuteRagOperationAsync(
            async () =>
            {
                // CONFIG-04: Load dynamic RAG configuration for top K
                var topK = await _configProvider.GetRagConfigAsync("TopK", DefaultTopK).ConfigureAwait(false);
                activity?.SetTag("rag.config.topK", topK);

                // Step 1: Parse gameId to Guid for hybrid search
                if (!Guid.TryParse(gameId, out var gameGuid))
                {
                    _logger.LogError("Invalid game ID format: {GameId}", gameId);
                    return new QaResponse("Invalid game ID format.", Array.Empty<Snippet>());
                }

                // Step 2: Use hybrid search service to retrieve results
                var hybridResults = await _hybridSearchService.SearchAsync(
                    query!,
                    gameGuid,
                    mode: searchMode,
                    limit: topK,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                if (hybridResults.Count == 0)
                {
                    LogInformation("No hybrid search results found for query in game {GameId}", gameId);
                    return new QaResponse("Not specified", Array.Empty<Snippet>());
                }

                // Take top 3-5 results for context building
                var topResults = hybridResults.Take(Math.Min(5, topK)).ToList();
                activity?.SetTag("results.hybrid.count", hybridResults.Count);
                activity?.SetTag("results.final.count", topResults.Count);

                // Step 3: Convert HybridSearchResult to Snippet format
                var snippets = topResults.Select(r => new Snippet(
                    r.Content,
                    $"PDF:{r.PdfDocumentId}",
                    r.PageNumber ?? 0,
                    0,
                    r.HybridScore
                )).ToList();

                // Step 4: Build context from retrieved chunks
                var context = string.Join("\n\n---\n\n", topResults.Select(r =>
                    $"[Page {r.PageNumber ?? 0}]\n{r.Content}"));

                // Step 5: Build user prompt using template pattern
                // ADMIN-01: Use custom system prompt instead of retrieved prompt
                var userPrompt = $@"Context from rulebook:
{context}

Question: {query}

Instructions:
1. Answer based ONLY on the provided context
2. If the context doesn't contain enough information, say ""Not specified""
3. Always cite page numbers when possible
4. Be concise and accurate";

                _logger.LogDebug(
                    "Using custom system prompt for evaluation (length: {PromptLength} chars)",
                    customSystemPrompt.Length);

                // Step 6: Generate LLM response with custom prompt
                var llmResult = await _llmService.GenerateCompletionAsync(
                    customSystemPrompt,
                    userPrompt,
                    cancellationToken);

                if (!llmResult.Success || string.IsNullOrWhiteSpace(llmResult.Response))
                {
                    _logger.LogError("Failed to generate LLM response: {Error}", llmResult.ErrorMessage);
                    return new QaResponse("Unable to generate answer.", snippets);
                }

                var answer = llmResult.Response.Trim();
                var confidence = topResults.Count > 0
                    ? (double?)topResults.Max(r => r.HybridScore)
                    : null;

                // OPS-02: Add trace attributes
                activity?.SetTag("response.tokens", llmResult.Usage.TotalTokens);
                activity?.SetTag("response.confidence", confidence ?? 0.0);
                activity?.SetTag("snippets.count", snippets.Count);
                activity?.SetTag("success", true);

                LogInformation(
                    "Custom prompt QA answered with {SnippetCount} snippets, LLM generated answer: {AnswerPreview}",
                    snippets.Count, StringHelper.Truncate(answer, 50));

                var metadata = llmResult.Metadata.Count > 0
                    ? new Dictionary<string, string>(llmResult.Metadata, StringComparer.Ordinal)
                    : null;

                var response = new QaResponse(
                    answer,
                    snippets,
                    llmResult.Usage.PromptTokens,
                    llmResult.Usage.CompletionTokens,
                    llmResult.Usage.TotalTokens,
                    confidence,
                    metadata);

                // OPS-02: Record metrics
                stopwatch.Stop();
                MeepleAiMetrics.RecordRagRequest(stopwatch.Elapsed.TotalMilliseconds, gameId, success: true);
                MeepleAiMetrics.TokensUsed.Record(llmResult.Usage.TotalTokens, new System.Diagnostics.TagList { { "game.id", gameId }, { "operation", "qa_custom_prompt" } });
                if (confidence.HasValue)
                {
                    MeepleAiMetrics.ConfidenceScore.Record(confidence.Value, new System.Diagnostics.TagList { { "game.id", gameId }, { "operation", "qa_custom_prompt" } });
                }

                return response;
            },
            "custom prompt RAG query",
            gameId,
            "qa_custom_prompt",
            activity,
            stopwatch,
            GetQaErrorResponseFactories());
    }
}
