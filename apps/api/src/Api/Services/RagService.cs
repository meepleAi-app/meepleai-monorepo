using Api.Infrastructure;
using Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public class RagService
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly EmbeddingService _embeddingService;
    private readonly QdrantService _qdrantService;
    private readonly ILogger<RagService> _logger;

    public RagService(
        MeepleAiDbContext dbContext,
        EmbeddingService embeddingService,
        QdrantService qdrantService,
        ILogger<RagService> logger)
    {
        _dbContext = dbContext;
        _embeddingService = embeddingService;
        _qdrantService = qdrantService;
        _logger = logger;
    }

    /// <summary>
    /// AI-01: Answer question using vector similarity search
    /// </summary>
    public async Task<QaResponse> AskAsync(string tenantId, string gameId, string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new QaResponse("Please provide a question.", Array.Empty<Snippet>());
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
            var searchResult = await _qdrantService.SearchAsync(tenantId, gameId, queryEmbedding, limit: 3, cancellationToken);

            if (!searchResult.Success || searchResult.Results.Count == 0)
            {
                _logger.LogInformation("No vector results found for query in game {GameId}", gameId);
                return new QaResponse("No relevant information found in the rulebook.", Array.Empty<Snippet>());
            }

            // Step 3: Build response from top results
            var topResult = searchResult.Results[0];
            var snippets = searchResult.Results.Select(r => new Snippet(
                r.Text,
                $"PDF:{r.PdfId}",
                r.Page,
                0 // line number not tracked in chunks
            )).ToList();

            _logger.LogInformation(
                "RAG query answered with {SnippetCount} snippets, top score: {Score}",
                snippets.Count, topResult.Score);

            return new QaResponse(topResult.Text, snippets);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during RAG query for game {GameId}", gameId);
            return new QaResponse("An error occurred while processing your question.", Array.Empty<Snippet>());
        }
    }
}
