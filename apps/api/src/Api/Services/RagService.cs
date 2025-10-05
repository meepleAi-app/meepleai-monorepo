using Api.Infrastructure;
using Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.Services;

public class RagService
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IEmbeddingService _embeddingService;
    private readonly IQdrantService _qdrantService;
    private readonly ILlmService _llmService;
    private readonly IAiResponseCacheService _cache;
    private readonly ILogger<RagService> _logger;

    public RagService(
        MeepleAiDbContext dbContext,
        IEmbeddingService embeddingService,
        IQdrantService qdrantService,
        ILlmService llmService,
        IAiResponseCacheService cache,
        ILogger<RagService> logger)
    {
        _dbContext = dbContext;
        _embeddingService = embeddingService;
        _qdrantService = qdrantService;
        _llmService = llmService;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// AI-04: Answer question using RAG with LLM generation and anti-hallucination
    /// AI-05: Now with caching support for reduced latency
    /// </summary>
    public async Task<QaResponse> AskAsync(string gameId, string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new QaResponse("Please provide a question.", Array.Empty<Snippet>());
        }

        // AI-05: Check cache first
        var cacheKey = _cache.GenerateQaCacheKey(gameId, query);
        var cachedResponse = await _cache.GetAsync<QaResponse>(cacheKey, cancellationToken);
        if (cachedResponse != null)
        {
            LogInformation("Returning cached QA response for game {GameId}", gameId);
            return cachedResponse;
        }

        try
        {
            // Step 1: Generate embedding for the query
            var embeddingResult = await _embeddingService.GenerateEmbeddingAsync(query, cancellationToken);
            if (!embeddingResult.Success || embeddingResult.Embeddings.Count == 0)
            {
                _logger.LogError("Failed to generate query embedding: {Error}", embeddingResult.ErrorMessage);
                return new QaResponse("Unable to process query.", Array.Empty<Snippet>());
            }

            var queryEmbedding = embeddingResult.Embeddings[0];

            // Step 2: Search Qdrant for similar chunks
            var searchResult = await _qdrantService.SearchAsync(gameId, queryEmbedding, limit: 3, cancellationToken);

            if (!searchResult.Success || searchResult.Results.Count == 0)
            {
                LogInformation("No vector results found for query in game {GameId}", gameId);
                return new QaResponse("Not specified", Array.Empty<Snippet>());
            }

            // Step 3: Build snippets from results
            var snippets = searchResult.Results.Select(r => new Snippet(
                r.Text,
                $"PDF:{r.PdfId}",
                r.Page,
                0 // line number not tracked in chunks
            )).ToList();

            // Step 4: Build context from retrieved chunks
            var context = string.Join("\n\n---\n\n", searchResult.Results.Select(r =>
                $"[Page {r.Page}]\n{r.Text}"));

            // Step 5: Generate answer using LLM with anti-hallucination prompt
            var systemPrompt = @"You are a board game rules assistant. Your job is to answer questions about board game rules based ONLY on the provided context from the rulebook.

CRITICAL INSTRUCTIONS:
- If the answer to the question is clearly found in the provided context, answer it concisely and accurately.
- If the answer is NOT in the provided context or you're uncertain, respond with EXACTLY: ""Not specified""
- Do NOT make assumptions or use external knowledge about the game.
- Do NOT hallucinate or invent information.
- Keep your answers brief and to the point (2-3 sentences maximum).
- Reference page numbers when relevant.";

            var userPrompt = $@"CONTEXT FROM RULEBOOK:
{context}

QUESTION:
{query}

ANSWER:";

            var llmResult = await _llmService.GenerateCompletionAsync(systemPrompt, userPrompt, cancellationToken);

            if (!llmResult.Success || string.IsNullOrWhiteSpace(llmResult.Response))
            {
                _logger.LogError("Failed to generate LLM response: {Error}", llmResult.ErrorMessage);
                return new QaResponse("Unable to generate answer.", snippets);
            }

            var answer = llmResult.Response.Trim();

            LogInformation(
                "RAG query answered with {SnippetCount} snippets, LLM generated answer: {AnswerPreview}",
                snippets.Count, answer.Length > 50 ? answer.Substring(0, 50) + "..." : answer);

            var response = new QaResponse(answer, snippets);

            // AI-05: Cache the response for future requests
            await _cache.SetAsync(cacheKey, response, 86400, cancellationToken);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during RAG query for game {GameId}", gameId);
            return new QaResponse("An error occurred while processing your question.", Array.Empty<Snippet>());
        }
    }

    /// <summary>
    /// AI-02: Generate structured explanation with outline, script, and citations
    /// AI-05: Now with caching support for reduced latency
    /// </summary>
    public async Task<ExplainResponse> ExplainAsync(string gameId, string topic, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            return CreateEmptyExplainResponse("Please provide a topic to explain.");
        }

        // AI-05: Check cache first
        var cacheKey = _cache.GenerateExplainCacheKey(gameId, topic);
        var cachedResponse = await _cache.GetAsync<ExplainResponse>(cacheKey, cancellationToken);
        if (cachedResponse != null)
        {
            LogInformation("Returning cached Explain response for game {GameId}, topic: {Topic}", gameId, topic);
            return cachedResponse;
        }

        try
        {
            // Step 1: Generate embedding for the topic
            var embeddingResult = await _embeddingService.GenerateEmbeddingAsync(topic, cancellationToken);
            if (!embeddingResult.Success || embeddingResult.Embeddings.Count == 0)
            {
                _logger.LogError("Failed to generate topic embedding: {Error}", embeddingResult.ErrorMessage);
                return CreateEmptyExplainResponse("Unable to process topic.");
            }

            var topicEmbedding = embeddingResult.Embeddings[0];

            // Step 2: Search Qdrant for relevant chunks (get more for comprehensive explanation)
            var searchResult = await _qdrantService.SearchAsync(gameId, topicEmbedding, limit: 5, cancellationToken);

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
                0 // line number not tracked in chunks
            )).ToList();

            // Step 6: Calculate estimated reading time (average reading speed: 200 words/minute)
            var wordCount = script.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            var estimatedMinutes = Math.Max(1, (int)Math.Ceiling(wordCount / 200.0));

            LogInformation(
                "RAG explain generated for topic '{Topic}' with {SectionCount} sections, {CitationCount} citations, ~{Minutes} min read",
                topic, outline.sections.Count, citations.Count, estimatedMinutes);

            var response = new ExplainResponse(outline, script, citations, estimatedMinutes);

            // AI-05: Cache the response for future requests
            await _cache.SetAsync(cacheKey, response, 86400, cancellationToken);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during RAG explain for topic {Topic} in game {GameId}", topic, gameId);
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
}
