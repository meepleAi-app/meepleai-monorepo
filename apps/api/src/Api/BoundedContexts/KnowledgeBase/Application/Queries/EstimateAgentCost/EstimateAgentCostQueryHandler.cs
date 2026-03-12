using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.EstimateAgentCost;

/// <summary>
/// Handler for EstimateAgentCostQuery.
/// Estimates token cost based on document chunks, model pricing, and retrieval strategy.
/// </summary>
internal sealed class EstimateAgentCostQueryHandler
    : IRequestHandler<EstimateAgentCostQuery, AgentCostEstimateDto>
{
    // Hardcoded pricing table (per token)
    // nomic-embed-text: $0.0001 / 1K tokens = $0.0000001 per token
    private const decimal EmbeddingPricePerToken = 0.0000001m;
    // gpt-4o-mini: $0.15 / 1M input tokens = $0.00000015 per token
    private const decimal CompletionInputPricePerToken = 0.00000015m;
    // gpt-4o-mini output: $0.60 / 1M output tokens = $0.0000006 per token
    private const decimal CompletionOutputPricePerToken = 0.0000006m;

    // Retrieval estimation constants
    private const int DefaultTopK = 5;
    private const int AvgChunkSizeTokens = 500;
    private const int PromptOverheadTokens = 200;
    private const int ResponseTokens = 500;

    private const string EmbeddingModel = "nomic-embed-text";
    private const string CompletionModel = "gpt-4o-mini";

    private readonly IVectorDocumentRepository _vectorDocumentRepository;
    private readonly ILogger<EstimateAgentCostQueryHandler> _logger;

    public EstimateAgentCostQueryHandler(
        IVectorDocumentRepository vectorDocumentRepository,
        ILogger<EstimateAgentCostQueryHandler> logger)
    {
        _vectorDocumentRepository = vectorDocumentRepository ?? throw new ArgumentNullException(nameof(vectorDocumentRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AgentCostEstimateDto> Handle(
        EstimateAgentCostQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Get all vector documents for the game
        var vectorDocuments = await _vectorDocumentRepository
            .GetByGameIdAsync(request.GameId, cancellationToken)
            .ConfigureAwait(false);

        // Filter to only requested document IDs (match by PdfDocumentId)
        var matchingDocuments = vectorDocuments
            .Where(vd => request.DocumentIds.Contains(vd.PdfDocumentId))
            .ToList();

        var totalChunks = matchingDocuments.Sum(vd => vd.TotalChunks);

        if (totalChunks == 0)
        {
            _logger.LogDebug(
                "No chunks found for GameId {GameId} with DocumentIds [{DocumentIds}]",
                request.GameId,
                string.Join(", ", request.DocumentIds));

            return new AgentCostEstimateDto(
                TotalChunks: 0,
                EstimatedEmbeddingTokens: 0,
                EstimatedCostPerQuery: 0m,
                Currency: "USD",
                Model: $"{EmbeddingModel} + {CompletionModel}",
                Note: "No indexed chunks found for the selected documents."
            );
        }

        // Estimate tokens per query:
        // - Embedding: query text (~50 tokens) for search
        // - Retrieved context: min(top-k, totalChunks) * avg chunk size
        // - Prompt overhead + response tokens
        var retrievedChunks = Math.Min(DefaultTopK, totalChunks);
        var embeddingTokens = 50; // query embedding tokens
        var contextTokens = retrievedChunks * AvgChunkSizeTokens;
        var inputTokens = contextTokens + PromptOverheadTokens;
        var totalEstimatedTokens = embeddingTokens + inputTokens + ResponseTokens;

        // Calculate cost
        var embeddingCost = embeddingTokens * EmbeddingPricePerToken;
        var completionInputCost = inputTokens * CompletionInputPricePerToken;
        var completionOutputCost = ResponseTokens * CompletionOutputPricePerToken;
        var totalCostPerQuery = embeddingCost + completionInputCost + completionOutputCost;

        // Include search cost for HybridSearch strategy (vector + keyword search)
        var isHybridSearch = string.Equals(request.StrategyName, "HybridSearch", StringComparison.OrdinalIgnoreCase);
        if (isHybridSearch)
        {
            // HybridSearch runs embedding twice (vector search + reranking)
            totalCostPerQuery += embeddingCost;
        }

        _logger.LogDebug(
            "Cost estimate for GameId {GameId}: {TotalChunks} chunks, {EstTokens} est. tokens, ${Cost:F8}/query ({Strategy})",
            request.GameId, totalChunks, totalEstimatedTokens, totalCostPerQuery, request.StrategyName);

        var note = isHybridSearch
            ? $"Estimate based on top-{retrievedChunks} retrieval from {totalChunks} chunks. HybridSearch includes vector search and reranking costs."
            : $"Estimate based on top-{retrievedChunks} retrieval from {totalChunks} chunks.";

        return new AgentCostEstimateDto(
            TotalChunks: totalChunks,
            EstimatedEmbeddingTokens: totalEstimatedTokens,
            EstimatedCostPerQuery: Math.Round(totalCostPerQuery, 8),
            Currency: "USD",
            Model: $"{EmbeddingModel} + {CompletionModel}",
            Note: note
        );
    }
}
