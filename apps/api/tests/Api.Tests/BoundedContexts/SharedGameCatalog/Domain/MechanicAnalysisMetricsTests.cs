using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain;

public class MechanicAnalysisMetricsTests
{
    [Fact]
    public void Create_computes_overall_and_certification()
    {
        var t = CertificationThresholds.Default();
        var m = MechanicAnalysisMetrics.Create(
            analysisId: Guid.NewGuid(), sharedGameId: Guid.NewGuid(),
            coveragePct: 85, pageAccuracyPct: 95, bggMatchPct: 90,
            thresholds: t, goldenVersionHash: new string('a', 64),
            matchDetailsJson: "{}");

        m.OverallScore.Should().Be(MechanicAnalysisMetrics.ComputeOverallScore(85, 95, 90));
        m.CertificationStatus.Should().Be(CertificationStatus.Certified);
    }

    [Fact]
    public void Metrics_is_immutable_no_public_setters()
    {
        typeof(MechanicAnalysisMetrics).GetProperties()
            .Where(p => p.SetMethod is { IsPublic: true })
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData(-1, 50, 50)]
    [InlineData(50, 101, 50)]
    public void Create_rejects_out_of_range(decimal c, decimal p, decimal b)
    {
        var act = () => MechanicAnalysisMetrics.Create(Guid.NewGuid(), Guid.NewGuid(), c, p, b,
            CertificationThresholds.Default(), new string('a', 64), "{}");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_marks_metrics_NotCertified_when_below_thresholds()
    {
        var t = CertificationThresholds.Default();
        var m = MechanicAnalysisMetrics.Create(
            analysisId: Guid.NewGuid(), sharedGameId: Guid.NewGuid(),
            coveragePct: 50, pageAccuracyPct: 50, bggMatchPct: 50,
            thresholds: t, goldenVersionHash: new string('a', 64),
            matchDetailsJson: "{}");

        m.CertificationStatus.Should().Be(CertificationStatus.NotCertified);
    }
}
