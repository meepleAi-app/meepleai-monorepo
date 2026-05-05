namespace Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

public sealed record CertificationThresholds(
    decimal MinCoveragePct,
    int MaxPageTolerance,
    decimal MinBggMatchPct,
    decimal MinOverallScore)
{
    public static CertificationThresholds Default() => new(70m, 10, 80m, 60m);

    public static CertificationThresholds Create(decimal minCoveragePct, int maxPageTolerance, decimal minBggMatchPct, decimal minOverallScore)
    {
        if (minCoveragePct is < 0 or > 100) throw new ArgumentException("MinCoveragePct must be 0..100", nameof(minCoveragePct));
        if (maxPageTolerance < 0) throw new ArgumentException("MaxPageTolerance must be >= 0", nameof(maxPageTolerance));
        if (minBggMatchPct is < 0 or > 100) throw new ArgumentException("MinBggMatchPct must be 0..100", nameof(minBggMatchPct));
        if (minOverallScore is < 0 or > 100) throw new ArgumentException("MinOverallScore must be 0..100", nameof(minOverallScore));
        return new CertificationThresholds(minCoveragePct, maxPageTolerance, minBggMatchPct, minOverallScore);
    }

    public bool IsCertified(decimal coveragePct, decimal pageAccuracyPct, decimal bggMatchPct, decimal overallScore)
        => coveragePct >= MinCoveragePct
        && bggMatchPct >= MinBggMatchPct
        && overallScore >= MinOverallScore
        && pageAccuracyPct >= 0; // page tolerance applied per-claim during matching
}
