using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.Models;
using Api.Services;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for IndexChessKnowledgeCommand.
/// Indexes all chess knowledge from ChessKnowledge.json into Qdrant.
/// </summary>
public sealed class IndexChessKnowledgeCommandHandler
    : IRequestHandler<IndexChessKnowledgeCommand, Api.Services.ChessIndexResult>
{
    private readonly IQdrantService _qdrantService;
    private readonly IEmbeddingService _embeddingService;
    private readonly ITextChunkingService _chunkingService;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<IndexChessKnowledgeCommandHandler> _logger;

    private const string ChessCategory = "chess";

    public IndexChessKnowledgeCommandHandler(
        IQdrantService qdrantService,
        IEmbeddingService embeddingService,
        ITextChunkingService chunkingService,
        IWebHostEnvironment environment,
        ILogger<IndexChessKnowledgeCommandHandler> logger)
    {
        _qdrantService = qdrantService ?? throw new ArgumentNullException(nameof(qdrantService));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _chunkingService = chunkingService ?? throw new ArgumentNullException(nameof(chunkingService));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Api.Services.ChessIndexResult> Handle(
        IndexChessKnowledgeCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting chess knowledge indexing");

            // Load chess knowledge from JSON file
            var knowledgePath = Path.Combine(_environment.ContentRootPath, "Data", "ChessKnowledge.json");
            if (!File.Exists(knowledgePath))
            {
                _logger.LogError("Chess knowledge file not found at {Path}", knowledgePath);
                return Api.Services.ChessIndexResult.CreateFailure($"Knowledge file not found: {knowledgePath}");
            }

            var jsonContent = await File.ReadAllTextAsync(knowledgePath, cancellationToken).ConfigureAwait(false);
            var knowledge = JsonSerializer.Deserialize<ChessKnowledge>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (knowledge == null)
            {
                return Api.Services.ChessIndexResult.CreateFailure("Failed to deserialize chess knowledge");
            }

            // Collect all knowledge items
            var allItems = new List<ChessKnowledgeItem>();
            allItems.AddRange(knowledge.Rules ?? new List<ChessKnowledgeItem>());
            allItems.AddRange(knowledge.Openings ?? new List<ChessKnowledgeItem>());
            allItems.AddRange(knowledge.Tactics ?? new List<ChessKnowledgeItem>());
            allItems.AddRange(knowledge.MiddlegameStrategies ?? new List<ChessKnowledgeItem>());

            if (allItems.Count == 0)
            {
                return Api.Services.ChessIndexResult.CreateFailure("No knowledge items found");
            }

            _logger.LogInformation("Found {Count} chess knowledge items to index", allItems.Count);

            var categoryCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var totalChunks = 0;

            // Process each knowledge item
            foreach (var item in allItems)
            {
                var fullText = $"{item.Title}\n\n{item.Content}";

                // Chunk the text
                var chunkInputs = _chunkingService.PrepareForEmbedding(fullText, chunkSize: 512, overlap: 50);

                if (chunkInputs.Count == 0)
                {
                    _logger.LogWarning("No chunks generated for knowledge item: {Title}", item.Title);
                    continue;
                }

                // Generate embeddings for all chunks
                var texts = chunkInputs.Select(c => c.Text).ToList();
                var embeddingResult = await _embeddingService.GenerateEmbeddingsAsync(texts, cancellationToken)
                    .ConfigureAwait(false);

                if (!embeddingResult.Success)
                {
                    _logger.LogError(
                        "Failed to generate embeddings for {Title}: {Error}",
                        item.Title,
                        embeddingResult.ErrorMessage);
                    continue;
                }

                // Combine chunk inputs with embeddings
                var chunks = chunkInputs.Zip(embeddingResult.Embeddings, (input, embedding) => new DocumentChunk
                {
                    Text = input.Text,
                    Embedding = embedding,
                    Page = input.Page,
                    CharStart = input.CharStart,
                    CharEnd = input.CharEnd
                }).ToList();

                // Create metadata for this knowledge item
                var metadata = new Dictionary<string, string>
                {
                    ["category"] = ChessCategory,
                    ["subcategory"] = item.Category,
                    ["title"] = item.Title,
                    ["knowledge_type"] = DetermineKnowledgeType(item, knowledge)
                };

                // Index the chunks
                var indexResult = await _qdrantService.IndexChunksWithMetadataAsync(
                    metadata,
                    chunks,
                    cancellationToken).ConfigureAwait(false);

                if (!indexResult.Success)
                {
                    _logger.LogError(
                        "Failed to index chunks for {Title}: {Error}",
                        item.Title,
                        indexResult.ErrorMessage);
                    continue;
                }

                // Update statistics
                totalChunks += indexResult.IndexedCount;
                var subcategory = item.Category;
                categoryCounts[subcategory] = categoryCounts.GetValueOrDefault(subcategory, 0) + 1;

                _logger.LogInformation(
                    "Indexed {ChunkCount} chunks for {Title} ({Category})",
                    indexResult.IndexedCount,
                    item.Title,
                    item.Category);
            }

            _logger.LogInformation(
                "Chess knowledge indexing completed: {TotalItems} items, {TotalChunks} chunks",
                allItems.Count,
                totalChunks);

            return Api.Services.ChessIndexResult.CreateSuccess(allItems.Count, totalChunks, categoryCounts);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
#pragma warning restore CA1031
        {
            // ERROR STATE MANAGEMENT: Chess knowledge indexing failures return structured error result
            // Rationale: Indexing involves multiple external systems (file I/O, Qdrant, embedding API).
            // Returning a typed failure result allows callers to distinguish success/failure cases and
            // display appropriate error messages to administrators. Throwing would cause 500 errors
            // without context about which indexing stage failed.
            // Context: Indexing failures typically from Qdrant unavailable or file read errors
            _logger.LogError(ex, "Error during chess knowledge indexing");
            return Api.Services.ChessIndexResult.CreateFailure($"Indexing error: {ex.Message}");
        }
    }

    private static string DetermineKnowledgeType(ChessKnowledgeItem item, ChessKnowledge knowledge)
    {
        if (knowledge.Rules?.Contains(item) == true) return "rule";
        if (knowledge.Openings?.Contains(item) == true) return "opening";
        if (knowledge.Tactics?.Contains(item) == true) return "tactic";
        if (knowledge.MiddlegameStrategies?.Contains(item) == true) return "middlegame_strategy";
        return "unknown";
    }
}

/// <summary>
/// Chess knowledge data structure matching ChessKnowledge.json
/// </summary>
internal record ChessKnowledge
{
    public List<ChessKnowledgeItem>? Rules { get; init; }
    public List<ChessKnowledgeItem>? Openings { get; init; }
    public List<ChessKnowledgeItem>? Tactics { get; init; }
    public List<ChessKnowledgeItem>? MiddlegameStrategies { get; init; }
}

internal record ChessKnowledgeItem
{
    public string Category { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
}
