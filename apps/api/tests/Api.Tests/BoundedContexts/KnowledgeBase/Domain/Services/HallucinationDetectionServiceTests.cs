using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Microsoft.Extensions.Logging;
using Moq;
using System.Linq;
using FluentAssertions;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Unit tests for HallucinationDetectionService
/// ISSUE-972: BGAI-030 - Hallucination detection (forbidden keywords)
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class HallucinationDetectionServiceTests
{
    private readonly HallucinationDetectionService _service;
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public HallucinationDetectionServiceTests()
    {
        var mockLogger = new Mock<ILogger<HallucinationDetectionService>>();
        _service = new HallucinationDetectionService(mockLogger.Object);
    }

    // ========== English Tests ==========

    [Fact]
    public async Task English_NoHallucination_ReturnsValid()
    {
        var result = await _service.DetectHallucinationsAsync(
            "The game supports 2-4 players.", "en", TestCancellationToken);

        result.IsValid.Should().BeTrue();
        result.DetectedKeywords.Should().BeEmpty();
        result.Severity.Should().Be(HallucinationSeverity.None);
        result.Language.Should().Be("en");
    }

    [Fact]
    public async Task English_ContainsDontKnow_ReturnsInvalid()
    {
        var result = await _service.DetectHallucinationsAsync(
            "I don't know how many players can play.", "en", TestCancellationToken);

        result.IsValid.Should().BeFalse();
        result.DetectedKeywords.Should().Contain("I don't know");
        result.Severity.Should().Be(HallucinationSeverity.High);
    }

    [Fact]
    public async Task English_ContainsNotSure_ReturnsInvalid()
    {
        var result = await _service.DetectHallucinationsAsync(
            "I'm not sure about the setup rules.", "en", TestCancellationToken);

        result.IsValid.Should().BeFalse();
        result.DetectedKeywords.Should().Contain("I'm not sure");
    }

    // ========== Italian Tests (Primary language) ==========

    [Fact]
    public async Task Italian_NoHallucination_ReturnsValid()
    {
        var result = await _service.DetectHallucinationsAsync(
            "Il gioco supporta da 2 a 4 giocatori.", "it", TestCancellationToken);

        result.IsValid.Should().BeTrue();
        result.DetectedKeywords.Should().BeEmpty();
        result.Language.Should().Be("it");
    }

    [Fact]
    public async Task Italian_ContainsNonLoSo_ReturnsInvalid()
    {
        var result = await _service.DetectHallucinationsAsync(
            "Non lo so quanti giocatori possono giocare.", "it", TestCancellationToken);

        result.IsValid.Should().BeFalse();
        result.DetectedKeywords.Should().Contain("non lo so");
        result.Severity.Should().Be(HallucinationSeverity.High);
    }

    [Fact]
    public async Task Italian_ContainsPocoChiaro_ReturnsInvalid()
    {
        var result = await _service.DetectHallucinationsAsync(
            "La regola è poco chiaro nel manuale.", "it", TestCancellationToken);

        result.IsValid.Should().BeFalse();
        result.DetectedKeywords.Should().Contain("poco chiaro");
    }

    // ========== German Tests ==========

    [Fact]
    public async Task German_NoHallucination_ReturnsValid()
    {
        var result = await _service.DetectHallucinationsAsync(
            "Das Spiel unterstützt 2-4 Spieler.", "de", TestCancellationToken);

        result.IsValid.Should().BeTrue();
        result.Language.Should().Be("de");
    }

    [Fact]
    public async Task German_ContainsIchWeissNicht_ReturnsInvalid()
    {
        var result = await _service.DetectHallucinationsAsync(
            "Ich weiß nicht wie viele Spieler.", "de", TestCancellationToken);

        result.IsValid.Should().BeFalse();
        result.DetectedKeywords.Should().Contain("Ich weiß nicht");
    }

    // ========== French Tests ==========

    [Fact]
    public async Task French_NoHallucination_ReturnsValid()
    {
        var result = await _service.DetectHallucinationsAsync(
            "Le jeu supporte 2-4 joueurs.", "fr", TestCancellationToken);

        result.IsValid.Should().BeTrue();
        result.Language.Should().Be("fr");
    }

    [Fact]
    public async Task French_ContainsJeNeSaisPas_ReturnsInvalid()
    {
        var result = await _service.DetectHallucinationsAsync(
            "Je ne sais pas combien de joueurs.", "fr", TestCancellationToken);

        result.IsValid.Should().BeFalse();
        result.DetectedKeywords.Should().Contain("Je ne sais pas");
    }

    // ========== Spanish Tests ==========

    [Fact]
    public async Task Spanish_NoHallucination_ReturnsValid()
    {
        var result = await _service.DetectHallucinationsAsync(
            "El juego admite de 2 a 4 jugadores.", "es", TestCancellationToken);

        result.IsValid.Should().BeTrue();
        result.Language.Should().Be("es");
    }

    [Fact]
    public async Task Spanish_ContainsNoLoSe_ReturnsInvalid()
    {
        var result = await _service.DetectHallucinationsAsync(
            "No lo sé cuántos jugadores pueden jugar.", "es", TestCancellationToken);

        result.IsValid.Should().BeFalse();
        result.DetectedKeywords.Should().Contain("No lo sé");
    }

    // ========== Edge Cases ==========

    [Fact]
    public async Task EmptyText_ReturnsValid()
    {
        var result = await _service.DetectHallucinationsAsync("", "en", TestCancellationToken);

        result.IsValid.Should().BeTrue();
        result.DetectedKeywords.Should().BeEmpty();
    }

    [Fact]
    public async Task NullLanguage_DefaultsToEnglish()
    {
        var result = await _service.DetectHallucinationsAsync(
            "Valid response", language: null, TestCancellationToken);

        result.Language.Should().Be("en");
    }

    [Fact]
    public async Task MultipleKeywords_CalculatesSeverity()
    {
        var text = "I'm not sure, unclear, and possibly incorrect.";
        var result = await _service.DetectHallucinationsAsync(text, "en", TestCancellationToken);

        result.IsValid.Should().BeFalse();
        result.DetectedKeywords.Count.Should().Be(3);
        result.Severity.Should().Be(HallucinationSeverity.Medium);
    }

    [Fact]
    public async Task CaseInsensitive_DetectsKeywords()
    {
        var result = await _service.DetectHallucinationsAsync(
            "I DON'T KNOW the answer.", "en", TestCancellationToken);

        result.IsValid.Should().BeFalse();
        result.DetectedKeywords.Should().ContainSingle();
    }

    [Fact]
    public async Task GetKeywordCount_ReturnsCorrectCount()
    {
        var enCount = _service.GetForbiddenKeywordCount("en");
        var itCount = _service.GetForbiddenKeywordCount("it");

        (enCount >= 10).Should().BeTrue();
        (itCount >= 10).Should().BeTrue();
    }

    // ========== Additional Comprehensive Tests (BGAI-031) ==========

    [Fact]
    public async Task Portuguese_UnsupportedLanguage_FallbacksToEnglish()
    {
        // Arrange - Portuguese is not supported, should fallback to English
        var result = await _service.DetectHallucinationsAsync(
            "I don't know the rules.", "pt", TestCancellationToken);

        // Assert - Falls back to English
        result.Language.Should().Be("en");
        result.IsValid.Should().BeFalse();
        result.DetectedKeywords.Should().Contain("I don't know");
    }

    [Fact]
    public async Task Severity_ExactlyOneKeyword_ReturnsLow()
    {
        // Arrange - Exactly 1 keyword
        var result = await _service.DetectHallucinationsAsync(
            "The setup is unclear.", "en", TestCancellationToken);

        // Assert
        result.IsValid.Should().BeFalse();
        result.DetectedKeywords.Should().ContainSingle();
        result.Severity.Should().Be(HallucinationSeverity.Low);
    }

    [Fact]
    public async Task Severity_ExactlyThreeKeywords_ReturnsMedium()
    {
        // Arrange - Exactly 3 keywords
        var text = "I'm not sure, unclear, and ambiguous about the rules.";
        var result = await _service.DetectHallucinationsAsync(text, "en", TestCancellationToken);

        // Assert
        result.IsValid.Should().BeFalse();
        result.DetectedKeywords.Count.Should().Be(3);
        result.Severity.Should().Be(HallucinationSeverity.Medium);
    }

    [Fact]
    public async Task Severity_ExactlyFiveKeywords_ReturnsHigh()
    {
        // Arrange - Exactly 5 keywords
        var text = "I'm not sure, unclear, ambiguous, possibly incorrect, and perhaps wrong.";
        var result = await _service.DetectHallucinationsAsync(text, "en", TestCancellationToken);

        // Assert
        result.IsValid.Should().BeFalse();
        result.DetectedKeywords.Count.Should().Be(5);
        result.Severity.Should().Be(HallucinationSeverity.High);
    }

    [Fact]
    public async Task PartialMatch_DoesNotTrigger()
    {
        // Arrange - "known" contains "know" but should not trigger "I don't know"
        var text = "The well-known rules are clear.";
        var result = await _service.DetectHallucinationsAsync(text, "en", TestCancellationToken);

        // Assert
        result.IsValid.Should().BeTrue();
        result.DetectedKeywords.Should().BeEmpty();
    }

    [Fact]
    public async Task AllSupportedLanguages_GetKeywordCount()
    {
        // Arrange & Act
        var languages = new[] { "en", "it", "de", "fr", "es" };
        var counts = languages.Select(lang => _service.GetForbiddenKeywordCount(lang)).ToList();

        // Assert - All languages have keywords defined
        counts.Should().AllSatisfy(count => (count > 0).Should().BeTrue());
    }

    [Fact]
    public async Task VeryLongText_DetectsKeywords()
    {
        // Arrange - Long text with keyword buried inside
        var longText = string.Join(" ", Enumerable.Repeat("Valid game rule text.", 100))
            + " I don't know the answer. "
            + string.Join(" ", Enumerable.Repeat("More valid text.", 100));

        // Act
        var result = await _service.DetectHallucinationsAsync(longText, "en", TestCancellationToken);

        // Assert
        result.IsValid.Should().BeFalse();
        result.DetectedKeywords.Should().Contain("I don't know");
    }

    [Fact]
    public async Task Italian_MultipleKeywords_DetectsAll()
    {
        // Arrange - Multiple Italian keywords
        var text = "Non lo so e non sono sicuro della regola. È poco chiaro.";
        var result = await _service.DetectHallucinationsAsync(text, "it", TestCancellationToken);

        // Assert
        result.IsValid.Should().BeFalse();
        result.DetectedKeywords.Count.Should().Be(3);
        result.DetectedKeywords.Should().Contain("non lo so");
        result.DetectedKeywords.Should().Contain("non sono sicuro");
        result.DetectedKeywords.Should().Contain("poco chiaro");
    }

    [Fact]
    public async Task German_CaseSensitivity_DetectsUpperAndLower()
    {
        // Arrange - Test case insensitivity for German
        var text1 = "ich weiß nicht wie viele."; // lowercase
        var text2 = "ICH WEISS NICHT WIE VIELE."; // uppercase (ß -> ss in uppercase)

        // Act
        var result1 = await _service.DetectHallucinationsAsync(text1, "de", TestCancellationToken);
        var result2 = await _service.DetectHallucinationsAsync(text2, "de", TestCancellationToken);

        // Assert - Both should detect
        result1.IsValid.Should().BeFalse();
        result2.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task French_GenderVariations_DetectsBoth()
    {
        // Arrange - French has gendered "sûr/sûre"
        var text1 = "Je ne suis pas sûr.";
        var text2 = "Je ne suis pas sûre.";

        // Act
        var result1 = await _service.DetectHallucinationsAsync(text1, "fr", TestCancellationToken);
        var result2 = await _service.DetectHallucinationsAsync(text2, "fr", TestCancellationToken);

        // Assert - Both variations should be detected
        result1.IsValid.Should().BeFalse();
        result2.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Spanish_AccentVariations_Detects()
    {
        // Arrange
        var text = "No lo sé cuántos jugadores.";
        var result = await _service.DetectHallucinationsAsync(text, "es", TestCancellationToken);

        // Assert
        result.IsValid.Should().BeFalse();
        result.DetectedKeywords.Should().Contain("No lo sé");
    }

    [Fact]
    public async Task MessageFormat_ReflectsKeywordCount()
    {
        // Arrange - Test message formatting
        var text = "I'm not sure and unclear.";
        var result = await _service.DetectHallucinationsAsync(text, "en", TestCancellationToken);

        // Assert
        result.Message.Should().ContainEquivalentOf("2 hallucination indicator(s) detected");
        result.Message.Should().ContainEquivalentOf("en");
    }

    [Fact]
    public async Task TotalKeywordsChecked_ReflectsLanguage()
    {
        // Arrange & Act
        var resultEn = await _service.DetectHallucinationsAsync("Valid text.", "en", TestCancellationToken);
        var resultIt = await _service.DetectHallucinationsAsync("Testo valido.", "it", TestCancellationToken);

        // Assert - Different languages may have different keyword counts
        (resultEn.TotalKeywordsChecked > 0).Should().BeTrue();
        (resultIt.TotalKeywordsChecked > 0).Should().BeTrue();
    }

    [Fact]
    public async Task CriticalPhrases_AlwaysReturnHigh()
    {
        // Arrange - Test critical phrases that force High severity
        var criticalTexts = new[]
        {
            "I don't know the answer.",
            "I cannot find the information.",
            "Non lo so quanti giocatori.",
            "Je ne sais pas combien.",
            "Ich weiß nicht wie viele."
        };

        foreach (var text in criticalTexts)
        {
            // Act
            var result = await _service.DetectHallucinationsAsync(text, language: null, TestCancellationToken);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Severity.Should().Be(HallucinationSeverity.High);
        }
    }

    [Fact]
    public async Task NullText_ThrowsOrReturnsValid()
    {
        // Arrange & Act
        var result = await _service.DetectHallucinationsAsync(null!, "en", TestCancellationToken);

        // Assert - Null treated as empty (implementation detail)
        result.IsValid.Should().BeTrue();
        result.DetectedKeywords.Should().BeEmpty();
    }

    [Fact]
    public async Task WhitespaceOnlyText_ReturnsValid()
    {
        // Arrange
        var result = await _service.DetectHallucinationsAsync("   \t\n   ", "en", TestCancellationToken);

        // Assert
        result.IsValid.Should().BeTrue();
        result.DetectedKeywords.Should().BeEmpty();
    }

    [Fact]
    public async Task MultipleInvocations_ReturnsConsistentResults()
    {
        // Arrange
        var text = "I'm not sure about this.";

        // Act - Call multiple times
        var result1 = await _service.DetectHallucinationsAsync(text, "en", TestCancellationToken);
        var result2 = await _service.DetectHallucinationsAsync(text, "en", TestCancellationToken);
        var result3 = await _service.DetectHallucinationsAsync(text, "en", TestCancellationToken);

        // Assert - All results identical
        result1.IsValid.Should().BeFalse();
        result2.IsValid.Should().BeFalse();
        result3.IsValid.Should().BeFalse();
        result2.DetectedKeywords.Count.Should().Be(result1.DetectedKeywords.Count);
        result3.DetectedKeywords.Count.Should().Be(result2.DetectedKeywords.Count);
        result2.Severity.Should().Be(result1.Severity);
    }

    [Theory]
    [InlineData("en", "I don't know")]
    [InlineData("it", "non lo so")]
    [InlineData("de", "Ich weiß nicht")]
    [InlineData("fr", "Je ne sais pas")]
    [InlineData("es", "No lo sé")]
    public async Task PrimaryKeywords_AllLanguages_Detected(string language, string keyword)
    {
        // Arrange
        var text = $"The rule says: {keyword} how many players.";

        // Act
        var result = await _service.DetectHallucinationsAsync(text, language, TestCancellationToken);

        // Assert
        result.IsValid.Should().BeFalse();
        result.DetectedKeywords.Should().Contain(keyword);
        result.Severity.Should().Be(HallucinationSeverity.High);
    }
}

