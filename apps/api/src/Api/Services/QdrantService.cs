using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace Api.Services;

/// <summary>
/// Service for vector storage and retrieval using Qdrant
/// </summary>
public class QdrantService : IQdrantService
{
    private readonly IQdrantClientAdapter _clientAdapter;
    private readonly ILogger<QdrantService> _logger;
    private readonly IConfiguration _configuration;
    private const string CollectionName = "meepleai_documents";

    // Vector size depends on embedding provider:
    // - OpenAI text-embedding-3-small: 1536
    // - Ollama nomic-embed-text: 768
    private readonly uint _vectorSize;

    public QdrantService(
        IQdrantClientAdapter clientAdapter,
        IConfiguration configuration,
        ILogger<QdrantService> logger)
    {
        _clientAdapter = clientAdapter;
        _configuration = configuration;
        _logger = logger;

        // Determine vector size based on embedding provider
        var provider = _configuration["EMBEDDING_PROVIDER"]?.ToLowerInvariant() ?? "ollama";
        _vectorSize = provider == "ollama" ? 768u : 1536u;

        _logger.LogInformation("QdrantService initialized with vector size {VectorSize} for provider {Provider}",
            _vectorSize, provider);
    }

    /// <summary>
    /// Check if collection exists
    /// </summary>
    public async Task<bool> CollectionExistsAsync(CancellationToken ct = default)
    {
        try
        {
            var collectionsResponse = await _clientAdapter.ListCollectionsAsync(ct);
            return collectionsResponse.Any(c => c == CollectionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if collection exists");
            throw;
        }
    }

    /// <summary>
    /// Initialize Qdrant collection if it doesn't exist
    /// </summary>
    public async Task EnsureCollectionExistsAsync(CancellationToken ct = default)
    {
        try
        {
            var exists = await CollectionExistsAsync(ct);

            if (exists)
            {
                _logger.LogInformation("Collection {CollectionName} already exists", CollectionName);
                return;
            }

            _logger.LogInformation("Creating collection {CollectionName} with vector size {VectorSize}",
                CollectionName, _vectorSize);

            await _clientAdapter.CreateCollectionAsync(
                collectionName: CollectionName,
                vectorsConfig: new VectorParams
                {
                    Size = _vectorSize,
                    Distance = Distance.Cosine
                },
                cancellationToken: ct
            );

            // Create payload indexes for filtering
            await _clientAdapter.CreatePayloadIndexAsync(
                collectionName: CollectionName,
                fieldName: "game_id",
                schemaType: PayloadSchemaType.Keyword,
                cancellationToken: ct
            );

            await _clientAdapter.CreatePayloadIndexAsync(
                collectionName: CollectionName,
                fieldName: "pdf_id",
                schemaType: PayloadSchemaType.Keyword,
                cancellationToken: ct
            );

            await _clientAdapter.CreatePayloadIndexAsync(
                collectionName: CollectionName,
                fieldName: "category",
                schemaType: PayloadSchemaType.Keyword,
                cancellationToken: ct
            );

            _logger.LogInformation("Collection {CollectionName} created successfully with indexes", CollectionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure collection exists");
            throw;
        }
    }

    /// <summary>
    /// Index document chunks with embeddings
    /// </summary>
    public async Task<IndexResult> IndexDocumentChunksAsync(
        string gameId,
        string pdfId,
        List<DocumentChunk> chunks,
        CancellationToken ct = default)
    {
        if (chunks == null || chunks.Count == 0)
        {
            return IndexResult.CreateFailure("No chunks to index");
        }

        try
        {
            _logger.LogInformation("Indexing {Count} chunks for PDF {PdfId}", chunks.Count, pdfId);

            var points = new List<PointStruct>();

            for (int i = 0; i < chunks.Count; i++)
            {
                var chunk = chunks[i];
                var pointId = Guid.NewGuid().ToString();

                var payload = new Dictionary<string, Value>
                {
                    ["game_id"] = gameId,
                    ["pdf_id"] = pdfId,
                    ["chunk_index"] = i,
                    ["text"] = chunk.Text,
                    ["page"] = chunk.Page,
                    ["char_start"] = chunk.CharStart,
                    ["char_end"] = chunk.CharEnd,
                    ["indexed_at"] = DateTime.UtcNow.ToString("o")
                };

                var point = new PointStruct
                {
                    Id = new PointId { Uuid = pointId },
                    Vectors = chunk.Embedding,
                    Payload = { payload }
                };

                points.Add(point);
            }

            await _clientAdapter.UpsertAsync(
                collectionName: CollectionName,
                points: points.AsReadOnly(),
                cancellationToken: ct
            );

            _logger.LogInformation("Successfully indexed {Count} chunks for PDF {PdfId}", chunks.Count, pdfId);
            return IndexResult.CreateSuccess(chunks.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index document chunks for PDF {PdfId}", pdfId);
            return IndexResult.CreateFailure($"Indexing failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Search for similar chunks filtered by game identifier
    /// </summary>
    public virtual async Task<SearchResult> SearchAsync(
        string gameId,
        float[] queryEmbedding,
        int limit = 5,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Searching in game {GameId}, limit {Limit}", gameId, limit);

            var filter = new Filter
            {
                Must =
                {
                    new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "game_id",
                            Match = new Match { Keyword = gameId }
                        }
                    }
                }
            };

            var searchResults = await _clientAdapter.SearchAsync(
                collectionName: CollectionName,
                vector: queryEmbedding,
                filter: filter,
                limit: (ulong)limit,
                cancellationToken: ct
            );

            var results = searchResults.Select(r => new SearchResultItem
            {
                Score = r.Score,
                Text = r.Payload["text"].StringValue,
                PdfId = r.Payload["pdf_id"].StringValue,
                Page = (int)r.Payload["page"].IntegerValue,
                ChunkIndex = (int)r.Payload["chunk_index"].IntegerValue
            }).ToList();

            _logger.LogInformation("Found {Count} results", results.Count);
            return SearchResult.CreateSuccess(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Search failed for game {GameId}", gameId);
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

            var filter = new Filter
            {
                Must =
                {
                    new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "pdf_id",
                            Match = new Match { Keyword = pdfId }
                        }
                    }
                }
            };

            await _clientAdapter.DeleteAsync(
                collectionName: CollectionName,
                filter: filter,
                cancellationToken: ct
            );

            _logger.LogInformation("Successfully deleted vectors for PDF {PdfId}", pdfId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete vectors for PDF {PdfId}", pdfId);
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

            var points = new List<PointStruct>();

            for (int i = 0; i < chunks.Count; i++)
            {
                var chunk = chunks[i];
                var pointId = Guid.NewGuid().ToString();

                var payload = new Dictionary<string, Value>();

                // Add all provided metadata
                foreach (var kvp in metadata)
                {
                    payload[kvp.Key] = kvp.Value;
                }

                // Add chunk-specific data
                payload["chunk_index"] = i;
                payload["text"] = chunk.Text;
                payload["page"] = chunk.Page;
                payload["char_start"] = chunk.CharStart;
                payload["char_end"] = chunk.CharEnd;
                payload["indexed_at"] = DateTime.UtcNow.ToString("o");

                var point = new PointStruct
                {
                    Id = new PointId { Uuid = pointId },
                    Vectors = chunk.Embedding,
                    Payload = { payload }
                };

                points.Add(point);
            }

            await _clientAdapter.UpsertAsync(
                collectionName: CollectionName,
                points: points.AsReadOnly(),
                cancellationToken: ct
            );

            _logger.LogInformation("Successfully indexed {Count} chunks with metadata", chunks.Count);
            return IndexResult.CreateSuccess(chunks.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index chunks with metadata");
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

            var filter = new Filter
            {
                Must =
                {
                    new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "category",
                            Match = new Match { Keyword = category }
                        }
                    }
                }
            };

            var searchResults = await _clientAdapter.SearchAsync(
                collectionName: CollectionName,
                vector: queryEmbedding,
                filter: filter,
                limit: (ulong)limit,
                cancellationToken: ct
            );

            var results = searchResults.Select(r => new SearchResultItem
            {
                Score = r.Score,
                Text = r.Payload["text"].StringValue,
                PdfId = r.Payload.ContainsKey("pdf_id") ? r.Payload["pdf_id"].StringValue : "",
                Page = (int)r.Payload["page"].IntegerValue,
                ChunkIndex = (int)r.Payload["chunk_index"].IntegerValue
            }).ToList();

            _logger.LogInformation("Found {Count} results in category {Category}", results.Count, category);
            return SearchResult.CreateSuccess(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Search failed for category {Category}", category);
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

            var filter = new Filter
            {
                Must =
                {
                    new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "category",
                            Match = new Match { Keyword = category }
                        }
                    }
                }
            };

            await _clientAdapter.DeleteAsync(
                collectionName: CollectionName,
                filter: filter,
                cancellationToken: ct
            );

            _logger.LogInformation("Successfully deleted vectors for category {Category}", category);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete vectors for category {Category}", category);
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
