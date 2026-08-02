using Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Evaluation;

/// <summary>
/// Unit tests for the page-level citation match policies (#3467).
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class ExpectedCitationsTests
{
    [Theory]
    // Exact: set equality, order-independent
    [InlineData("exact", new[] { 3, 5 }, new[] { 3, 5 }, true)]
    [InlineData("exact", new[] { 3, 5 }, new[] { 5, 3 }, true)]
    [InlineData("exact", new[] { 3, 5 }, new[] { 3 }, false)]
    [InlineData("exact", new[] { 3, 5 }, new[] { 3, 5, 7 }, false)]
    // OverlapAtLeastOne: non-empty intersection
    [InlineData("overlap_at_least_one", new[] { 3, 5 }, new[] { 5, 9 }, true)]
    [InlineData("overlap_at_least_one", new[] { 3, 5 }, new[] { 9 }, false)]
    // Subset: actual is a subset of expected (no extra pages cited)
    [InlineData("subset", new[] { 3, 5, 7 }, new[] { 3, 5 }, true)]
    [InlineData("subset", new[] { 3, 5 }, new[] { 3, 9 }, false)]
    // Superset: actual is a superset of expected (all expected pages present)
    [InlineData("superset", new[] { 3, 5 }, new[] { 3, 5, 7 }, true)]
    [InlineData("superset", new[] { 3, 5 }, new[] { 3 }, false)]
    public void IsSatisfiedBy_AppliesPolicy(string policyWire, int[] expectedPages, int[] actualPages, bool expected)
    {
        // Arrange — policy parsed from its snake_case wire value (exercises the wire mapping too)
        var citations = new ExpectedCitations
        {
            PrimaryPages = expectedPages,
            MatchPolicy = CitationMatchPolicyWire.Parse(policyWire)
        };

        // Act & Assert
        citations.IsSatisfiedBy(actualPages).Should().Be(expected);
    }

    [Fact]
    public void IsSatisfiedBy_ExactWithEmptyExpected_IsTrueOnlyWhenActualEmpty()
    {
        // "No answer expected" edge case: the answer must cite nothing.
        var noAnswer = new ExpectedCitations { PrimaryPages = [], MatchPolicy = CitationMatchPolicy.Exact };

        noAnswer.IsSatisfiedBy([]).Should().BeTrue();
        noAnswer.IsSatisfiedBy(new[] { 2 }).Should().BeFalse();
    }

    [Fact]
    public void IsSatisfiedBy_IgnoresDuplicatePages()
    {
        var citations = new ExpectedCitations { PrimaryPages = new[] { 4 }, MatchPolicy = CitationMatchPolicy.Exact };

        citations.IsSatisfiedBy(new[] { 4, 4, 4 }).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_NullOrBlank_DefaultsToOverlap(string? wire)
    {
        // Omitted match_policy is the documented default (a sample can carry primary_pages without a policy).
        CitationMatchPolicyWire.Parse(wire).Should().Be(CitationMatchPolicy.OverlapAtLeastOne);
    }

    [Theory]
    [InlineData("exact_match")]
    [InlineData("overlap")]
    [InlineData("sub_set")]
    [InlineData("nonsense")]
    public void Parse_UnrecognizedToken_ThrowsInsteadOfSilentlyWeakeningTheGate(string wire)
    {
        // A typo'd policy must fail loud rather than silently degrade to the loosest comparison,
        // which would weaken the citation-accuracy gate in the false-negative direction (#3467 review).
        Action act = () => CitationMatchPolicyWire.Parse(wire);
        act.Should().Throw<ArgumentException>();
    }
}
