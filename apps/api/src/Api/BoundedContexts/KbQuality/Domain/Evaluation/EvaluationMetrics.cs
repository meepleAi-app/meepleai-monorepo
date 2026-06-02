namespace Api.BoundedContexts.KbQuality.Domain.Evaluation;

public sealed record EvaluationMetrics(
    PrecisionMetrics Precision,
    RankingMetrics Ranking,
    LatencyMetrics Latency,
    int QueryCount,
    decimal CostUsd,
    QualityBand QualityBand)
{
    // Parameterless ctor for EF Core JSON owned-type materialization.
    // EF Core 9 cannot bind navigation-typed properties via the primary positional ctor,
    // so we expose a defaulted ctor that EF can invoke before setting properties from JSON.
    private EvaluationMetrics() : this(default!, default!, default!, 0, 0m, default) { }
}

public sealed record PrecisionMetrics(double At1, double At3, double At5)
{
    private PrecisionMetrics() : this(0, 0, 0) { }
}

public sealed record RankingMetrics(double Mrr)
{
    private RankingMetrics() : this(0) { }
}

public sealed record LatencyMetrics(TimeSpan P50, TimeSpan P95)
{
    private LatencyMetrics() : this(TimeSpan.Zero, TimeSpan.Zero) { }
}

public enum QualityBand { Red, Yellow, Green }
