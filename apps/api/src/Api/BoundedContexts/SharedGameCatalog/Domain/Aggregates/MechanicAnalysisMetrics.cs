using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;

public sealed class MechanicAnalysisMetrics
{
    public Guid Id { get; private set; }
    public Guid MechanicAnalysisId { get; private set; }
    public Guid SharedGameId { get; private set; }
    public decimal CoveragePct { get; private set; }
    public decimal PageAccuracyPct { get; private set; }
    public decimal BggMatchPct { get; private set; }
    public decimal OverallScore { get; private set; }
    public CertificationStatus CertificationStatus { get; private set; }
    public string GoldenVersionHash { get; private set; } = string.Empty;
    public string ThresholdsSnapshotJson { get; private set; } = "{}";
    public string MatchDetailsJson { get; private set; } = "{}";
    public DateTimeOffset ComputedAt { get; private set; }

    private MechanicAnalysisMetrics() { }

    public static decimal ComputeOverallScore(decimal coverage, decimal page, decimal bgg)
        => Math.Round(coverage * 0.4m + page * 0.2m + bgg * 0.4m, 2);

    public static MechanicAnalysisMetrics Create(
        Guid analysisId, Guid sharedGameId,
        decimal coveragePct, decimal pageAccuracyPct, decimal bggMatchPct,
        CertificationThresholds thresholds, string goldenVersionHash, string matchDetailsJson)
    {
        ValidatePct(coveragePct, nameof(coveragePct));
        ValidatePct(pageAccuracyPct, nameof(pageAccuracyPct));
        ValidatePct(bggMatchPct, nameof(bggMatchPct));
        ArgumentNullException.ThrowIfNull(goldenVersionHash);
        if (goldenVersionHash.Length != 64) throw new ArgumentException("Hash must be 64 chars", nameof(goldenVersionHash));

        var overall = ComputeOverallScore(coveragePct, pageAccuracyPct, bggMatchPct);
        var status = thresholds.IsCertified(coveragePct, pageAccuracyPct, bggMatchPct, overall)
            ? CertificationStatus.Certified
            : CertificationStatus.NotCertified;

        var thresholdsJson = System.Text.Json.JsonSerializer.Serialize(thresholds);
        return new MechanicAnalysisMetrics
        {
            Id = Guid.NewGuid(),
            MechanicAnalysisId = analysisId,
            SharedGameId = sharedGameId,
            CoveragePct = coveragePct,
            PageAccuracyPct = pageAccuracyPct,
            BggMatchPct = bggMatchPct,
            OverallScore = overall,
            CertificationStatus = status,
            GoldenVersionHash = goldenVersionHash,
            ThresholdsSnapshotJson = thresholdsJson,
            MatchDetailsJson = matchDetailsJson ?? "{}",
            ComputedAt = DateTimeOffset.UtcNow,
        };
    }

    private static void ValidatePct(decimal v, string name)
    {
        if (v is < 0 or > 100) throw new ArgumentException($"{name} must be 0..100", name);
    }
}
