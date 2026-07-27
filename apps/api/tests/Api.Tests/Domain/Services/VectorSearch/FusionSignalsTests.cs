using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Domain.Services.VectorSearch;

public class FusionSignalsTests
{
    [Fact]
    public void ComputeRoleMatchBoost_WhenRolesIntersect_ReturnsBoost()
    {
        FusionSignals.ComputeRoleMatchBoost(GameBookRole.Setup, GameBookRole.Setup | GameBookRole.RulesReference)
            .Should().Be(0.15f);
    }

    [Fact]
    public void ComputeRoleMatchBoost_WhenHintIsNone_ReturnsZero()
    {
        FusionSignals.ComputeRoleMatchBoost(GameBookRole.None, GameBookRole.Setup).Should().Be(0f);
    }

    [Fact]
    public void ComputeRoleMatchBoost_WhenNoIntersection_ReturnsZero()
    {
        FusionSignals.ComputeRoleMatchBoost(GameBookRole.Setup, GameBookRole.Narrative).Should().Be(0f);
    }

    [Fact]
    public void ComputeLegendPenaltyFactor_WithFewerThanTwoPointers_ReturnsZero()
    {
        FusionSignals.ComputeLegendPenaltyFactor("See page 12 for details.").Should().Be(0f);
    }

    [Fact]
    public void ComputeLegendPenaltyFactor_WithDenseCrossReferences_ReturnsPenaltyInRange()
    {
        var legendy = "See p. 3. See p. 5. See p. 7. See p. 9.";
        var factor = FusionSignals.ComputeLegendPenaltyFactor(legendy);
        factor.Should().BeInRange(0f, 0.5f);
        factor.Should().BeGreaterThan(0f);
    }

    // --- #3270: heading-match boost ---

    [Fact]
    public void ComputeHeadingMatchBoost_TermInHeading_ReturnsBoost()
    {
        var terms = new[] { "setup", "giocatori" };
        FusionSignals.ComputeHeadingMatchBoost(terms, "Setup").Should().Be(FusionSignals.HeadingMatchBoost);
    }

    [Fact]
    public void ComputeHeadingMatchBoost_NoTermInHeading_ReturnsZero()
    {
        FusionSignals.ComputeHeadingMatchBoost(new[] { "scoring", "endgame" }, "Setup").Should().Be(0f);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ComputeHeadingMatchBoost_BlankHeading_ReturnsZero(string? heading)
    {
        FusionSignals.ComputeHeadingMatchBoost(new[] { "setup" }, heading).Should().Be(0f);
    }

    [Fact]
    public void ComputeHeadingMatchBoost_EmptyTerms_ReturnsZero()
    {
        FusionSignals.ComputeHeadingMatchBoost(System.Array.Empty<string>(), "Setup").Should().Be(0f);
    }

    [Fact]
    public void ComputeHeadingMatchBoost_IsCaseInsensitiveOnHeadingOnly()
    {
        // terms are contract-lowercased; heading may be any case
        FusionSignals.ComputeHeadingMatchBoost(new[] { "preparazione" }, "PREPARAZIONE del gioco")
            .Should().Be(FusionSignals.HeadingMatchBoost);
    }

    [Fact]
    public void ExtractHeadingMatchTerms_NormalizesLowercasesFiltersShortAndDedups()
    {
        // "N" dropped (len<3), trailing "setup" deduped, punctuation split
        FusionSignals.ExtractHeadingMatchTerms("Setup per N giocatori, setup!")
            .Should().Equal("setup", "per", "giocatori");
    }

    [Fact]
    public void ExtractHeadingMatchTerms_Blank_ReturnsEmpty()
    {
        FusionSignals.ExtractHeadingMatchTerms("  ").Should().BeEmpty();
    }
}
