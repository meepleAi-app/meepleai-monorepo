using Api.Helpers;
using Api.Observability;
using Api.Services.Qdrant;
using Qdrant.Client.Grpc;
using System.Diagnostics;

namespace Api.Services;

/// <summary>
/// User-scoped private vector storage service for private PDF documents.
/// Issue #3651: Provides isolated vector search ensuring users can only access their own private documents.
/// Uses dedicated "private_rules" collection with user_id payload for filtering.
/// </summary>
internal class PrivateQdrantService : IPrivateQdrantService
{
    private readonly IQdrantCollectionManager _collectionManager;
    private readonly IQdrantVectorIndexer _vectorIndexer;
    private readonly IQdrantVectorSearcher _vectorSearcher;
    private readonly ILogger<PrivateQdrantService> _logger;

    private const string CollectionName = "private_rules";
    private readonly uint _vectorSize;

    public PrivateQdrantService(
        IQdrantCollectionManager collectionManager,
        IQdrantVectorIndexer vectorIndexer,
        IQdrantVectorSearcher vectorSearcher,
        IEmbeddingService embeddingService,
        ILogger<PrivateQdrantService> logger)
    {
        _collectionManager = collectionManager ?? throw new ArgumentNullException(nameof(collectionManager));
        _vectorIndexer = vectorIndexer ?? throw new ArgumentNullException(nameof(vectorIndexer));
        _vectorSearcher = vectorSearcher ?? throw new ArgumentNullException(nameof(vectorSearcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _vectorSize = (uint)embeddingService.GetEmbeddingDimensions();

        _logger.LogInformation(
            "PrivateQdrantService initialized with vector size {VectorSize} for collection {CollectionName}",
            _vectorSize, CollectionName);
    }

    /// <inheritdoc />
    public async Task<bool> CollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        return await _collectionManager.CollectionExistsAsync(CollectionName, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        var exists = await _collectionManager.CollectionExistsAsync(CollectionName, cancellationToken).ConfigureAwait(false);
        if (exists)
        {
            _logger.LogDebug("Private collection {CollectionName} already exists", CollectionName);
            return;
        }

        _logger.LogInformation("Creating private collection {CollectionName} with vector size {VectorSize}",
            CollectionName, _vectorSize);

        await _collectionManager.CreateCollectionAsync(CollectionName, _vectorSize, cancellationToken).ConfigureAwait(false);

        // Create payload indexes for efficient filtering
        await _collectionManager.CreatePayloadIndexAsync(CollectionName, "user_id", PayloadSchemaType.Keyword, cancellationToken).ConfigureAwait(false);
        await _collectionManager.CreatePayloadIndexAsync(CollectionName, "game_id", PayloadSchemaType.Keyword, cancellationToken).ConfigureAwait(false);
        await _collectionManager.CreatePayloadIndexAsync(CollectionName, "pdf_id", PayloadSchemaType.Keyword, cancellationToken).ConfigureAwait(false);
        await _collectionManager.CreatePayloadIndexAsync(CollectionName, "library_entry_id", PayloadSchemaType.Keyword, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Private collection {CollectionName} created with indexes", CollectionName);
    }

    /// <inheritdoc />
    public async Task<IndexResult> IndexPrivateAsync(
        Guid userId,
        Guid gameId,
        Guid pdfId,
        Guid libraryEntryId,
        List<DocumentChunk> chunks,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chunks);

        using var activity = MeepleAiActivitySources.VectorSearch.StartActivity("PrivateQdrantService.IndexPrivate");
        activity?.SetTag("user.id", userId.ToString());
        activity?.SetTag("game.id", gameId.ToString());
        activity?.SetTag("pdf.id", pdfId.ToString());
        activity?.SetTag("library_entry.id", libraryEntryId.ToString());
        activity?.SetTag("chunks.count", chunks.Count);
        activity?.SetTag("collection", CollectionName);

        if (chunks.Count == 0)
        {
            activity?.SetTag("success", false);
            activity?.SetTag("error", "No chunks to index");
            return IndexResult.CreateFailure("No chunks to index");
        }

        // Validate chunks
        var validChunks = ValidateChunks(chunks, pdfId);
        if (validChunks.Count == 0)
        {
            activity?.SetTag("success", false);
            activity?.SetTag("error", "No valid chunks after validation");
            return IndexResult.CreateFailure("No valid chunks to index");
        }

        activity?.SetTag("chunks.valid", validChunks.Count);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            _logger.LogInformation(
                "Indexing {Count} private chunks for user {UserId}, game {GameId}, PDF {PdfId}",
                validChunks.Count, userId, gameId, pdfId);

            // Build payload with user-scoped metadata
            var basePayload = new Dictionary<string, Value>(StringComparer.Ordinal)
            {
                ["user_id"] = userId.ToString(),
                ["game_id"] = gameId.ToString(),
                ["pdf_id"] = pdfId.ToString(),
                ["library_entry_id"] = libraryEntryId.ToString(),
                ["is_private"] = true
            };

            var points = _vectorIndexer.BuildPoints(validChunks, basePayload);
            await _vectorIndexer.UpsertPointsAsync(CollectionName, points.AsReadOnly(), cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Successfully indexed {Count} private chunks for PDF {PdfId}",
                validChunks.Count, pdfId);

            activity?.SetTag("success", true);
            activity?.SetTag("indexed.count", validChunks.Count);
            stopwatch.Stop();

            MeepleAiMetrics.VectorIndexingDuration.Record(
                stopwatch.Elapsed.TotalMilliseconds,
                new TagList { { "collection", CollectionName }, { "type", "private" } });

            return IndexResult.CreateSuccess(validChunks.Count);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            return RagExceptionHandler.HandleServiceException(
                ex, _logger, $"indexing private document chunks for PDF {pdfId}",
                errorMessage => IndexResult.CreateFailure(errorMessage),
                activity);
        }
#pragma warning restore CA1031
    }

    /// <inheritdoc />
    public Task<SearchResult> SearchPrivateAsync(
        Guid userId,
        Guid gameId,
        float[] queryEmbedding,
        int limit = 5,
        CancellationToken cancellationToken = default)
    {
        return SearchPrivateAsync(userId, gameId, queryEmbedding, null, limit, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SearchResult> SearchPrivateAsync(
        Guid userId,
        Guid gameId,
        float[] queryEmbedding,
        IReadOnlyList<Guid>? pdfIds,
        int limit = 5,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queryEmbedding);

        using var activity = MeepleAiActivitySources.VectorSearch.StartActivity("PrivateQdrantService.SearchPrivate");
        activity?.SetTag("user.id", userId.ToString());
        activity?.SetTag("game.id", gameId.ToString());
        activity?.SetTag("limit", limit);
        activity?.SetTag("collection", CollectionName);
        activity?.SetTag("vector.dimension", queryEmbedding.Length);
        activity?.SetTag("pdf.filter.enabled", pdfIds != null);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            _logger.LogInformation(
                "Searching private vectors for user {UserId}, game {GameId}, limit {Limit}, pdfFilter={HasFilter}",
                userId, gameId, limit, pdfIds != null);

            // Build filter: MUST match user_id AND game_id
            var filter = BuildUserGameFilter(userId, gameId);

            // Optionally filter by specific PDF IDs (nested Should within Must)
            // This ensures: (user_id = X AND game_id = Y) AND (pdf_id IN [A, B, C])
            if (pdfIds != null && pdfIds.Count > 0)
            {
                // Create a nested filter with Should (OR logic for pdf_ids)
                var pdfFilter = new Filter();
                foreach (var pdfId in pdfIds)
                {
                    pdfFilter.Should.Add(new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "pdf_id",
                            Match = new Match { Keyword = pdfId.ToString() }
                        }
                    });
                }

                // Add the nested filter to Must (AND logic with user/game)
                filter.Must.Add(new Condition { Filter = pdfFilter });
            }

            var searchResults = await _vectorSearcher.SearchAsync(
                collectionName: CollectionName,
                queryEmbedding: queryEmbedding,
                filter: filter,
                limit: limit,
                cancellationToken: cancellationToken
            ).ConfigureAwait(false);

            var results = _vectorSearcher.ConvertToSearchResults(searchResults);

            _logger.LogInformation(
                "Found {Count} private results for user {UserId}",
                results.Count, userId);

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
        catch (Exception ex)
        {
            return RagExceptionHandler.HandleServiceException(
                ex, _logger, $"private vector search for user {userId} and game {gameId}",
                errorMessage => SearchResult.CreateFailure(errorMessage),
                activity);
        }
#pragma warning restore CA1031
    }

    /// <inheritdoc />
    public async Task<bool> DeletePrivateByPdfIdAsync(
        Guid userId,
        Guid pdfId,
        CancellationToken cancellationToken = default)
    {
        using var activity = MeepleAiActivitySources.VectorSearch.StartActivity("PrivateQdrantService.DeletePrivateByPdfId");
        activity?.SetTag("user.id", userId.ToString());
        activity?.SetTag("pdf.id", pdfId.ToString());
        activity?.SetTag("collection", CollectionName);

        try
        {
            _logger.LogInformation(
                "Deleting private vectors for user {UserId}, PDF {PdfId}",
                userId, pdfId);

            // Build filter: MUST match user_id AND pdf_id
            var filter = new Filter
            {
                Must =
                {
                    new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "user_id",
                            Match = new Match { Keyword = userId.ToString() }
                        }
                    },
                    new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "pdf_id",
                            Match = new Match { Keyword = pdfId.ToString() }
                        }
                    }
                }
            };

            await _vectorIndexer.DeleteByFilterAsync(CollectionName, filter, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Successfully deleted private vectors for PDF {PdfId}",
                pdfId);

            activity?.SetTag("success", true);
            return true;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            return RagExceptionHandler.HandleServiceException(
                ex, _logger, $"deleting private vectors for PDF {pdfId}",
                _ => false,
                activity);
        }
#pragma warning restore CA1031
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAllUserVectorsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        using var activity = MeepleAiActivitySources.VectorSearch.StartActivity("PrivateQdrantService.DeleteAllUserVectors");
        activity?.SetTag("user.id", userId.ToString());
        activity?.SetTag("collection", CollectionName);

        try
        {
            _logger.LogInformation("Deleting all private vectors for user {UserId}", userId);

            var filter = new Filter
            {
                Must =
                {
                    new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "user_id",
                            Match = new Match { Keyword = userId.ToString() }
                        }
                    }
                }
            };

            await _vectorIndexer.DeleteByFilterAsync(CollectionName, filter, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Successfully deleted all private vectors for user {UserId}", userId);

            activity?.SetTag("success", true);
            return true;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            return RagExceptionHandler.HandleServiceException(
                ex, _logger, $"deleting all private vectors for user {userId}",
                _ => false,
                activity);
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Build filter for user ID and game ID
    /// </summary>
    private static Filter BuildUserGameFilter(Guid userId, Guid gameId)
    {
        return new Filter
        {
            Must =
            {
                new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = "user_id",
                        Match = new Match { Keyword = userId.ToString() }
                    }
                },
                new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = "game_id",
                        Match = new Match { Keyword = gameId.ToString() }
                    }
                }
            }
        };
    }

    /// <summary>
    /// Validate chunks and filter out invalid ones
    /// </summary>
    private List<DocumentChunk> ValidateChunks(List<DocumentChunk> chunks, Guid pdfId)
    {
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

            var hasInvalidValue = embedding.ToArray().Any(value => float.IsNaN(value) || float.IsInfinity(value));
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
            _logger.LogWarning(
                "Skipping {InvalidCount} invalid chunks for private PDF {PdfId}: {Reasons}",
                invalidReasons.Count, pdfId, string.Join(", ", invalidReasons));
        }

        return validChunks;
    }
}
