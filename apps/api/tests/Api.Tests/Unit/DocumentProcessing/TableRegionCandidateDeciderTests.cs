using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Unit.DocumentProcessing;

/// <summary>
/// #3435 SP2 (router DC-B): the candidacy rule is a pure function of the image-region count.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3435")]
public sealed class TableRegionCandidateDeciderTests
{
    [Fact]
    public void DefaultMinImageRegions_IsOne()
    {
        TableRegionCandidateDecider.DefaultMinImageRegions.Should().Be(1);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(5, true)]
    public void IsCandidate_DefaultThreshold_FlagsAnyRegion(int count, bool expected)
    {
        TableRegionCandidateDecider.IsCandidate(count).Should().Be(expected);
    }

    [Theory]
    [InlineData(2, 3, false)] // below custom threshold
    [InlineData(3, 3, true)]  // at threshold
    [InlineData(4, 3, true)]  // above threshold
    public void IsCandidate_CustomThreshold_RespectsBoundary(int count, int min, bool expected)
    {
        TableRegionCandidateDecider.IsCandidate(count, min).Should().Be(expected);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void IsCandidate_NonPositiveThreshold_ClampedToOne(int min)
    {
        // A misconfigured 0/negative threshold must never flag a 0-region PDF (clamped to 1).
        TableRegionCandidateDecider.IsCandidate(0, min).Should().BeFalse();
        TableRegionCandidateDecider.IsCandidate(1, min).Should().BeTrue();
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-3, 1)]
    [InlineData(1, 1)]
    [InlineData(5, 5)]
    public void NormalizeThreshold_ClampsNonPositiveToOne(int input, int expected)
    {
        // Shared floor used by both IsCandidate and the query handler's SQL HAVING (no decider↔SQL drift).
        TableRegionCandidateDecider.NormalizeThreshold(input).Should().Be(expected);
    }
}
