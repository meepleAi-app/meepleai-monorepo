namespace Api.BoundedContexts.KbQuality.Application.Configuration;

public sealed class QualityBandsConfig
{
    public BandThreshold PrecisionAt5 { get; set; } = new();
    public BandThreshold Mrr { get; set; } = new();
    public BandThreshold LatencyP95Ms { get; set; } = new();
}

public sealed class BandThreshold
{
    /// <summary>
    /// For severity-DIRECT metrics (precision/MRR): values strictly less than RedMax → Red.
    /// Right-exclusive: e.g. 0.40 falls in Yellow band, not Red.
    /// </summary>
    public double RedMax { get; set; }

    /// <summary>
    /// For severity-DIRECT metrics: values in [RedMax, YellowMax) → Yellow. Above → Green.
    /// </summary>
    public double YellowMax { get; set; }

    /// <summary>
    /// For inverted-severity metrics (latency): values strictly less than GreenMax → Green.
    /// </summary>
    public double GreenMax { get; set; }

    /// <summary>
    /// When true: severity is inverted (lower = better). Defaults false.
    /// </summary>
    public bool InvertedSeverity { get; set; }
}
