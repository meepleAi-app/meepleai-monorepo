using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain;

public class CertificationThresholdsTests
{
    [Fact]
    public void Default_returns_70_10_80_60()
    {
        var d = CertificationThresholds.Default();
        d.MinCoveragePct.Should().Be(70);
        d.MaxPageTolerance.Should().Be(10);
        d.MinBggMatchPct.Should().Be(80);
        d.MinOverallScore.Should().Be(60);
    }

    [Theory]
    [InlineData(-1, 10, 80, 60)]
    [InlineData(70, -1, 80, 60)]
    [InlineData(70, 10, 101, 60)]
    [InlineData(70, 10, 80, 101)]
    public void Create_rejects_out_of_range(decimal cov, int page, decimal bgg, decimal overall)
    {
        var act = () => CertificationThresholds.Create(cov, page, bgg, overall);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IsCertified_passes_when_all_pass()
    {
        var t = CertificationThresholds.Default();
        t.IsCertified(coveragePct: 80, pageAccuracyPct: 95, bggMatchPct: 85, overallScore: 70)
            .Should().BeTrue();
    }

    [Fact]
    public void IsCertified_fails_when_any_missing()
    {
        var t = CertificationThresholds.Default();
        t.IsCertified(coveragePct: 65, pageAccuracyPct: 95, bggMatchPct: 85, overallScore: 70)
            .Should().BeFalse();
    }
}
