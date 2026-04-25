using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;

/// <summary>
/// Pure matching engine that compares analysis claims against curated golden claims
/// and BGG mechanic tags, producing coverage, page-accuracy, and BGG-match percentages
/// plus per-claim match details. Consumed by <c>CalculateMechanicAnalysisMetricsCommand</c>.
/// </summary>
public interface IMechanicMatchingEngine
{
    /// <summary>
    /// Matches analysis claims to golden claims using greedy first-match with
    /// Jaccard (keywords) and cosine (embeddings) thresholds, and computes percentage
    /// metrics. Page accuracy for each match is evaluated using
    /// <paramref name="thresholds"/>.<see cref="CertificationThresholds.MaxPageTolerance"/>.
    /// </summary>
    MatchResult Match(
        IReadOnlyList<AnalysisClaim> analysisClaims,
        IReadOnlyList<MechanicGoldenClaim> golden,
        IReadOnlyList<MechanicGoldenBggTag> bggTags,
        IReadOnlyList<AnalysisMechanicTag> analysisTags,
        CertificationThresholds thresholds);
}

/// <summary>
/// DTO representing a pre-processed analysis claim ready for matching. Keywords must be
/// already extracted/normalized and embedding must be already computed by the caller.
/// </summary>
public sealed record AnalysisClaim(Guid Id, string[] Keywords, float[]? Embedding, int Page);

/// <summary>
/// DTO representing a BGG-like mechanic tag emitted by the analysis pipeline.
/// </summary>
public sealed record AnalysisMechanicTag(string Name);

/// <summary>
/// Aggregate matching outcome: the three percentage metrics and the list of per-claim
/// matches (one entry per matched analysis→golden pair).
/// </summary>
public sealed record MatchResult(
    decimal CoveragePct,
    decimal PageAccuracyPct,
    decimal BggMatchPct,
    IReadOnlyList<MatchDetail> Matches);

/// <summary>
/// Per-match detail linking a matched golden claim to an analysis claim, including
/// page accuracy evaluation and raw page difference.
/// </summary>
public sealed record MatchDetail(Guid GoldenClaimId, Guid AnalysisClaimId, bool PageAccurate, int PageDiff);
