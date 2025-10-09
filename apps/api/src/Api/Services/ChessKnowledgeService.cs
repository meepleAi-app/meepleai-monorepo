using System.Text.Json;

namespace Api.Services;

/// <summary>
/// CHESS-03: Service for indexing and managing chess knowledge in the vector database
/// </summary>
public class ChessKnowledgeService : IChessKnowledgeService
{
    private readonly IQdrantService _qdrantService;
    private readonly IEmbeddingService _embeddingService;
    private readonly ITextChunkingService _chunkingService;
    private readonly ILogger<ChessKnowledgeService> _logger;
    private readonly IWebHostEnvironment _environment;
    private const string ChessCategory = "chess";

    public ChessKnowledgeService(
        IQdrantService qdrantService,
        IEmbeddingService embeddingService,
        ITextChunkingService chunkingService,
        IWebHostEnvironment environment,
        ILogger<ChessKnowledgeService> logger)
    {
        _qdrantService = qdrantService;
        _embeddingService = embeddingService;
        _chunkingService = chunkingService;
        _environment = environment;
        _logger = logger;
    }

    /// <summary>
    /// Index all chess knowledge into Qdrant from the ChessKnowledge.json file
    /// </summary>
    public async Task<ChessIndexResult> IndexChessKnowledgeAsync(CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Starting chess knowledge indexing");

            // Load chess knowledge from JSON file
            var knowledgePath = Path.Combine(_environment.ContentRootPath, "Data", "ChessKnowledge.json");
            if (!File.Exists(knowledgePath))
            {
                _logger.LogError("Chess knowledge file not found at {Path}", knowledgePath);
                return ChessIndexResult.CreateFailure($"Knowledge file not found: {knowledgePath}");
            }

            var jsonContent = await File.ReadAllTextAsync(knowledgePath, ct);
            var knowledge = JsonSerializer.Deserialize<ChessKnowledge>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (knowledge == null)
            {
                return ChessIndexResult.CreateFailure("Failed to deserialize chess knowledge");
            }

            // Collect all knowledge items
            var allItems = new List<ChessKnowledgeItem>();
            allItems.AddRange(knowledge.Rules ?? new List<ChessKnowledgeItem>());
            allItems.AddRange(knowledge.Openings ?? new List<ChessKnowledgeItem>());
            allItems.AddRange(knowledge.Tactics ?? new List<ChessKnowledgeItem>());
            allItems.AddRange(knowledge.MiddlegameStrategies ?? new List<ChessKnowledgeItem>());

            if (allItems.Count == 0)
            {
                return ChessIndexResult.CreateFailure("No knowledge items found");
            }

            _logger.LogInformation("Found {Count} chess knowledge items to index", allItems.Count);

            var categoryCounts = new Dictionary<string, int>();
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
                var embeddingResult = await _embeddingService.GenerateEmbeddingsAsync(texts, ct);

                if (!embeddingResult.Success)
                {
                    _logger.LogError("Failed to generate embeddings for {Title}: {Error}", item.Title, embeddingResult.ErrorMessage);
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
                var indexResult = await _qdrantService.IndexChunksWithMetadataAsync(metadata, chunks, ct);

                if (!indexResult.Success)
                {
                    _logger.LogError("Failed to index chunks for {Title}: {Error}", item.Title, indexResult.ErrorMessage);
                    continue;
                }

                // Update statistics
                totalChunks += indexResult.IndexedCount;
                var subcategory = item.Category;
                categoryCounts[subcategory] = categoryCounts.GetValueOrDefault(subcategory, 0) + 1;

                _logger.LogInformation("Indexed {ChunkCount} chunks for {Title} ({Category})",
                    indexResult.IndexedCount, item.Title, item.Category);
            }

            _logger.LogInformation("Chess knowledge indexing completed: {TotalItems} items, {TotalChunks} chunks",
                allItems.Count, totalChunks);

            return ChessIndexResult.CreateSuccess(allItems.Count, totalChunks, categoryCounts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during chess knowledge indexing");
            return ChessIndexResult.CreateFailure($"Indexing error: {ex.Message}");
        }
    }

    /// <summary>
    /// Search chess knowledge with specified query
    /// </summary>
    public async Task<SearchResult> SearchChessKnowledgeAsync(string query, int limit = 5, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Searching chess knowledge: {Query}", query);

            // Generate embedding for query
            var embeddingResult = await _embeddingService.GenerateEmbeddingAsync(query, ct);
            if (!embeddingResult.Success || embeddingResult.Embeddings.Count == 0)
            {
                _logger.LogError("Failed to generate query embedding: {Error}", embeddingResult.ErrorMessage);
                return SearchResult.CreateFailure("Failed to generate query embedding");
            }

            // Search by chess category
            var searchResult = await _qdrantService.SearchByCategoryAsync(
                ChessCategory,
                embeddingResult.Embeddings[0],
                limit,
                ct);

            _logger.LogInformation("Chess knowledge search completed: {ResultCount} results", searchResult.Results.Count);
            return searchResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during chess knowledge search");
            return SearchResult.CreateFailure($"Search error: {ex.Message}");
        }
    }

    /// <summary>
    /// Delete all chess knowledge from the vector database
    /// </summary>
    public async Task<bool> DeleteChessKnowledgeAsync(CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Deleting all chess knowledge");
            var result = await _qdrantService.DeleteByCategoryAsync(ChessCategory, ct);
            _logger.LogInformation("Chess knowledge deletion completed: {Success}", result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during chess knowledge deletion");
            return false;
        }
    }

    private string DetermineKnowledgeType(ChessKnowledgeItem item, ChessKnowledge knowledge)
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
