using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for ExplainQuery.
/// Generates structured explanation using RAG:
/// 1. Vector search for relevant context
/// 2. Build outline and script from retrieved chunks
/// 3. Create citations with page numbers
/// </summary>
internal class ExplainQueryHandler : IQueryHandler<ExplainQuery, ExplainResponseDto>
{
    private readonly SearchQueryHandler _searchQueryHandler;
    private readonly ILogger<ExplainQueryHandler> _logger;

    public ExplainQueryHandler(
        SearchQueryHandler searchQueryHandler,
        ILogger<ExplainQueryHandler> logger)
    {
        _searchQueryHandler = searchQueryHandler ?? throw new ArgumentNullException(nameof(searchQueryHandler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ExplainResponseDto> Handle(
        ExplainQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        _logger.LogInformation(
            "Processing ExplainQuery: GameId={GameId}, Topic={Topic}",
            query.GameId, query.Topic);

        // Step 1: Perform vector search for relevant chunks
        var searchQuery = new SearchQuery(
            GameId: query.GameId,
            Query: query.Topic,
            TopK: 10, // Get more results for comprehensive explanation
            MinScore: 0.50, // Lower threshold for broader coverage
            SearchMode: "hybrid",
            Language: query.Language
        );

        var searchResults = await _searchQueryHandler.Handle(searchQuery, cancellationToken)
            .ConfigureAwait(false);

        if (searchResults.Count == 0)
        {
            _logger.LogWarning(
                "No search results found for ExplainQuery: GameId={GameId}, Topic={Topic}",
                query.GameId, query.Topic);

            return CreateEmptyExplainResponse(
                $"No relevant information found about '{query.Topic}' in the rulebook.");
        }

        // Step 2: Build outline from search results
        var outline = BuildOutline(query.Topic, searchResults);

        // Step 3: Build script from chunks with proper structure
        var script = BuildScript(query.Topic, searchResults);

        // Step 4: Create citations from search results
        var citations = searchResults.Select(sr => new CitationDto(
            DocumentId: sr.VectorDocumentId,
            PageNumber: sr.PageNumber,
            Snippet: sr.TextContent.Length > 150
                ? sr.TextContent.Substring(0, 150)
                : sr.TextContent,
            RelevanceScore: sr.RelevanceScore
        )).ToList();

        // Step 5: Calculate estimated reading time (average reading speed: 200 words/minute)
        var wordCount = script.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        var estimatedSeconds = Math.Max(60, (int)Math.Ceiling(wordCount / 200.0 * 60));

        // Step 6: Calculate confidence from search results
        var confidence = searchResults.Count > 0
            ? searchResults.Max(sr => sr.RelevanceScore)
            : 0.0;

        _logger.LogInformation(
            "ExplainQuery completed: Topic={Topic}, Citations={CitationCount}, Confidence={Confidence:F3}, EstimatedSeconds={EstimatedSeconds}",
            query.Topic, citations.Count, confidence, estimatedSeconds);

        return new ExplainResponseDto(
            Outline: outline,
            Script: script,
            Citations: citations,
            Confidence: confidence,
            EstimatedReadingTimeSeconds: estimatedSeconds
        );
    }

    /// <summary>
    /// Creates an empty explanation response with a message.
    /// </summary>
    private static ExplainResponseDto CreateEmptyExplainResponse(string message)
    {
        return new ExplainResponseDto(
            Outline: string.Empty,
            Script: message,
            Citations: Array.Empty<CitationDto>(),
            Confidence: 0.0,
            EstimatedReadingTimeSeconds: 0
        );
    }

    /// <summary>
    /// Builds an outline from search results.
    /// Extracts key sections based on the most relevant chunks.
    /// </summary>
    private string BuildOutline(string topic, List<SearchResultDto> results)
    {
        var sections = new List<string> { topic };

        // Extract section titles from top 5 results
        for (int i = 0; i < results.Count && i < 5; i++)
        {
            var result = results[i];
            var text = result.TextContent.Trim();

            // Extract first sentence as section title
            var firstSentence = text.Split('.', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()?.Trim();

            if (!string.IsNullOrWhiteSpace(firstSentence) && firstSentence.Length <= 80)
            {
                sections.Add($"  • {firstSentence}");
            }
        }

        return string.Join("\n", sections);
    }

    /// <summary>
    /// Builds a structured script from search results.
    /// Creates a markdown-formatted explanation with citations.
    /// </summary>
    private string BuildScript(string topic, List<SearchResultDto> results)
    {
        var scriptParts = new List<string>
        {
            $"# Explanation: {topic}",
            "",
            "## Overview",
            ""
        };

        // Combine relevant chunks into a coherent explanation
        for (int i = 0; i < results.Count; i++)
        {
            var result = results[i];
            scriptParts.Add($"### Section {i + 1} (Page {result.PageNumber})");
            scriptParts.Add("");
            scriptParts.Add(result.TextContent.Trim());
            scriptParts.Add("");
        }

        return string.Join("\n", scriptParts);
    }
}
