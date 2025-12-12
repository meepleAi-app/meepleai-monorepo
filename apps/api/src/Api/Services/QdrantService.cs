using Api.Helpers;
using Api.Observability;
using Api.Services.Qdrant;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Qdrant.Client.Grpc;
using System.Diagnostics;

#pragma warning disable MA0048 // File name must match type name - Contains Service with Configuration classes
namespace Api.Services;
/// <summary>
/// Qdrant vector database adapter.
/// Classification: Tier 1 Infrastructure Adapter (ADR-017)
/// Critical: YES (19 references). Pure technical integration, zero business logic.
/// Facade pattern - delegates to specialized services.
/// </summary>
public class QdrantService : IQdrantService
{
    private readonly IQdrantCollectionManager _collectionManager;
    private readonly IQdrantVectorIndexer _vectorIndexer;
    private readonly IQdrantVectorSearcher _vectorSearcher;
    private readonly ILogger<QdrantService> _logger;
    private const string CollectionName = "meepleai_documents";
    // Vector size is determined by the embedding model configured in EmbeddingService
    private readonly uint _vectorSize;
    public QdrantService(
        IQdrantCollectionManager collectionManager,
        IQdrantVectorIndexer vectorIndexer,
        IQdrantVectorSearcher vectorSearcher,
        IEmbeddingService embeddingService,
        IConfiguration configuration,
        ILogger<QdrantService> logger)
    {
        _collectionManager = collectionManager;
        _vectorIndexer = vectorIndexer;
        _vectorSearcher = vectorSearcher;
        _logger = logger;

        // S1450: embeddingService used only locally for initialization
        _vectorSize = (uint)embeddingService.GetEmbeddingDimensions();

        var provider = configuration["EMBEDDING_PROVIDER"]?.ToLowerInvariant() ?? "ollama";
        var model = configuration["EMBEDDING_MODEL"] ?? "unknown";

        _logger.LogInformation("QdrantService initialized with vector size {VectorSize} for provider {Provider}, model {Model}",
            _vectorSize, provider, model);
    }
    /// <summary>
    /// Check if collection exists
    /// </summary>
    public async Task<bool> CollectionExistsAsync(CancellationToken ct = default)
    {
        return await _collectionManager.CollectionExistsAsync(CollectionName, ct).ConfigureAwait(false);
    }
    /// <summary>
    /// Initialize Qdrant collection if it doesn't exist
    /// </summary>
    public async Task EnsureCollectionExistsAsync(CancellationToken ct = default)
    {
        await _collectionManager.EnsureCollectionExistsAsync(CollectionName, _vectorSize, ct).ConfigureAwait(false);
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
        using var activity = MeepleAiActivitySources.VectorSearch.StartActivity("QdrantService.IndexDocumentChunks");
        activity?.SetTag("game.id", gameId);
        activity?.SetTag("pdf.id", pdfId);
        var originalChunkCount = chunks?.Count ?? 0;
        activity?.SetTag("chunks.original_count", originalChunkCount);
        activity?.SetTag("collection", CollectionName);
        if (chunks == null || originalChunkCount == 0)
        {
            activity?.SetTag("success", false);
            activity?.SetTag("error", "No chunks to index");
            return IndexResult.CreateFailure("No chunks to index");
        }
        var validChunks = new List<DocumentChunk>(chunks.Count);
        var invalidReasons = new List<string>();
        for (var i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            if (chunk == null)
            {
                invalidReasons.Add($"{i}: null chunk");
                continue;
            }
            if (string.IsNullOrWhiteSpace(chunk.Text))
            {
                invalidReasons.Add($"{i}: empty text");
                continue;
            }
            var embedding = chunk.Embedding;
            if (embedding == null || embedding.Length == 0)
            {
                invalidReasons.Add($"{i}: missing embedding");
                continue;
            }
            var hasInvalidValue = false;
            foreach (var value in embedding)
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    hasInvalidValue = true;
                    break;
                }
            }
            if (hasInvalidValue)
            {
                invalidReasons.Add($"{i}: non-finite embedding value");
                continue;
            }
            if (embedding.Length != _vectorSize)
            {
                invalidReasons.Add($"{i}: dimension {embedding.Length} expected {_vectorSize}");
                continue;
            }
            validChunks.Add(chunk);
        }
        if (invalidReasons.Count > 0)
        {
            _logger.LogWarning("Skipping {InvalidCount} invalid chunks for PDF {PdfId}: {Reasons}", invalidReasons.Count, pdfId, string.Join(", ", invalidReasons));
            activity?.SetTag("chunks.filtered", invalidReasons.Count);
        }
        if (validChunks.Count == 0)
        {
            activity?.SetTag("success", false);
            activity?.SetTag("error", "No valid chunks to index");
            return IndexResult.CreateFailure("No valid chunks to index");
        }
        activity?.SetTag("chunks.count", validChunks.Count);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            _logger.LogInformation("Indexing {Count} chunks for PDF {PdfId}", validChunks.Count, pdfId);
            var basePayload = new Dictionary<string, Value>(StringComparer.Ordinal)
            {
                ["game_id"] = gameId,
                ["pdf_id"] = pdfId
            };
            var points = _vectorIndexer.BuildPoints(validChunks, basePayload);
            await _vectorIndexer.UpsertPointsAsync(CollectionName, points.AsReadOnly(), ct).ConfigureAwait(false);
            _logger.LogInformation("Successfully indexed {Count} chunks for PDF {PdfId}", validChunks.Count, pdfId);
            activity?.SetTag("success", true);
            activity?.SetTag("indexed.count", validChunks.Count);
            stopwatch.Stop();
            MeepleAiMetrics.VectorIndexingDuration.Record(stopwatch.Elapsed.TotalMilliseconds, new TagList { { "collection", CollectionName } });
            return IndexResult.CreateSuccess(validChunks.Count);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: Infrastructure adapter - Wraps Qdrant gRPC exceptions into domain-friendly IndexResult with telemetry
        // Issue #1444: Use centralized exception handling for Result pattern
        catch (Exception ex)
        {
            return RagExceptionHandler.HandleServiceException(
                ex, _logger, $"indexing document chunks for PDF {pdfId}",
                errorMessage => IndexResult.CreateFailure(errorMessage),
                activity);
        }
#pragma warning restore CA1031
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
            ).ConfigureAwait(false);
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
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: Infrastructure adapter - Wraps Qdrant gRPC exceptions into domain-friendly SearchResult with telemetry
        // Issue #1444: Use centralized exception handling for Result pattern
        catch (Exception ex)
        {
            return RagExceptionHandler.HandleServiceException(
                ex, _logger, $"vector search for game {gameId}",
                errorMessage => SearchResult.CreateFailure(errorMessage),
                activity);
        }
#pragma warning restore CA1031
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
            await _vectorIndexer.DeleteByFilterAsync(CollectionName, filter, ct).ConfigureAwait(false);
            _logger.LogInformation("Successfully deleted vectors for PDF {PdfId}", pdfId);
            return true;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: Infrastructure adapter - Wraps Qdrant gRPC exceptions into boolean result pattern for deletion operations
        // Issue #1444: Use centralized exception handling for Result pattern (boolean return)
        catch (Exception ex)
        {
            return RagExceptionHandler.HandleServiceException(
                ex, _logger, $"deleting vectors for PDF {pdfId}",
                _ => false);
        }
#pragma warning restore CA1031
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
            var basePayload = new Dictionary<string, Value>(StringComparer.Ordinal);
            foreach (var kvp in metadata)
            {
                basePayload[kvp.Key] = kvp.Value;
            }
            // Build points from chunks
            var points = _vectorIndexer.BuildPoints(chunks, basePayload);
            // Upsert to Qdrant
            await _vectorIndexer.UpsertPointsAsync(CollectionName, points.AsReadOnly(), ct).ConfigureAwait(false);
            _logger.LogInformation("Successfully indexed {Count} chunks with metadata", chunks.Count);
            return IndexResult.CreateSuccess(chunks.Count);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: Infrastructure adapter - Wraps Qdrant gRPC exceptions into domain-friendly IndexResult with telemetry
        // Issue #1444: Use centralized exception handling for Result pattern
        catch (Exception ex)
        {
            return RagExceptionHandler.HandleServiceException(
                ex, _logger, "indexing chunks with metadata",
                errorMessage => IndexResult.CreateFailure(errorMessage));
        }
#pragma warning restore CA1031
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
            ).ConfigureAwait(false);
            var results = _vectorSearcher.ConvertToSearchResults(searchResults);
            _logger.LogInformation("Found {Count} results in category {Category}", results.Count, category);
            return SearchResult.CreateSuccess(results);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: Infrastructure adapter - Wraps Qdrant gRPC exceptions into domain-friendly SearchResult with telemetry
        // Issue #1444: Use centralized exception handling for Result pattern
        catch (Exception ex)
        {
            return RagExceptionHandler.HandleServiceException(
                ex, _logger, $"vector search for category {category}",
                errorMessage => SearchResult.CreateFailure(errorMessage));
        }
#pragma warning restore CA1031
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
(StringComparer.Ordinal)
            {
                ["game_id"] = gameId,
                ["pdf_id"] = pdfId,
                ["language"] = language
            };
            // Build points from chunks
            var points = _vectorIndexer.BuildPoints(chunks, basePayload);
            // Upsert to Qdrant
            await _vectorIndexer.UpsertPointsAsync(CollectionName, points.AsReadOnly(), ct).ConfigureAwait(false);
            _logger.LogInformation("Successfully indexed {Count} chunks for PDF {PdfId} with language {Language}",
                chunks.Count, pdfId, language);
            activity?.SetTag("success", true);
            activity?.SetTag("indexed.count", chunks.Count);
            stopwatch.Stop();
            MeepleAiMetrics.VectorIndexingDuration.Record(stopwatch.Elapsed.TotalMilliseconds,
                new TagList { { "collection", CollectionName }, { "language", language } });
            return IndexResult.CreateSuccess(chunks.Count);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: Infrastructure adapter - Wraps Qdrant gRPC exceptions into domain-friendly IndexResult with telemetry
        // Issue #1444: Use centralized exception handling for Result pattern
        catch (Exception ex)
        {
            return RagExceptionHandler.HandleServiceException(
                ex, _logger, $"indexing document chunks for PDF {pdfId} with language {language}",
                errorMessage => IndexResult.CreateFailure(errorMessage),
                activity);
        }
#pragma warning restore CA1031
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
            ).ConfigureAwait(false);
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
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: Infrastructure adapter - Wraps Qdrant gRPC exceptions into domain-friendly SearchResult with telemetry
        // Issue #1444: Use centralized exception handling for Result pattern
        catch (Exception ex)
        {
            return RagExceptionHandler.HandleServiceException(
                ex, _logger, $"vector search for game {gameId} and language {language}",
                errorMessage => SearchResult.CreateFailure(errorMessage),
                activity);
        }
#pragma warning restore CA1031
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
            await _vectorIndexer.DeleteByFilterAsync(CollectionName, filter, ct).ConfigureAwait(false);
            _logger.LogInformation("Successfully deleted vectors for category {Category}", category);
            return true;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: Infrastructure adapter - Wraps Qdrant gRPC exceptions into boolean result pattern for deletion operations
        // Issue #1444: Use centralized exception handling for Result pattern (boolean return)
        catch (Exception ex)
        {
            return RagExceptionHandler.HandleServiceException(
                ex, _logger, $"deleting vectors for category {category}",
                _ => false);
        }
#pragma warning restore CA1031
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

/// <summary>
/// Result of chess knowledge indexing operation.
/// </summary>
public record ChessIndexResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public int TotalKnowledgeItems { get; init; }
    public int TotalChunks { get; init; }
    public Dictionary<string, int> CategoryCounts { get; init; } = new(StringComparer.Ordinal);

    public static ChessIndexResult CreateSuccess(int totalItems, int totalChunks, Dictionary<string, int> categoryCounts) =>
        new() { Success = true, TotalKnowledgeItems = totalItems, TotalChunks = totalChunks, CategoryCounts = categoryCounts };

    public static ChessIndexResult CreateFailure(string error) =>
        new() { Success = false, ErrorMessage = error };
}




