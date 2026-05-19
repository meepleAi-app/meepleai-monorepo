using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Infrastructure.Services;

/// <summary>
/// Unit tests for <see cref="RegexParagraphNumberExtractor"/> covering both
/// the regex grammar and the issue #747 ≥ 80 % accuracy acceptance criterion
/// on a hand-curated test set of three distinct gamebook numbering styles.
///
/// Accuracy metric (precision-oriented per AC):
///   precision = |extracted ∩ expected| / |extracted|
///   recall    = |extracted ∩ expected| / |expected|
///   accuracy  = harmonic mean (F1-like) >= 0.80 on each fixture
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "747")]
public sealed class RegexParagraphNumberExtractorTests
{
    private static RegexParagraphNumberExtractor CreateSut() => new();

    // -----------------------------------------------------------------------
    // Edge cases — empty / null / whitespace
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\n\t\n")]
    public void Extract_EmptyOrWhitespace_ReturnsEmpty(string? input)
    {
        CreateSut().Extract(input).Should().BeEmpty();
    }

    [Fact]
    public void Extract_NoParagraphHeaders_ReturnsEmpty()
    {
        const string text =
            "This is plain prose with no paragraph numbering convention.\n" +
            "The player draws a card and resolves it. Roll 1d6.";

        CreateSut().Extract(text).Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // Pattern 1 — "N." naked number + period (most common gamebook style)
    // -----------------------------------------------------------------------

    [Fact]
    public void Extract_NakedNumberPeriod_CapturesAllHeaders()
    {
        const string text =
            "1. Setup the board as shown in figure A.\n" +
            "2. Each player draws three cards.\n" +
            "3. Determine the start player by die roll.";

        CreateSut().Extract(text).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Extract_NakedNumberPeriod_IgnoresMidSentenceNumbers()
    {
        // "retreat 3 hexes" must NOT match — the regex is anchored to start-of-line.
        const string text = "The unit can retreat 3 hexes. Lose 2 morale.";
        CreateSut().Extract(text).Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // Pattern 2 — "Paragrafo N" / "Paragraph N" (IT + EN labels)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("Paragrafo 42\nIn questo paragrafo affronti il drago.", 42)]
    [InlineData("Paragraph 42\nIn this paragraph you face the dragon.", 42)]
    [InlineData("paragrafo 7 — Il bosco\nCammini tra gli alberi.", 7)]
    [InlineData("PARAGRAPH 100\nThe final boss appears.", 100)]
    public void Extract_LabeledParagraph_CapturesNumber(string text, int expected)
    {
        CreateSut().Extract(text).Should().ContainSingle().Which.Should().Be(expected);
    }

    // -----------------------------------------------------------------------
    // Pattern 3 — "§ N" section glyph
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("§ 12\nLe regole speciali si applicano.", 12)]
    [InlineData("§42 — Regola del fuoco amico", 42)]
    public void Extract_SectionGlyph_CapturesNumber(string text, int expected)
    {
        CreateSut().Extract(text).Should().ContainSingle().Which.Should().Be(expected);
    }

    // -----------------------------------------------------------------------
    // Pattern 4 — "(N)" parenthesised
    // -----------------------------------------------------------------------

    [Fact]
    public void Extract_ParenthesizedHeader_CapturesNumber()
    {
        const string text = "(42)\nCammini nella foresta oscura.";
        CreateSut().Extract(text).Should().ContainSingle().Which.Should().Be(42);
    }

    // -----------------------------------------------------------------------
    // De-duplication + ordering
    // -----------------------------------------------------------------------

    [Fact]
    public void Extract_DuplicatesAcrossPatterns_DeduplicatesAndSorts()
    {
        // Same paragraph "42" appears as "Paragrafo 42" AND as "42." — must collapse.
        const string text =
            "Paragrafo 42\nVai al bosco.\n" +
            "42. Cammini tra gli alberi.\n" +
            "3. Vedi un cavaliere.\n" +
            "1. Estrai la spada.";

        CreateSut().Extract(text).Should().Equal(1, 3, 42);
    }

    // -----------------------------------------------------------------------
    // ReDoS guard — pathologically long input completes within timeout
    // -----------------------------------------------------------------------

    [Fact]
    public void Extract_VeryLongNoiseInput_CompletesWithoutTimeout()
    {
        var longInput = new string('a', 50_000) + "\n42. Real paragraph header\n" + new string('b', 50_000);

        var result = CreateSut().Extract(longInput);

        result.Should().ContainSingle().Which.Should().Be(42);
    }

    // -----------------------------------------------------------------------
    // Issue #747 AC — ≥ 80 % accuracy on 3 distinct gamebook styles
    // -----------------------------------------------------------------------

    /// <summary>
    /// Test set 1 — Italian gamebook (Lupo Solitario style): "N." headers,
    /// long prose between headers, occasional mid-sentence numbers.
    /// </summary>
    [Fact]
    public void Extract_GamebookFixture_ItalianNumberDot_MeetsAccuracyThreshold()
    {
        const string ocrText = """
            1. Ti svegli in una radura silenziosa. Il sole filtra tra i rami.
            Decidi cosa fare: se vuoi esplorare la foresta vai al 5.
            Se preferisci tornare al villaggio vai al 12.

            5. La foresta è fitta e oscura. Senti un rumore.
            Tira 2d6 e somma 3 al risultato.

            12. Il villaggio è in fiamme. Quattro guerrieri ti aspettano.
            Combatti: forza 8, vita 15.

            42. Hai trovato l'amuleto. Vai al paragrafo 100 per concludere.

            100. Vittoria! Hai completato l'avventura.
            """;

        var expected = new[] { 1, 5, 12, 42, 100 };
        var extracted = CreateSut().Extract(ocrText);

        AssertAccuracyAboveThreshold(expected, extracted, threshold: 0.80, fixtureName: "ItalianNumberDot");
    }

    /// <summary>
    /// Test set 2 — English narrative gamebook with explicit "Paragraph N" labels
    /// (Tainted Grail / ISS Vanguard style).
    /// </summary>
    [Fact]
    public void Extract_GamebookFixture_EnglishLabeled_MeetsAccuracyThreshold()
    {
        const string ocrText = """
            Paragraph 1
            You arrive at the gates of Avalon as fog rolls in from the marshes.
            The gate is barred. You can shout to attract attention or look for another way in.

            Paragraph 7
            A guard appears on the wall. He demands a password. You have 2 turns.

            Paragraph 23
            You scale the wall under cover of darkness. Roll for stealth.

            Paragraph 100
            The king grants you audience. Your quest begins.

            Paragraph 200
            You face the final guardian. Combat ensues.
            """;

        var expected = new[] { 1, 7, 23, 100, 200 };
        var extracted = CreateSut().Extract(ocrText);

        AssertAccuracyAboveThreshold(expected, extracted, threshold: 0.80, fixtureName: "EnglishLabeled");
    }

    /// <summary>
    /// Test set 3 — mixed-format rulebook with "§" section glyph and "(N)"
    /// parenthesised references (legal-style rulebooks / older gamebooks).
    /// </summary>
    [Fact]
    public void Extract_GamebookFixture_MixedSectionAndParenthesised_MeetsAccuracyThreshold()
    {
        const string ocrText = """
            § 1
            Setup rules. Each player receives 7 cards.

            § 2
            Turn order. Resolve actions clockwise.

            (15)
            Combat rules. Roll 2d6 against opponent's defense.

            § 33
            Victory conditions. First to 10 points wins.

            (42)
            Special rule: night phase. All units lose 1 morale.
            """;

        var expected = new[] { 1, 2, 15, 33, 42 };
        var extracted = CreateSut().Extract(ocrText);

        AssertAccuracyAboveThreshold(expected, extracted, threshold: 0.80, fixtureName: "MixedSectionParenthesised");
    }

    // -----------------------------------------------------------------------
    // Accuracy helper
    // -----------------------------------------------------------------------

    /// <summary>
    /// Asserts F1 score (harmonic mean of precision + recall) >= <paramref name="threshold"/>.
    /// F1 is preferred over straight precision because the AC must guard against
    /// both over-extraction (precision) and missed paragraphs (recall) — a 100 %-precision
    /// extractor that finds 1 out of 10 paragraphs is useless.
    /// </summary>
    private static void AssertAccuracyAboveThreshold(
        IReadOnlyCollection<int> expected,
        IReadOnlyCollection<int> extracted,
        double threshold,
        string fixtureName)
    {
        var expectedSet = expected.ToHashSet();
        var extractedSet = extracted.ToHashSet();
        var truePositives = expectedSet.Intersect(extractedSet).Count();

        double precision = extractedSet.Count == 0 ? 0 : (double)truePositives / extractedSet.Count;
        double recall = expectedSet.Count == 0 ? 0 : (double)truePositives / expectedSet.Count;
        double f1 = (precision + recall) == 0 ? 0 : 2 * (precision * recall) / (precision + recall);

        f1.Should().BeGreaterThanOrEqualTo(threshold,
            "fixture '{0}' must hit issue #747 AC ≥ {1:P0} (precision={2:P0}, recall={3:P0}; expected={4}; extracted={5})",
            fixtureName, threshold, precision, recall, string.Join(',', expected), string.Join(',', extracted));
    }
}
