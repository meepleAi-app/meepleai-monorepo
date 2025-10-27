using Api.Observability;
using Api.Services.Qdrant;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Qdrant.Client.Grpc;
using System.Diagnostics;

namespace Api.Services;

/// <summary>
/// Service for vector storage and retrieval using Qdrant
/// Facade pattern - delegates to specialized services
/// </summary>
public class QdrantService : IQdrantService
{
    private readonly IQdrantCollectionManager _collectionManager;
    private readonly IQdrantVectorIndexer _vectorIndexer;
    private readonly IQdrantVectorSearcher _vectorSearcher;
    private readonly ILogger<QdrantService> _logger;
    private const string CollectionName = "meepleai_documents";

    // Vector size depends on embedding provider:
    // - OpenAI text-embedding-3-small: 1536
    // - Ollama nomic-embed-text: 768
    private readonly uint _vectorSize;

    public QdrantService(
        IQdrantCollectionManager collectionManager,
        IQdrantVectorIndexer vectorIndexer,
        IQdrantVectorSearcher vectorSearcher,
        IConfiguration configuration,
        ILogger<QdrantService> logger)
    {
        _collectionManager = collectionManager;
        _vectorIndexer = vectorIndexer;
        _vectorSearcher = vectorSearcher;
        _logger = logger;

        // Determine vector size based on embedding provider
        var provider = configuration["EMBEDDING_PROVIDER"]?.ToLowerInvariant() ?? "ollama";
        _vectorSize = provider == "ollama" ? 768u : 1536u;

        _logger.LogInformation("QdrantService initialized with vector size {VectorSize} for provider {Provider}",
            _vectorSize, provider);
    }

    /// <summary>
    /// Check if collection exists
    /// </summary>
    public async Task<bool> CollectionExistsAsync(CancellationToken ct = default)
    {
        return await _collectionManager.CollectionExistsAsync(CollectionName, ct);
    }

    /// <summary>
    /// Initialize Qdrant collection if it doesn't exist
    /// </summary>
    public async Task EnsureCollectionExistsAsync(CancellationToken ct = default)
    {
        await _collectionManager.EnsureCollectionExistsAsync(CollectionName, _vectorSize, ct);
    }

    /// <summary>
    /// Index document chunks with embeddings
    /// OPS-02: Now with OpenTelemetry metrics tracking and distributed tracing
    /// </summary>
    public async Task<IndexResult> IndexDocumentChunksAsync(
        string gameId,
        string pdfId,
        List<DocumentChunk> chunks,
        CancellationToken ct = default)
    {
        // OPS-02: Create distributed trace span for vector indexing
        using var activity = MeepleAiActivitySources.VectorSearch.StartActivity("QdrantService.IndexDocumentChunks");
        activity?.SetTag("game.id", gameId);
        activity?.SetTag("pdf.id", pdfId);
        activity?.SetTag("chunks.count", chunks?.Count ?? 0);
        activity?.SetTag("collection", CollectionName);

        if (chunks == null || chunks.Count == 0)
        {
            activity?.SetTag("success", false);
            activity?.SetTag("error", "No chunks to index");
            return IndexResult.CreateFailure("No chunks to index");
        }

        // OPS-02: Start tracking duration
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Indexing {Count} chunks for PDF {PdfId}", chunks.Count, pdfId);

            // Build base payload
            var basePayload = new Dictionary<string, Value>
            {
                ["game_id"] = gameId,
                ["pdf_id"] = pdfId
            };

            // Build points from chunks
            var points = _vectorIndexer.BuildPoints(chunks, basePayload);

            // Upsert to Qdrant
            await _vectorIndexer.UpsertPointsAsync(CollectionName, points.AsReadOnly(), ct);

            _logger.LogInformation("Successfully indexed {Count} chunks for PDF {PdfId}", chunks.Count, pdfId);

            // OPS-02: Add trace attributes for successful operation
            activity?.SetTag("success", true);
            activity?.SetTag("indexed.count", chunks.Count);

            // OPS-02: Record metrics
            stopwatch.Stop();
            MeepleAiMetrics.VectorIndexingDuration.Record(stopwatch.Elapsed.TotalMilliseconds, new TagList { { "collection", CollectionName } });

            return IndexResult.CreateSuccess(chunks.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error indexing document chunks for PDF {PdfId}", pdfId);

            activity?.SetTag("success", false);
            activity?.SetTag("error.type", ex.GetType().Name);
            activity?.SetTag("error.message", ex.Message);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            return IndexResult.CreateFailure($"Indexing failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Search for similar chunks filtered by game identifier
    /// OPS-02: Now with OpenTelemetry metrics tracking and distributed tracing
    /// </summary>
    public virtual async Task<SearchResult> SearchAsync(
        string gameId,
        float[] queryEmbedding,
        int limit = 5,
        CancellationToken ct = default)
    {
        // Validate parameters
        ArgumentNullException.ThrowIfNull(queryEmbedding);

        // OPS-02: Create distributed trace span for vector search
        using var activity = MeepleAiActivitySources.VectorSearch.StartActivity("QdrantService.Search");
        activity?.SetTag("game.id", gameId);
        activity?.SetTag("limit", limit);
        activity?.SetTag("collection", CollectionName);
        activity?.SetTag("vector.dimension", queryEmbedding.Length);

        // OPS-02: Start tracking duration
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Searching in game {GameId}, limit {Limit}", gameId, limit);

            var filter = _vectorSearcher.BuildGameFilter(gameId);

            var searchResults = await _vectorSearcher.SearchAsync(
                collectionName: CollectionName,
                queryEmbedding: queryEmbedding,
                filter: filter,
                limit: limit,
                ct: ct
            );

            var results = _vectorSearcher.ConvertToSearchResults(searchResults);

            _logger.LogInformation("Found {Count} results", results.Count);

            // OPS-02: Add trace attributes for successful operation
            activity?.SetTag("results.count", results.Count);
            activity?.SetTag("success", true);
            if (results.Count > 0)
            {
                activity?.SetTag("top.score", results[0].Score);
            }

            // OPS-02: Record metrics
            stopwatch.Stop();
            MeepleAiMetrics.RecordVectorSearch(stopwatch.Elapsed.TotalMilliseconds, results.Count, CollectionName);

            return SearchResult.CreateSuccess(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during search for game {GameId}", gameId);

            activity?.SetTag("success", false);
            activity?.SetTag("error.type", ex.GetType().Name);
            activity?.SetTag("error.message", ex.Message);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            return SearchResult.CreateFailure($"Search failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Delete all vectors for a specific PDF
    /// </summary>
    public async Task<bool> DeleteDocumentAsync(string pdfId, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Deleting vectors for PDF {PdfId}", pdfId);

            var filter = _vectorSearcher.BuildPdfFilter(pdfId);
            await _vectorIndexer.DeleteByFilterAsync(CollectionName, filter, ct);

            _logger.LogInformation("Successfully deleted vectors for PDF {PdfId}", pdfId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting vectors for PDF {PdfId}", pdfId);
            return false;
        }
    }

    /// <summary>
    /// Index document chunks with custom metadata
    /// </summary>
    public async Task<IndexResult> IndexChunksWithMetadataAsync(
        Dictionary<string, string> metadata,
        List<DocumentChunk> chunks,
        CancellationToken ct = default)
    {
        if (chunks == null || chunks.Count == 0)
        {
            return IndexResult.CreateFailure("No chunks to index");
        }

        try
        {
            var category = metadata.GetValueOrDefault("category", "unknown");
            _logger.LogInformation("Indexing {Count} chunks with category {Category}", chunks.Count, category);

            // Convert metadata to Value dictionary
            var basePayload = new Dictionary<string, Value>();
            foreach (var kvp in metadata)
            {
                basePayload[kvp.Key] = kvp.Value;
            }

            // Build points from chunks
            var points = _vectorIndexer.BuildPoints(chunks, basePayload);

            // Upsert to Qdrant
            await _vectorIndexer.UpsertPointsAsync(CollectionName, points.AsReadOnly(), ct);

            _logger.LogInformation("Successfully indexed {Count} chunks with metadata", chunks.Count);
            return IndexResult.CreateSuccess(chunks.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error indexing chunks with metadata");
            return IndexResult.CreateFailure($"Indexing failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Search for similar chunks filtered by category
    /// </summary>
    public virtual async Task<SearchResult> SearchByCategoryAsync(
        string category,
        float[] queryEmbedding,
        int limit = 5,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Searching in category {Category}, limit {Limit}", category, limit);

            var filter = _vectorSearcher.BuildCategoryFilter(category);

            var searchResults = await _vectorSearcher.SearchAsync(
                collectionName: CollectionName,
                queryEmbedding: queryEmbedding,
                filter: filter,
                limit: limit,
                ct: ct
            );

            var results = _vectorSearcher.ConvertToSearchResults(searchResults);

            _logger.LogInformation("Found {Count} results in category {Category}", results.Count, category);
            return SearchResult.CreateSuccess(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during search for category {Category}", category);
            return SearchResult.CreateFailure($"Search failed: {ex.Message}");
        }
    }

    // AI-09: Multi-language support

    /// <summary>
    /// Index document chunks with embeddings and language metadata
    /// </summary>
    public async Task<IndexResult> IndexDocumentChunksAsync(
        string gameId,
        string pdfId,
        List<DocumentChunk> chunks,
        string language,
        CancellationToken ct = default)
    {
        using var activity = MeepleAiActivitySources.VectorSearch.StartActivity("QdrantService.IndexDocumentChunks");
        activity?.SetTag("game.id", gameId);
        activity?.SetTag("pdf.id", pdfId);
        activity?.SetTag("chunks.count", chunks?.Count ?? 0);
        activity?.SetTag("language", language);
        activity?.SetTag("collection", CollectionName);

        if (chunks == null || chunks.Count == 0)
        {
            activity?.SetTag("success", false);
            activity?.SetTag("error", "No chunks to index");
            return IndexResult.CreateFailure("No chunks to index");
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Indexing {Count} chunks for PDF {PdfId} with language {Language}",
                chunks.Count, pdfId, language);

            // Build base payload with language
            var basePayload = new Dictionary<string, Value>
            {
                ["game_id"] = gameId,
                ["pdf_id"] = pdfId,
                ["language"] = language
            };

            // Build points from chunks
            var points = _vectorIndexer.BuildPoints(chunks, basePayload);

            // Upsert to Qdrant
            await _vectorIndexer.UpsertPointsAsync(CollectionName, points.AsReadOnly(), ct);

            _logger.LogInformation("Successfully indexed {Count} chunks for PDF {PdfId} with language {Language}",
                chunks.Count, pdfId, language);

            activity?.SetTag("success", true);
            activity?.SetTag("indexed.count", chunks.Count);

            stopwatch.Stop();
            MeepleAiMetrics.VectorIndexingDuration.Record(stopwatch.Elapsed.TotalMilliseconds,
                new TagList { { "collection", CollectionName }, { "language", language } });

            return IndexResult.CreateSuccess(chunks.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error indexing document chunks for PDF {PdfId} with language {Language}",
                pdfId, language);

            activity?.SetTag("success", false);
            activity?.SetTag("error.type", ex.GetType().Name);
            activity?.SetTag("error.message", ex.Message);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            return IndexResult.CreateFailure($"Indexing failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Search for similar chunks filtered by game and language
    /// </summary>
    public virtual async Task<SearchResult> SearchAsync(
        string gameId,
        float[] queryEmbedding,
        string language,
        int limit = 5,
        CancellationToken ct = default)
    {
        // Validate parameters
        ArgumentNullException.ThrowIfNull(queryEmbedding);

        using var activity = MeepleAiActivitySources.VectorSearch.StartActivity("QdrantService.Search");
        activity?.SetTag("game.id", gameId);
        activity?.SetTag("language", language);
        activity?.SetTag("limit", limit);
        activity?.SetTag("collection", CollectionName);
        activity?.SetTag("vector.dimension", queryEmbedding.Length);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Searching in game {GameId} for language {Language}, limit {Limit}",
                gameId, language, limit);

            var filter = _vectorSearcher.BuildGameLanguageFilter(gameId, language);

            var searchResults = await _vectorSearcher.SearchAsync(
                collectionName: CollectionName,
                queryEmbedding: queryEmbedding,
                filter: filter,
                limit: limit,
                ct: ct
            );

            var results = _vectorSearcher.ConvertToSearchResults(searchResults);

            _logger.LogInformation("Found {Count} results for language {Language}", results.Count, language);

            activity?.SetTag("results.count", results.Count);
            activity?.SetTag("success", true);
            if (results.Count > 0)
            {
                activity?.SetTag("top.score", results[0].Score);
            }

            stopwatch.Stop();
            MeepleAiMetrics.RecordVectorSearch(stopwatch.Elapsed.TotalMilliseconds, results.Count, CollectionName);

            return SearchResult.CreateSuccess(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during search for game {GameId} and language {Language}", gameId, language);

            activity?.SetTag("success", false);
            activity?.SetTag("error.type", ex.GetType().Name);
            activity?.SetTag("error.message", ex.Message);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            return SearchResult.CreateFailure($"Search failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Delete all vectors for a specific category
    /// </summary>
    public async Task<bool> DeleteByCategoryAsync(string category, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Deleting vectors for category {Category}", category);

            var filter = _vectorSearcher.BuildCategoryFilter(category);
            await _vectorIndexer.DeleteByFilterAsync(CollectionName, filter, ct);

            _logger.LogInformation("Successfully deleted vectors for category {Category}", category);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting vectors for category {Category}", category);
            return false;
        }
    }
}

/// <summary>
/// Document chunk with embedding
/// </summary>
public record DocumentChunk
{
    public string Text { get; init; } = string.Empty;
    public float[] Embedding { get; init; } = Array.Empty<float>();
    public int Page { get; init; }
    public int CharStart { get; init; }
    public int CharEnd { get; init; }
}

/// <summary>
/// Result of indexing operation
/// </summary>
public record IndexResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public int IndexedCount { get; init; }

    public static IndexResult CreateSuccess(int count) =>
        new() { Success = true, IndexedCount = count };

    public static IndexResult CreateFailure(string error) =>
        new() { Success = false, ErrorMessage = error };
}

/// <summary>
/// Result of search operation
/// </summary>
public record SearchResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public List<SearchResultItem> Results { get; init; } = new();

    public static SearchResult CreateSuccess(List<SearchResultItem> results) =>
        new() { Success = true, Results = results };

    public static SearchResult CreateFailure(string error) =>
        new() { Success = false, ErrorMessage = error };
}

/// <summary>
/// Single search result item
/// </summary>
public record SearchResultItem
{
    public float Score { get; init; }
    public string Text { get; init; } = string.Empty;
    public string PdfId { get; init; } = string.Empty;
    public int Page { get; init; }
    public int ChunkIndex { get; init; }
}
