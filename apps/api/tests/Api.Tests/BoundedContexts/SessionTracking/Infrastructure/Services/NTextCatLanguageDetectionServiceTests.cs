using Api.BoundedContexts.SessionTracking.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Infrastructure.Services;

public sealed class NTextCatLanguageDetectionServiceTests
{
    private static NTextCatLanguageDetectionService CreateSut()
        => new(NullLogger<NTextCatLanguageDetectionService>.Instance);

    /// <summary>
    /// Happy-path: ~50-word sentences covering all 5 allowlist languages.
    /// Confidence threshold 0.4 reflects NTextCat Core14 reality for short text (~50 chars):
    /// the out-of-place distance gap between winner and runner-up is small (5-30 units on
    /// a ~3900 base), yielding tanh-normalized confidence in [0.40, 0.90] depending on lang.
    /// Observed calibration values: EN≈0.66, IT≈0.76, FR≈0.82, DE≈0.90, ES≈0.47.
    /// The primary assertion is correct language identification; confidence verifies the
    /// signal is positive (non-trivial gap), not that it reaches human-perception-level certainty.
    /// Threshold adjusted from plan spec 0.7 per plan instruction to match actual NTextCat range.
    /// </summary>
    [Theory]
    [InlineData("You wake up in a dungeon. Roll 2d6 to escape the trap.", "EN")]
    [InlineData("Si sveglia in una segreta. Tira 2d6 per sfuggire alla trappola.", "IT")]
    [InlineData("Vous vous reveillez dans un donjon. Lancez 2d6 pour echapper au piege.", "FR")]
    [InlineData("Du erwachst in einem Verlies. Wirf 2d6, um der Falle zu entkommen.", "DE")]
    [InlineData("Te despiertas en una mazmorra. Tira 2d6 para escapar de la trampa.", "ES")]
    public void Detect_HappyPath_AllowlistLang_ReturnsCorrectLangWithHighConfidence(string text, string expectedLang)
    {
        var sut = CreateSut();

        var result = sut.Detect(text);

        result.Lang.Should().Be(expectedLang);
        // Threshold 0.4 per DEC-4 calibration (plan spec said 0.7, adjusted for NTextCat Core14 short-text reality).
        result.Confidence.Should().BeGreaterThan(0.4);
    }

    [Fact]
    public void Detect_OutsideAllowlist_Russian_ReturnsNullLang()
    {
        var sut = CreateSut();

        // Russian text: Core14 includes Russian model (rus) → NTextCat correctly identifies it,
        // but "RU" is not in the EN/FR/DE/ES/IT allowlist → Lang must be null (DEC-3).
        // Raw confidence is preserved in result per DEC-4 (calibrated: ≈0.9998 for 70-char Russian).
        var result = sut.Detect("Вы просыпаетесь в подземелье. Бросьте 2к6, чтобы сбежать из ловушки.");

        result.Lang.Should().BeNull();
        result.Confidence.Should().BeGreaterThan(0.0); // raw confidence preserved per DEC-4
    }

    [Fact]
    public void Detect_EmptyText_ReturnsNullLangZeroConfidence()
    {
        var sut = CreateSut();

        var result = sut.Detect(string.Empty);

        result.Lang.Should().BeNull();
        result.Confidence.Should().Be(0.0);
    }

    [Fact]
    public void Detect_WhitespaceOnly_ReturnsNullLangZeroConfidence()
    {
        var sut = CreateSut();

        var result = sut.Detect("   \n\t  \r\n  ");

        result.Lang.Should().BeNull();
        result.Confidence.Should().Be(0.0);
    }

    [Fact]
    public void Detect_VeryShortText_ReturnsNullOrLowConfidence()
    {
        var sut = CreateSut();

        // "Si." — 3 chars, ambiguous. NTextCat gap≈1 unit → tanh-confidence ≈ 0.048.
        var result = sut.Detect("Si.");

        // Either null lang OR confidence < 0.5 acceptable per DEC-2 ambiguous short text
        if (result.Lang is not null)
            result.Confidence.Should().BeLessThan(0.5);
    }

    [Fact]
    public void Detect_VeryLongText_DoesNotThrow()
    {
        var sut = CreateSut();
        var longText = string.Concat(Enumerable.Repeat("The quick brown fox jumps over the lazy dog. ", 10_000));

        var act = () => sut.Detect(longText);

        act.Should().NotThrow();
    }

    [Fact]
    public void Detect_NullText_ReturnsNullLangZeroConfidence()
    {
        var sut = CreateSut();

        var result = sut.Detect(null!);

        result.Lang.Should().BeNull();
        result.Confidence.Should().Be(0.0);
    }

    [Fact]
    public void Detect_ReturnedLangAlwaysUppercaseTwoLetterCode()
    {
        var sut = CreateSut();

        var result = sut.Detect("Hello world, this is English text for detection.");

        result.Lang.Should().NotBeNull();
        result.Lang!.Length.Should().Be(2);
        result.Lang.Should().Be(result.Lang.ToUpperInvariant());
    }

    [Fact]
    public void Detect_ConfidenceInBounds_AlwaysZeroToOne()
    {
        var sut = CreateSut();

        var result = sut.Detect("Hello world, this is English text for detection.");

        result.Confidence.Should().BeInRange(0.0, 1.0);
    }
}
