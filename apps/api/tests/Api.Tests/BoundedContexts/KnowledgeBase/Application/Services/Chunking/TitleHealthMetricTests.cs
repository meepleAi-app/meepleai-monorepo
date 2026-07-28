using Api.BoundedContexts.KnowledgeBase.Application.Services.Chunking;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services.Chunking;

/// <summary>
/// Epic #3338 WP3 unit tests for <see cref="TitleHealthMetric"/> — the per-game extraction-quality
/// signal (regression guard + WP4 go/no-go). Fixtures use the real garbage headings observed on the
/// Terraforming Mars staging corpus.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class TitleHealthMetricTests
{
    [Theory]
    // Real section titles → plausible
    [InlineData("PREPARAZIONE", true)]
    [InlineData("AZIONI", true)]
    [InlineData("FINE DEL GIOCO", true)]
    [InlineData("PANORAMICA DEL GIOCO", true)]
    [InlineData("Setup", true)]
    [InlineData("Dato", true)]                 // a real-word fragment: title-LIKE, counts as plausible
    // Gross extraction garbage → not plausible
    [InlineData("D", false)]                   // single char
    [InlineData("S U", false)]                 // spaced single letters, no 3-letter run
    [InlineData("I L E X Y R F C A A S I", false)] // vertical-text artefact
    [InlineData("(14%), Oceani", false)]       // symbol/number-heavy (letter fraction < 0.6)
    [InlineData("© 2016 FryxGames www.fryxgames.se/games/terraforming", false)] // too long + symbol-heavy
    [InlineData("12 34 56", false)]            // digits only, no word
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void IsPlausibleHeading_ClassifiesRealTitlesVsGarbage(string heading, bool expected)
    {
        TitleHealthMetric.IsPlausibleHeading(heading).Should().Be(expected);
    }

    [Fact]
    public void Compute_WellExtractedGame_IsGreen()
    {
        var headings = new[] { "SETUP", "GAMEPLAY", "SCORING", "END OF GAME", "COMPONENTS" };

        var r = TitleHealthMetric.Compute(headings);

        r.DistinctHeadings.Should().Be(5);
        r.PlausibleHeadings.Should().Be(5);
        r.PlausibleFraction.Should().Be(1.0);
        r.Band.Should().Be("green");
    }

    [Fact]
    public void Compute_TmLikeGarbageMix_IsRedOrYellow_BelowThreshold()
    {
        // Roughly the TM shape: a few real titles buried under vertical-text / symbol garbage.
        var headings = new[]
        {
            "PREPARAZIONE", "AZIONI", "CARTE",                 // plausible (3)
            "D", "S U", "I L E X Y R F C A A S I", "(14%), Oceani", "Dato",  // garbage-ish
        };

        var r = TitleHealthMetric.Compute(headings);

        r.DistinctHeadings.Should().Be(8);
        r.PlausibleFraction.Should().BeLessThan(TitleHealthMetric.Compute(new[] { "SETUP" }).PlausibleFraction + 0.01);
        r.Band.Should().NotBe("green"); // extraction quality is degraded → not green
        r.CanonicalCoverage.Should().BeGreaterThan(0, "PREPARAZIONE/AZIONI are curated lexicon sections");
    }

    [Fact]
    public void Compute_DeduplicatesHeadings()
    {
        // Child chunks repeat the parent heading; the metric counts DISTINCT headings.
        var headings = new[] { "PREPARAZIONE", "PREPARAZIONE", "PREPARAZIONE", "AZIONI" };

        TitleHealthMetric.Compute(headings).DistinctHeadings.Should().Be(2);
    }

    [Fact]
    public void Compute_NoHeadings_IsRedWithZeroCounts()
    {
        var r = TitleHealthMetric.Compute(new string?[] { null, "", "   " });

        r.DistinctHeadings.Should().Be(0);
        r.PlausibleFraction.Should().Be(0.0);
        r.Band.Should().Be("red");
    }

    [Fact]
    public void Compute_CanonicalCoverage_CountsLexiconSectionsPresent()
    {
        // "PANORAMICA DEL GIOCO" contains PANORAMICA; PREPARAZIONE + AZIONI are direct lexicon entries.
        var r = TitleHealthMetric.Compute(new[] { "PREPARAZIONE", "AZIONI", "PANORAMICA DEL GIOCO", "Random Section" });

        r.CanonicalCoverage.Should().BeGreaterThanOrEqualTo(3);
    }
}
