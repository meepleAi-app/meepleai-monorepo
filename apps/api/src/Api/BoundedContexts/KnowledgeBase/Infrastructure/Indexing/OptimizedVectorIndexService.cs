using Api.BoundedContexts.KnowledgeBase.Domain.Indexing;
using Api.Services;
using Microsoft.Extensions.Logging;
using Qdrant.Client.Grpc;
using DomainQuantizationType = Api.BoundedContexts.KnowledgeBase.Domain.Indexing.QuantizationType;
using QdrantQuantizationType = Qdrant.Client.Grpc.QuantizationType;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Indexing;

/// <summary>
/// ADR-016 Phase 3: Optimized vector index service with HNSW and quantization configuration.
/// Provides a domain-driven interface for Qdrant collection management with Phase 3 optimizations.
/// </summary>
public class OptimizedVectorIndexService : IOptimizedVectorIndexService
{
    private readonly IQdrantClientAdapter _clientAdapter;
    private readonly ILogger<OptimizedVectorIndexService> _logger;

    /// <summary>
    /// Default vector dimension for text-embedding-3-large model.
    /// ADR-016 Phase 3 specification.
    /// </summary>
    public const uint DefaultVectorDimension = 3072;

    /// <summary>
    /// Default collection name for board game rules.
    /// </summary>
    public const string DefaultCollectionName = "boardgame_rules";

    public OptimizedVectorIndexService(
        IQdrantClientAdapter clientAdapter,
        ILogger<OptimizedVectorIndexService> logger)
    {
        _clientAdapter = clientAdapter ?? throw new ArgumentNullException(nameof(clientAdapter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates or updates a collection with optimized HNSW and quantization configuration.
    /// </summary>
    /// <param name="collectionName">Name of the collection</param>
    /// <param name="vectorDimension">Vector dimension (default: 3072 for text-embedding-3-large)</param>
    /// <param name="hnswConfig">HNSW configuration (default: ADR-016 Phase 3 settings)</param>
    /// <param name="quantizationConfig">Quantization configuration (default: ADR-016 Phase 3 settings)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task EnsureOptimizedCollectionAsync(
        string collectionName,
        uint vectorDimension = DefaultVectorDimension,
        HnswConfiguration? hnswConfig = null,
        QuantizationConfiguration? quantizationConfig = null,
        CancellationToken cancellationToken = default)
    {
        hnswConfig ??= HnswConfiguration.Default();
        quantizationConfig ??= QuantizationConfiguration.Default();

        _logger.LogInformation(
            "Ensuring optimized collection {CollectionName} exists with {VectorDimension}D vectors, {HnswConfig}, {QuantizationConfig}",
            collectionName, vectorDimension, hnswConfig, quantizationConfig);

        var collections = await _clientAdapter.ListCollectionsAsync(cancellationToken).ConfigureAwait(false);

        if (collections.Contains(collectionName, StringComparer.Ordinal))
        {
            _logger.LogInformation("Collection {CollectionName} already exists, updating configuration", collectionName);
            await UpdateCollectionConfigurationAsync(collectionName, hnswConfig, quantizationConfig, cancellationToken).ConfigureAwait(false);
            return;
        }

        await CreateOptimizedCollectionAsync(
            collectionName, vectorDimension, hnswConfig, quantizationConfig, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a new optimized collection with HNSW and quantization configuration.
    /// </summary>
    public async Task CreateOptimizedCollectionAsync(
        string collectionName,
        uint vectorDimension,
        HnswConfiguration hnswConfig,
        QuantizationConfiguration quantizationConfig,
        CancellationToken cancellationToken = default)
    {
        var vectorParams = new VectorParams
        {
            Size = vectorDimension,
            Distance = Distance.Cosine,
            OnDisk = hnswConfig.OnDisk
        };

        var hnswConfigDiff = MapToHnswConfigDiff(hnswConfig);
        var qdrantQuantizationConfig = MapToQuantizationConfig(quantizationConfig);

        await _clientAdapter.CreateCollectionWithConfigAsync(
            collectionName,
            vectorParams,
            hnswConfigDiff,
            qdrantQuantizationConfig,
            cancellationToken).ConfigureAwait(false);

        // Create payload indexes for filtering (ADR-016 Phase 3)
        await CreatePayloadIndexesAsync(collectionName, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Created optimized collection {CollectionName} with {Dimensions}D vectors, HNSW(m={M}, ef={EfConstruct}), Quantization({Type})",
            collectionName, vectorDimension, hnswConfig.M, hnswConfig.EfConstruct, quantizationConfig.Type);
    }

    /// <summary>
    /// Updates an existing collection's HNSW and quantization configuration.
    /// </summary>
    public async Task UpdateCollectionConfigurationAsync(
        string collectionName,
        HnswConfiguration hnswConfig,
        QuantizationConfiguration quantizationConfig,
        CancellationToken cancellationToken = default)
    {
        var hnswConfigDiff = MapToHnswConfigDiff(hnswConfig);
        var quantizationConfigDiff = MapToQuantizationConfigDiff(quantizationConfig);

        await _clientAdapter.UpdateCollectionConfigAsync(
            collectionName,
            hnswConfigDiff,
            quantizationConfigDiff,
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Updated collection {CollectionName} configuration: HNSW(m={M}, ef={EfConstruct}), Quantization({Type})",
            collectionName, hnswConfig.M, hnswConfig.EfConstruct, quantizationConfig.Type);
    }

    /// <summary>
    /// Creates payload indexes for efficient filtering.
    /// Uses parallel execution for improved performance.
    /// </summary>
    private async Task CreatePayloadIndexesAsync(string collectionName, CancellationToken cancellationToken)
    {
        // Create all indexes in parallel for better performance
        var indexTasks = new[]
        {
            // Core indexes from existing implementation
            _clientAdapter.CreatePayloadIndexAsync(collectionName, "game_id", PayloadSchemaType.Keyword, cancellationToken),
            _clientAdapter.CreatePayloadIndexAsync(collectionName, "pdf_id", PayloadSchemaType.Keyword, cancellationToken),
            _clientAdapter.CreatePayloadIndexAsync(collectionName, "category", PayloadSchemaType.Keyword, cancellationToken),
            _clientAdapter.CreatePayloadIndexAsync(collectionName, "language", PayloadSchemaType.Keyword, cancellationToken),
            // ADR-016 Phase 3: Additional indexes for hierarchical chunks
            _clientAdapter.CreatePayloadIndexAsync(collectionName, "level", PayloadSchemaType.Integer, cancellationToken),
            _clientAdapter.CreatePayloadIndexAsync(collectionName, "parent_chunk_id", PayloadSchemaType.Keyword, cancellationToken),
            _clientAdapter.CreatePayloadIndexAsync(collectionName, "page_number", PayloadSchemaType.Integer, cancellationToken),
            _clientAdapter.CreatePayloadIndexAsync(collectionName, "element_type", PayloadSchemaType.Keyword, cancellationToken)
        };

        await Task.WhenAll(indexTasks).ConfigureAwait(false);

        _logger.LogInformation("Created payload indexes for collection {CollectionName}", collectionName);
    }

    /// <summary>
    /// Maps domain HnswConfiguration to Qdrant HnswConfigDiff.
    /// </summary>
    private static HnswConfigDiff MapToHnswConfigDiff(HnswConfiguration config)
    {
        return new HnswConfigDiff
        {
            M = (ulong)config.M,
            EfConstruct = (ulong)config.EfConstruct,
            FullScanThreshold = (ulong)config.FullScanThreshold,
            OnDisk = config.OnDisk
        };
    }

    /// <summary>
    /// Maps domain QuantizationConfiguration to Qdrant QuantizationConfig.
    /// </summary>
    private static QuantizationConfig? MapToQuantizationConfig(QuantizationConfiguration config)
    {
        if (!config.IsEnabled)
            return null;

        return config.Type switch
        {
            DomainQuantizationType.Int8 => new QuantizationConfig
            {
                Scalar = new ScalarQuantization
                {
                    Type = QdrantQuantizationType.Int8,
                    Quantile = (float)config.Quantile,
                    AlwaysRam = config.AlwaysRam
                }
            },
            DomainQuantizationType.Binary => new QuantizationConfig
            {
                Binary = new BinaryQuantization
                {
                    AlwaysRam = config.AlwaysRam
                }
            },
            _ => null
        };
    }

    /// <summary>
    /// Maps domain QuantizationConfiguration to Qdrant QuantizationConfigDiff for updates.
    /// </summary>
    private static QuantizationConfigDiff? MapToQuantizationConfigDiff(QuantizationConfiguration config)
    {
        if (!config.IsEnabled)
        {
            return new QuantizationConfigDiff
            {
                Disabled = new Disabled()
            };
        }

        return config.Type switch
        {
            DomainQuantizationType.Int8 => new QuantizationConfigDiff
            {
                Scalar = new ScalarQuantization
                {
                    Type = QdrantQuantizationType.Int8,
                    Quantile = (float)config.Quantile,
                    AlwaysRam = config.AlwaysRam
                }
            },
            DomainQuantizationType.Binary => new QuantizationConfigDiff
            {
                Binary = new BinaryQuantization
                {
                    AlwaysRam = config.AlwaysRam
                }
            },
            _ => null
        };
    }
}
