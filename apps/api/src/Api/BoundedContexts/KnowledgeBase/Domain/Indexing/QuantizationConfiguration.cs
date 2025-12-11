using Api.SharedKernel.Domain.ValueObjects;

#pragma warning disable MA0048 // File name must match type name - Contains Interface with supporting types
namespace Api.BoundedContexts.KnowledgeBase.Domain.Indexing;

/// <summary>
/// ADR-016 Phase 3: Scalar quantization configuration value object.
/// Reduces vector storage by compressing float32 to int8, with ~75% memory reduction
/// and less than 1% accuracy loss for typical use cases.
/// </summary>
public sealed class QuantizationConfiguration : ValueObject
{
    /// <summary>
    /// Quantization type (Scalar = int8 compression).
    /// </summary>
    public QuantizationType Type { get; }

    /// <summary>
    /// Quantile for determining value ranges (0.95-0.999 typical).
    /// Higher values preserve more outliers but use more bits.
    /// Default: 0.99 (ADR-016 recommendation)
    /// </summary>
    public double Quantile { get; }

    /// <summary>
    /// Whether to always keep quantized vectors in RAM.
    /// True = faster search, higher memory usage.
    /// Default: true (ADR-016 recommendation for performance)
    /// </summary>
    public bool AlwaysRam { get; }

    private QuantizationConfiguration(QuantizationType type, double quantile, bool alwaysRam)
    {
        Type = type;
        Quantile = quantile;
        AlwaysRam = alwaysRam;
    }

    /// <summary>
    /// Creates the default ADR-016 Phase 3 scalar quantization configuration.
    /// Optimized for board game rules retrieval (int8, quantile=0.99, always_ram=true).
    /// </summary>
    public static QuantizationConfiguration Default() => new(
        type: QuantizationType.Int8,
        quantile: 0.99,
        alwaysRam: true
    );

    /// <summary>
    /// Creates disabled quantization (no compression).
    /// Use when maximum accuracy is required.
    /// </summary>
    public static QuantizationConfiguration Disabled() => new(
        type: QuantizationType.None,
        quantile: 0.99,
        alwaysRam: false
    );

    /// <summary>
    /// Creates a custom quantization configuration.
    /// </summary>
    /// <param name="type">Quantization type</param>
    /// <param name="quantile">Quantile for value ranges (0.9-0.999)</param>
    /// <param name="alwaysRam">Keep quantized vectors in RAM</param>
    public static QuantizationConfiguration Create(
        QuantizationType type = QuantizationType.Int8,
        double quantile = 0.99,
        bool alwaysRam = true)
    {
        if (quantile < 0.9 || quantile > 0.999)
            throw new ArgumentOutOfRangeException(nameof(quantile), "Quantile must be between 0.9 and 0.999");

        return new QuantizationConfiguration(type, quantile, alwaysRam);
    }

    /// <summary>
    /// Memory-optimized configuration for constrained environments.
    /// Trades RAM for disk, keeping full vectors on disk.
    /// </summary>
    public static QuantizationConfiguration MemoryOptimized() => new(
        type: QuantizationType.Int8,
        quantile: 0.95,
        alwaysRam: false
    );

    /// <summary>
    /// High-accuracy configuration with conservative quantization.
    /// </summary>
    public static QuantizationConfiguration HighAccuracy() => new(
        type: QuantizationType.Int8,
        quantile: 0.999,
        alwaysRam: true
    );

    /// <summary>
    /// Indicates whether quantization is enabled.
    /// </summary>
    public bool IsEnabled => Type != QuantizationType.None;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Type;
        yield return Quantile;
        yield return AlwaysRam;
    }

    public override string ToString() =>
        Type == QuantizationType.None
            ? "Quantization(disabled)"
            : $"Quantization({Type}, quantile={Quantile}, always_ram={AlwaysRam})";
}

/// <summary>
/// Quantization type for vector compression.
/// </summary>
public enum QuantizationType
{
    /// <summary>
    /// No quantization (full float32 precision).
    /// </summary>
    None = 0,

    /// <summary>
    /// Scalar quantization using int8 (1 byte per dimension).
    /// ~75% memory reduction with less than 1% accuracy loss.
    /// </summary>
    Int8 = 1,

    /// <summary>
    /// Binary quantization (1 bit per dimension).
    /// ~97% memory reduction but higher accuracy loss.
    /// Use only for very large datasets where memory is critical.
    /// </summary>
    Binary = 2
}
