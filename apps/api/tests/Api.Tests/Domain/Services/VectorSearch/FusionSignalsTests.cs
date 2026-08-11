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

    // --- #3338 WP1b: number-noise demotion ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Disponi le tessere sul tabellone e mescola il mazzo delle carte.")] // prose, no digits
    public void ComputeNumberNoiseFactor_NoDigits_ReturnsZero(string? content)
    {
        FusionSignals.ComputeNumberNoiseFactor(content).Should().Be(0f);
    }

    [Fact]
    public void ComputeNumberNoiseFactor_ProseCitingAFewNumbers_ReturnsZero()
    {
        // Letters-majority text (a few incidental numbers) is real prose — must not be demoted.
        var prose = "Di seguito viene descritta la preparazione per il gioco da 2 a 5 giocatori.";
        FusionSignals.ComputeNumberNoiseFactor(prose).Should().Be(0f);
    }

    [Fact]
    public void ComputeNumberNoiseFactor_ShortDigitDominatedFragment_ReturnsStrongDemotion()
    {
        // The exact TM garbage chunk that out-ranked the real setup prose under ts_rank_cd.
        var factor = FusionSignals.ComputeNumberNoiseFactor("CARTE 2 5 6 4 3");
        factor.Should().BeInRange(0f, 0.5f);
        factor.Should().BeGreaterThan(0.3f);
    }

    [Fact]
    public void ComputeNumberNoiseFactor_WhitespacePadding_DoesNotWeakenDemotion()
    {
        // Review #3347: the taper is token-count based, so a whitespace-heavy table fragment (the exact
        // WP1b target class) is demoted the SAME as its compact form — whitespace no longer inflates the
        // length denominator and under-demotes the fragment.
        var compact = FusionSignals.ComputeNumberNoiseFactor("CARTE 2 5 6 4 3");
        var padded = FusionSignals.ComputeNumberNoiseFactor("CARTE     2     5     6     4     3");

        padded.Should().BeApproximately(compact, 1e-6f);
        padded.Should().BeGreaterThan(0.3f);
    }

    [Fact]
    public void ComputeNumberNoiseFactor_LongNumberDenseTable_IsTaperedBelowShortFragment()
    {
        // A long, legitimately number-dense section (e.g. a scoring table) is tapered by length so it
        // is demoted far less than a short mostly-digits fragment of the same ratio.
        var shortFrag = "AB 1 2 3 4 5 6 7 8"; // ~digitRatio 0.5, short
        var longTable = "Punteggi finali giocatori " + string.Concat(Enumerable.Repeat("12 34 56 78 90 ", 30));
        var shortFactor = FusionSignals.ComputeNumberNoiseFactor(shortFrag);
        var longFactor = FusionSignals.ComputeNumberNoiseFactor(longTable);

        longFactor.Should().BeLessThan(shortFactor);
        longFactor.Should().BeInRange(0f, 0.5f);
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
