using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Indexing;

/// <summary>
/// ADR-016 Phase 3: HNSW index configuration value object.
/// Defines Hierarchical Navigable Small World graph parameters for vector search.
/// </summary>
public sealed class HnswConfiguration : ValueObject
{
    /// <summary>
    /// Number of edges per node in the HNSW graph.
    /// Higher values = more accurate but slower indexing.
    /// Default: 16 (ADR-016 recommendation for balance)
    /// </summary>
    public int M { get; }

    /// <summary>
    /// Number of neighbors to consider during index construction.
    /// Higher values = better quality but slower build time.
    /// Default: 100 (ADR-016 recommendation)
    /// </summary>
    public int EfConstruct { get; }

    /// <summary>
    /// Threshold below which to use full scan instead of HNSW.
    /// For small collections, full scan is faster.
    /// Default: 10000
    /// </summary>
    public int FullScanThreshold { get; }

    /// <summary>
    /// Whether to store HNSW graph on disk (vs RAM).
    /// Default: false (RAM storage for performance)
    /// </summary>
    public bool OnDisk { get; }

    private HnswConfiguration(int m, int efConstruct, int fullScanThreshold, bool onDisk)
    {
        M = m;
        EfConstruct = efConstruct;
        FullScanThreshold = fullScanThreshold;
        OnDisk = onDisk;
    }

    /// <summary>
    /// Creates the default ADR-016 Phase 3 configuration.
    /// Optimized for board game rules retrieval (high accuracy, moderate dataset size).
    /// </summary>
    public static HnswConfiguration Default() => new(
        m: 16,
        efConstruct: 100,
        fullScanThreshold: 10000,
        onDisk: false
    );

    /// <summary>
    /// Creates a custom HNSW configuration.
    /// </summary>
    /// <param name="m">Edges per node (4-64 recommended)</param>
    /// <param name="efConstruct">Construction neighbors (50-500 recommended)</param>
    /// <param name="fullScanThreshold">Full scan threshold (default 10000)</param>
    /// <param name="onDisk">Store on disk vs RAM</param>
    public static HnswConfiguration Create(
        int m = 16,
        int efConstruct = 100,
        int fullScanThreshold = 10000,
        bool onDisk = false)
    {
        if (m < 4 || m > 64)
            throw new ArgumentOutOfRangeException(nameof(m), "M must be between 4 and 64");

        if (efConstruct < 10 || efConstruct > 500)
            throw new ArgumentOutOfRangeException(nameof(efConstruct), "efConstruct must be between 10 and 500");

        if (fullScanThreshold < 0)
            throw new ArgumentOutOfRangeException(nameof(fullScanThreshold), "fullScanThreshold must be non-negative");

        return new HnswConfiguration(m, efConstruct, fullScanThreshold, onDisk);
    }

    /// <summary>
    /// High-accuracy configuration for production with larger datasets.
    /// </summary>
    public static HnswConfiguration HighAccuracy() => new(
        m: 24,
        efConstruct: 200,
        fullScanThreshold: 10000,
        onDisk: false
    );

    /// <summary>
    /// Memory-optimized configuration for constrained environments.
    /// </summary>
    public static HnswConfiguration MemoryOptimized() => new(
        m: 12,
        efConstruct: 64,
        fullScanThreshold: 5000,
        onDisk: true
    );

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return M;
        yield return EfConstruct;
        yield return FullScanThreshold;
        yield return OnDisk;
    }

    public override string ToString() =>
        $"HNSW(m={M}, ef_construct={EfConstruct}, full_scan={FullScanThreshold}, on_disk={OnDisk})";
}
