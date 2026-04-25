using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure.Services;

[Trait("Category", TestCategories.Unit)]
public class MechanicMatchingEngineTests
{
    // Embeddings chosen so cosine is deterministic:
    //   [3, 0]  vs  [3, sqrt(7)]  => 9 / (3 * 4) = 0.75  (AT threshold)
    //   [3, 0]  vs  [3, 4]        => 9 / (3 * 5) = 0.60  (BELOW threshold)
    //   [3, 0]  vs  [3, 0]        => 1.00
    private static readonly float[] EmbeddingA = { 3f, 0f };
    private static readonly float[] EmbeddingAtCosineThreshold = { 3f, (float)2.6457513110645907 }; // sqrt(7)
    private static readonly float[] EmbeddingBelowCosineThreshold = { 3f, 4f };
    private static readonly float[] EmbeddingAlignedWithA = { 6f, 0f }; // cosine = 1.0

    private readonly IMechanicMatchingEngine _sut = new MechanicMatchingEngine();
    private readonly CertificationThresholds _defaultThresholds = CertificationThresholds.Default();

    private static MechanicGoldenClaim BuildGolden(
        string[] keywords,
        float[]? embedding,
        int expectedPage,
        Guid? id = null)
    {
        var now = DateTimeOffset.UtcNow;
        return MechanicGoldenClaim.Reconstitute(
            id: id ?? Guid.NewGuid(),
            sharedGameId: Guid.NewGuid(),
            section: MechanicSection.Mechanics,
            statement: "Test statement",
            expectedPage: expectedPage,
            sourceQuote: "Test source quote",
            keywords: keywords,
            embedding: embedding,
            curatorUserId: Guid.NewGuid(),
            createdAt: now,
            updatedAt: now,
            deletedAt: null);
    }

    [Fact]
    public void Match_JaccardAtThreshold_AcceptsClaim()
    {
        // Jaccard: intersection=1, union=2 => 0.5 (exactly at threshold)
        var golden = BuildGolden(new[] { "alpha", "beta" }, EmbeddingA, expectedPage: 3);
        var analysis = new AnalysisClaim(Guid.NewGuid(), new[] { "alpha" }, EmbeddingA, Page: 3);

        var result = _sut.Match(
            new[] { analysis },
            new[] { golden },
            Array.Empty<MechanicGoldenBggTag>(),
            Array.Empty<AnalysisMechanicTag>(),
            _defaultThresholds);

        result.Matches.Should().HaveCount(1);
        result.CoveragePct.Should().Be(100m);
    }

    [Fact]
    public void Match_JaccardBelowThreshold_RejectsClaim()
    {
        // Jaccard: intersection=1, union=4 => 0.25 (below 0.5)
        var golden = BuildGolden(new[] { "alpha", "beta" }, EmbeddingA, expectedPage: 3);
        var analysis = new AnalysisClaim(Guid.NewGuid(), new[] { "alpha", "x", "y" }, EmbeddingA, Page: 3);

        var result = _sut.Match(
            new[] { analysis },
            new[] { golden },
            Array.Empty<MechanicGoldenBggTag>(),
            Array.Empty<AnalysisMechanicTag>(),
            _defaultThresholds);

        result.Matches.Should().BeEmpty();
        result.CoveragePct.Should().Be(0m);
    }

    [Fact]
    public void Match_CosineAtThreshold_AcceptsClaim()
    {
        var golden = BuildGolden(new[] { "alpha" }, EmbeddingAtCosineThreshold, expectedPage: 5);
        var analysis = new AnalysisClaim(Guid.NewGuid(), new[] { "alpha" }, EmbeddingA, Page: 5);

        var result = _sut.Match(
            new[] { analysis },
            new[] { golden },
            Array.Empty<MechanicGoldenBggTag>(),
            Array.Empty<AnalysisMechanicTag>(),
            _defaultThresholds);

        result.Matches.Should().HaveCount(1);
    }

    [Fact]
    public void Match_CosineBelowThreshold_RejectsClaim()
    {
        var golden = BuildGolden(new[] { "alpha" }, EmbeddingBelowCosineThreshold, expectedPage: 5);
        var analysis = new AnalysisClaim(Guid.NewGuid(), new[] { "alpha" }, EmbeddingA, Page: 5);

        var result = _sut.Match(
            new[] { analysis },
            new[] { golden },
            Array.Empty<MechanicGoldenBggTag>(),
            Array.Empty<AnalysisMechanicTag>(),
            _defaultThresholds);

        result.Matches.Should().BeEmpty();
    }

    [Fact]
    public void Match_PageDiffWithinTolerance_MarkedAccurate()
    {
        var thresholds = CertificationThresholds.Create(
            minCoveragePct: 70m,
            maxPageTolerance: 2,
            minBggMatchPct: 80m,
            minOverallScore: 60m);

        var golden = BuildGolden(new[] { "alpha" }, EmbeddingAlignedWithA, expectedPage: 10);
        // pageDiff = 2 (== tolerance) => PageAccurate = true
        var analysis = new AnalysisClaim(Guid.NewGuid(), new[] { "alpha" }, EmbeddingA, Page: 12);

        var result = _sut.Match(
            new[] { analysis },
            new[] { golden },
            Array.Empty<MechanicGoldenBggTag>(),
            Array.Empty<AnalysisMechanicTag>(),
            thresholds);

        result.Matches.Should().HaveCount(1);
        result.Matches[0].PageAccurate.Should().BeTrue();
        result.Matches[0].PageDiff.Should().Be(2);
        result.PageAccuracyPct.Should().Be(100m);
    }

    [Fact]
    public void Match_PageDiffBeyondTolerance_NotMarkedAccurate()
    {
        var thresholds = CertificationThresholds.Create(
            minCoveragePct: 70m,
            maxPageTolerance: 1,
            minBggMatchPct: 80m,
            minOverallScore: 60m);

        var golden = BuildGolden(new[] { "alpha" }, EmbeddingAlignedWithA, expectedPage: 10);
        // pageDiff = 2 (> tolerance of 1) => PageAccurate = false
        var analysis = new AnalysisClaim(Guid.NewGuid(), new[] { "alpha" }, EmbeddingA, Page: 12);

        var result = _sut.Match(
            new[] { analysis },
            new[] { golden },
            Array.Empty<MechanicGoldenBggTag>(),
            Array.Empty<AnalysisMechanicTag>(),
            thresholds);

        result.Matches.Should().HaveCount(1);
        result.Matches[0].PageAccurate.Should().BeFalse();
        result.Matches[0].PageDiff.Should().Be(2);
        result.PageAccuracyPct.Should().Be(0m);
    }

    [Fact]
    public void Match_GreedyFirstMatch_AssignsGoldenOnlyOnce()
    {
        // Both analysis claims qualify for the same single golden. Only the first wins.
        var goldenId = Guid.NewGuid();
        var golden = BuildGolden(new[] { "alpha" }, EmbeddingA, expectedPage: 3, id: goldenId);

        var firstAnalysisId = Guid.NewGuid();
        var secondAnalysisId = Guid.NewGuid();
        var first = new AnalysisClaim(firstAnalysisId, new[] { "alpha" }, EmbeddingA, Page: 3);
        var second = new AnalysisClaim(secondAnalysisId, new[] { "alpha" }, EmbeddingA, Page: 3);

        var result = _sut.Match(
            new[] { first, second },
            new[] { golden },
            Array.Empty<MechanicGoldenBggTag>(),
            Array.Empty<AnalysisMechanicTag>(),
            _defaultThresholds);

        result.Matches.Should().HaveCount(1);
        result.Matches[0].GoldenClaimId.Should().Be(goldenId);
        result.Matches[0].AnalysisClaimId.Should().Be(firstAnalysisId);
    }

    [Fact]
    public void Match_BggTagIntersection_IsCaseInsensitive()
    {
        var bggTag = MechanicGoldenBggTag.Create(Guid.NewGuid(), "Drafting", "Mechanism");
        var analysisTag = new AnalysisMechanicTag("drafting");

        var result = _sut.Match(
            Array.Empty<AnalysisClaim>(),
            Array.Empty<MechanicGoldenClaim>(),
            new[] { bggTag },
            new[] { analysisTag },
            _defaultThresholds);

        result.BggMatchPct.Should().Be(100m);
    }

    [Fact]
    public void Match_NoGoldenClaims_CoverageZero_AndMatchesEmpty()
    {
        var analysis = new AnalysisClaim(Guid.NewGuid(), new[] { "alpha" }, EmbeddingA, Page: 1);

        var result = _sut.Match(
            new[] { analysis },
            Array.Empty<MechanicGoldenClaim>(),
            Array.Empty<MechanicGoldenBggTag>(),
            Array.Empty<AnalysisMechanicTag>(),
            _defaultThresholds);

        result.CoveragePct.Should().Be(0m);
        result.Matches.Should().BeEmpty();
    }

    [Fact]
    public void Match_NoMatches_PageAccuracyZero()
    {
        // One golden, one analysis claim that fails Jaccard entirely => no matches.
        var golden = BuildGolden(new[] { "alpha" }, EmbeddingA, expectedPage: 3);
        var analysis = new AnalysisClaim(Guid.NewGuid(), new[] { "completely", "different" }, EmbeddingA, Page: 3);

        var result = _sut.Match(
            new[] { analysis },
            new[] { golden },
            Array.Empty<MechanicGoldenBggTag>(),
            Array.Empty<AnalysisMechanicTag>(),
            _defaultThresholds);

        result.Matches.Should().BeEmpty();
        result.PageAccuracyPct.Should().Be(0m); // divide-by-zero guard
    }

    [Fact]
    public void Match_NoBggTags_BggMatchPctZero()
    {
        var result = _sut.Match(
            Array.Empty<AnalysisClaim>(),
            Array.Empty<MechanicGoldenClaim>(),
            Array.Empty<MechanicGoldenBggTag>(),
            new[] { new AnalysisMechanicTag("anything") },
            _defaultThresholds);

        result.BggMatchPct.Should().Be(0m);
    }

    [Fact]
    public void Match_NullEmbeddingOnAnalysisSide_RejectsClaim()
    {
        var golden = BuildGolden(new[] { "alpha" }, EmbeddingA, expectedPage: 3);
        var analysis = new AnalysisClaim(Guid.NewGuid(), new[] { "alpha" }, Embedding: null, Page: 3);

        var result = _sut.Match(
            new[] { analysis },
            new[] { golden },
            Array.Empty<MechanicGoldenBggTag>(),
            Array.Empty<AnalysisMechanicTag>(),
            _defaultThresholds);

        result.Matches.Should().BeEmpty();
    }

    [Fact]
    public void Match_NullEmbeddingOnGoldenSide_RejectsClaim()
    {
        var golden = BuildGolden(new[] { "alpha" }, embedding: null, expectedPage: 3);
        var analysis = new AnalysisClaim(Guid.NewGuid(), new[] { "alpha" }, EmbeddingA, Page: 3);

        var result = _sut.Match(
            new[] { analysis },
            new[] { golden },
            Array.Empty<MechanicGoldenBggTag>(),
            Array.Empty<AnalysisMechanicTag>(),
            _defaultThresholds);

        result.Matches.Should().BeEmpty();
    }

    [Fact]
    public void Match_PercentagesRoundedToTwoDecimals()
    {
        // 1 of 3 golden matched => 33.333...% should round to 33.33.
        var matchingGolden = BuildGolden(new[] { "alpha" }, EmbeddingA, expectedPage: 1);
        var unmatched1 = BuildGolden(new[] { "beta" }, EmbeddingA, expectedPage: 2);
        var unmatched2 = BuildGolden(new[] { "gamma" }, EmbeddingA, expectedPage: 3);

        var analysis = new AnalysisClaim(Guid.NewGuid(), new[] { "alpha" }, EmbeddingA, Page: 1);

        var result = _sut.Match(
            new[] { analysis },
            new[] { matchingGolden, unmatched1, unmatched2 },
            Array.Empty<MechanicGoldenBggTag>(),
            Array.Empty<AnalysisMechanicTag>(),
            _defaultThresholds);

        result.Matches.Should().HaveCount(1);
        result.CoveragePct.Should().Be(33.33m);
    }

    [Fact]
    public void Match_PartialBggMatch_RoundedToTwoDecimals()
    {
        // 1 of 3 bgg tags matched => 33.33%
        var bgg1 = MechanicGoldenBggTag.Create(Guid.NewGuid(), "Drafting", "Mechanism");
        var bgg2 = MechanicGoldenBggTag.Create(Guid.NewGuid(), "Auction", "Mechanism");
        var bgg3 = MechanicGoldenBggTag.Create(Guid.NewGuid(), "Bluffing", "Mechanism");

        var analysisTag = new AnalysisMechanicTag("drafting");

        var result = _sut.Match(
            Array.Empty<AnalysisClaim>(),
            Array.Empty<MechanicGoldenClaim>(),
            new[] { bgg1, bgg2, bgg3 },
            new[] { analysisTag },
            _defaultThresholds);

        result.BggMatchPct.Should().Be(33.33m);
    }

    [Fact]
    public void Match_NullArguments_Throws()
    {
        var act1 = () => _sut.Match(null!, Array.Empty<MechanicGoldenClaim>(), Array.Empty<MechanicGoldenBggTag>(), Array.Empty<AnalysisMechanicTag>(), _defaultThresholds);
        var act2 = () => _sut.Match(Array.Empty<AnalysisClaim>(), null!, Array.Empty<MechanicGoldenBggTag>(), Array.Empty<AnalysisMechanicTag>(), _defaultThresholds);
        var act3 = () => _sut.Match(Array.Empty<AnalysisClaim>(), Array.Empty<MechanicGoldenClaim>(), null!, Array.Empty<AnalysisMechanicTag>(), _defaultThresholds);
        var act4 = () => _sut.Match(Array.Empty<AnalysisClaim>(), Array.Empty<MechanicGoldenClaim>(), Array.Empty<MechanicGoldenBggTag>(), null!, _defaultThresholds);
        var act5 = () => _sut.Match(Array.Empty<AnalysisClaim>(), Array.Empty<MechanicGoldenClaim>(), Array.Empty<MechanicGoldenBggTag>(), Array.Empty<AnalysisMechanicTag>(), null!);

        act1.Should().Throw<ArgumentNullException>();
        act2.Should().Throw<ArgumentNullException>();
        act3.Should().Throw<ArgumentNullException>();
        act4.Should().Throw<ArgumentNullException>();
        act5.Should().Throw<ArgumentNullException>();
    }
}
