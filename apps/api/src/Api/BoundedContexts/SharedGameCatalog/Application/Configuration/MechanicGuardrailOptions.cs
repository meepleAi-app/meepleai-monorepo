namespace Api.BoundedContexts.SharedGameCatalog.Application.Configuration;

/// <summary>
/// ME-M1.3 (#525) tunable guardrail thresholds (ADR-051).
/// Bound from configuration section <see cref="SectionName"/>.
/// </summary>
public sealed class MechanicGuardrailOptions
{
    public const string SectionName = "MechanicGuardrails";

    /// <summary>T1: max words per citation quote.</summary>
    public int MaxQuoteWords { get; init; } = 25;

    /// <summary>T2: max contiguous normalized words from source allowed outside citation quotes.</summary>
    public int MaxConsecutiveSourceWords { get; init; } = 10;

    /// <summary>T3: minimum cosine similarity between a claim and its cited chunk.</summary>
    public double MinClaimGroundingSimilarity { get; init; } = 0.65;

    /// <summary>T8: hard cost cap (USD) for one analysis run, retry-inclusive.</summary>
    public decimal MaxAnalysisCostUsd { get; init; } = 2.00m;

    /// <summary>Max re-prompt retries per section (total attempts = value + 1).</summary>
    public int MaxRetriesPerSection { get; init; } = 2;

    /// <summary>Typical retry inflation factor for cost projection (1.3 = +30%).</summary>
    public decimal RetryCostInflationFactor { get; init; } = 1.3m;
}
