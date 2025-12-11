

#pragma warning disable MA0048 // File name must match type name - Contains Interface with supporting types
namespace Api.BoundedContexts.KnowledgeBase.Domain.GridSearch;

/// <summary>
/// ADR-016 Phase 5: Grid search configuration for RAG pipeline evaluation.
/// Defines all possible configuration combinations for benchmarking.
/// </summary>
public sealed record GridSearchConfiguration
{
    /// <summary>
    /// Unique identifier for this configuration.
    /// </summary>
    public required string ConfigurationId { get; init; }

    /// <summary>
    /// Human-readable name for the configuration.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Chunking configuration settings.
    /// </summary>
    public required ChunkingConfig Chunking { get; init; }

    /// <summary>
    /// Quantization configuration settings.
    /// </summary>
    public required QuantizationConfig Quantization { get; init; }

    /// <summary>
    /// Reranking configuration settings.
    /// </summary>
    public required RerankingConfig Reranking { get; init; }

    /// <summary>
    /// Gets all predefined grid search configurations.
    /// 3 chunking x 2 quantization x 2 reranking = 12 configurations.
    /// </summary>
    public static IReadOnlyList<GridSearchConfiguration> GetAllConfigurations()
    {
        var configurations = new List<GridSearchConfiguration>();
        var chunkingConfigs = ChunkingConfig.GetAll();
        var quantizationConfigs = QuantizationConfig.GetAll();
        var rerankingConfigs = RerankingConfig.GetAll();

        foreach (var chunking in chunkingConfigs)
        {
            foreach (var quantization in quantizationConfigs)
            {
                foreach (var reranking in rerankingConfigs)
                {
                    var configId = $"{chunking.Name}_{quantization.Name}_{reranking.Name}";
                    configurations.Add(new GridSearchConfiguration
                    {
                        ConfigurationId = configId,
                        Name = $"{chunking.DisplayName} + {quantization.DisplayName} + {reranking.DisplayName}",
                        Chunking = chunking,
                        Quantization = quantization,
                        Reranking = reranking
                    });
                }
            }
        }

        return configurations.AsReadOnly();
    }

    /// <summary>
    /// Gets a subset of configurations for quick evaluation (3 configs).
    /// </summary>
    public static IReadOnlyList<GridSearchConfiguration> GetQuickConfigurations()
    {
        // Return baseline, best expected, and one variant
        var all = GetAllConfigurations();
        return new[]
        {
            all.First(c => string.Equals(c.ConfigurationId, "baseline_none_no_rerank", StringComparison.Ordinal)),
            all.First(c => string.Equals(c.ConfigurationId, "baseline_none_bge_rerank", StringComparison.Ordinal)),
            all.First(c => string.Equals(c.ConfigurationId, "dense_scalar_int8_bge_rerank", StringComparison.Ordinal))
        };
    }
}

/// <summary>
/// Chunking configuration for grid search.
/// </summary>
public sealed record ChunkingConfig
{
    /// <summary>
    /// Configuration identifier.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Human-readable display name.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Chunk size in tokens.
    /// </summary>
    public required int SizeTokens { get; init; }

    /// <summary>
    /// Overlap percentage (0.0 to 1.0).
    /// </summary>
    public required double OverlapPercent { get; init; }

    /// <summary>
    /// Gets all predefined chunking configurations.
    /// </summary>
    public static IReadOnlyList<ChunkingConfig> GetAll() =>
    [
        new ChunkingConfig
        {
            Name = "dense",
            DisplayName = "Dense (200/20%)",
            SizeTokens = 200,
            OverlapPercent = 0.20
        },
        new ChunkingConfig
        {
            Name = "baseline",
            DisplayName = "Baseline (350/15%)",
            SizeTokens = 350,
            OverlapPercent = 0.15
        },
        new ChunkingConfig
        {
            Name = "sparse",
            DisplayName = "Sparse (500/10%)",
            SizeTokens = 500,
            OverlapPercent = 0.10
        }
    ];
}

/// <summary>
/// Quantization configuration for grid search.
/// </summary>
public sealed record QuantizationConfig
{
    /// <summary>
    /// Configuration identifier.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Human-readable display name.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Quantization type.
    /// </summary>
    public required QuantizationType Type { get; init; }

    /// <summary>
    /// Gets all predefined quantization configurations.
    /// </summary>
    public static IReadOnlyList<QuantizationConfig> GetAll() =>
    [
        new QuantizationConfig
        {
            Name = "none",
            DisplayName = "Full Precision",
            Type = QuantizationType.None
        },
        new QuantizationConfig
        {
            Name = "scalar_int8",
            DisplayName = "Scalar INT8",
            Type = QuantizationType.ScalarInt8
        }
    ];
}

/// <summary>
/// Quantization type enumeration.
/// </summary>
public enum QuantizationType
{
    /// <summary>
    /// No quantization - full precision vectors.
    /// </summary>
    None,

    /// <summary>
    /// Scalar INT8 quantization - 75% memory reduction, less than 1% accuracy loss.
    /// </summary>
    ScalarInt8
}

/// <summary>
/// Reranking configuration for grid search.
/// </summary>
public sealed record RerankingConfig
{
    /// <summary>
    /// Configuration identifier.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Human-readable display name.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Whether reranking is enabled.
    /// </summary>
    public required bool Enabled { get; init; }

    /// <summary>
    /// Model name if reranking is enabled.
    /// </summary>
    public string? ModelName { get; init; }

    /// <summary>
    /// Gets all predefined reranking configurations.
    /// </summary>
    public static IReadOnlyList<RerankingConfig> GetAll() =>
    [
        new RerankingConfig
        {
            Name = "no_rerank",
            DisplayName = "No Reranking",
            Enabled = false,
            ModelName = null
        },
        new RerankingConfig
        {
            Name = "bge_rerank",
            DisplayName = "BGE Reranker v2-m3",
            Enabled = true,
            ModelName = "BAAI/bge-reranker-v2-m3"
        }
    ];
}
