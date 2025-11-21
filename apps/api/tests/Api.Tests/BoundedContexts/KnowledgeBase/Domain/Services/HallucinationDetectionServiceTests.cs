using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Microsoft.Extensions.Logging;
using Moq;
using System.Linq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Unit tests for HallucinationDetectionService
/// ISSUE-972: BGAI-030 - Hallucination detection (forbidden keywords)
/// </summary>
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

        Assert.True(result.IsValid);
        Assert.Empty(result.DetectedKeywords);
        Assert.Equal(HallucinationSeverity.None, result.Severity);
        Assert.Equal("en", result.Language);
    }

    [Fact]
    public async Task English_ContainsDontKnow_ReturnsInvalid()
    {
        var result = await _service.DetectHallucinationsAsync(
            "I don't know how many players can play.", "en", TestCancellationToken);

        Assert.False(result.IsValid);
        Assert.Contains("I don't know", result.DetectedKeywords);
        Assert.Equal(HallucinationSeverity.High, result.Severity);
    }

    [Fact]
    public async Task English_ContainsNotSure_ReturnsInvalid()
    {
        var result = await _service.DetectHallucinationsAsync(
            "I'm not sure about the setup rules.", "en", TestCancellationToken);

        Assert.False(result.IsValid);
        Assert.Contains("I'm not sure", result.DetectedKeywords);
    }

    // ========== Italian Tests (Primary language) ==========

    [Fact]
    public async Task Italian_NoHallucination_ReturnsValid()
    {
        var result = await _service.DetectHallucinationsAsync(
            "Il gioco supporta da 2 a 4 giocatori.", "it", TestCancellationToken);

        Assert.True(result.IsValid);
        Assert.Empty(result.DetectedKeywords);
        Assert.Equal("it", result.Language);
    }

    [Fact]
    public async Task Italian_ContainsNonLoSo_ReturnsInvalid()
    {
        var result = await _service.DetectHallucinationsAsync(
            "Non lo so quanti giocatori possono giocare.", "it", TestCancellationToken);

        Assert.False(result.IsValid);
        Assert.Contains("non lo so", result.DetectedKeywords);
        Assert.Equal(HallucinationSeverity.High, result.Severity);
    }

    [Fact]
    public async Task Italian_ContainsPocoChiaro_ReturnsInvalid()
    {
        var result = await _service.DetectHallucinationsAsync(
            "La regola è poco chiaro nel manuale.", "it", TestCancellationToken);

        Assert.False(result.IsValid);
        Assert.Contains("poco chiaro", result.DetectedKeywords);
    }

    // ========== German Tests ==========

    [Fact]
    public async Task German_NoHallucination_ReturnsValid()
    {
        var result = await _service.DetectHallucinationsAsync(
            "Das Spiel unterstützt 2-4 Spieler.", "de", TestCancellationToken);

        Assert.True(result.IsValid);
        Assert.Equal("de", result.Language);
    }

    [Fact]
    public async Task German_ContainsIchWeissNicht_ReturnsInvalid()
    {
        var result = await _service.DetectHallucinationsAsync(
            "Ich weiß nicht wie viele Spieler.", "de", TestCancellationToken);

        Assert.False(result.IsValid);
        Assert.Contains("Ich weiß nicht", result.DetectedKeywords);
    }

    // ========== French Tests ==========

    [Fact]
    public async Task French_NoHallucination_ReturnsValid()
    {
        var result = await _service.DetectHallucinationsAsync(
            "Le jeu supporte 2-4 joueurs.", "fr", TestCancellationToken);

        Assert.True(result.IsValid);
        Assert.Equal("fr", result.Language);
    }

    [Fact]
    public async Task French_ContainsJeNeSaisPas_ReturnsInvalid()
    {
        var result = await _service.DetectHallucinationsAsync(
            "Je ne sais pas combien de joueurs.", "fr", TestCancellationToken);

        Assert.False(result.IsValid);
        Assert.Contains("Je ne sais pas", result.DetectedKeywords);
    }

    // ========== Spanish Tests ==========

    [Fact]
    public async Task Spanish_NoHallucination_ReturnsValid()
    {
        var result = await _service.DetectHallucinationsAsync(
            "El juego admite de 2 a 4 jugadores.", "es", TestCancellationToken);

        Assert.True(result.IsValid);
        Assert.Equal("es", result.Language);
    }

    [Fact]
    public async Task Spanish_ContainsNoLoSe_ReturnsInvalid()
    {
        var result = await _service.DetectHallucinationsAsync(
            "No lo sé cuántos jugadores pueden jugar.", "es", TestCancellationToken);

        Assert.False(result.IsValid);
        Assert.Contains("No lo sé", result.DetectedKeywords);
    }

    // ========== Edge Cases ==========

    [Fact]
    public async Task EmptyText_ReturnsValid()
    {
        var result = await _service.DetectHallucinationsAsync("", "en", TestCancellationToken);

        Assert.True(result.IsValid);
        Assert.Empty(result.DetectedKeywords);
    }

    [Fact]
    public async Task NullLanguage_DefaultsToEnglish()
    {
        var result = await _service.DetectHallucinationsAsync(
            "Valid response", language: null, TestCancellationToken);

        Assert.Equal("en", result.Language);
    }

    [Fact]
    public async Task MultipleKeywords_CalculatesSeverity()
    {
        var text = "I'm not sure, unclear, and possibly incorrect.";
        var result = await _service.DetectHallucinationsAsync(text, "en", TestCancellationToken);

        Assert.False(result.IsValid);
        Assert.Equal(3, result.DetectedKeywords.Count);
        Assert.Equal(HallucinationSeverity.Medium, result.Severity);
    }

    [Fact]
    public async Task CaseInsensitive_DetectsKeywords()
    {
        var result = await _service.DetectHallucinationsAsync(
            "I DON'T KNOW the answer.", "en", TestCancellationToken);

        Assert.False(result.IsValid);
        Assert.Single(result.DetectedKeywords);
    }

    [Fact]
    public async Task GetKeywordCount_ReturnsCorrectCount()
    {
        var enCount = _service.GetForbiddenKeywordCount("en");
        var itCount = _service.GetForbiddenKeywordCount("it");

        Assert.True(enCount >= 10);
        Assert.True(itCount >= 10);
    }

    // ========== Additional Comprehensive Tests (BGAI-031) ==========

    [Fact]
    public async Task Portuguese_UnsupportedLanguage_FallbacksToEnglish()
    {
        // Arrange - Portuguese is not supported, should fallback to English
        var result = await _service.DetectHallucinationsAsync(
            "I don't know the rules.", "pt", TestCancellationToken);

        // Assert - Falls back to English
        Assert.Equal("en", result.Language);
        Assert.False(result.IsValid);
        Assert.Contains("I don't know", result.DetectedKeywords);
    }

    [Fact]
    public async Task Severity_ExactlyOneKeyword_ReturnsLow()
    {
        // Arrange - Exactly 1 keyword
        var result = await _service.DetectHallucinationsAsync(
            "The setup is unclear.", "en", TestCancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.DetectedKeywords);
        Assert.Equal(HallucinationSeverity.Low, result.Severity);
    }

    [Fact]
    public async Task Severity_ExactlyThreeKeywords_ReturnsMedium()
    {
        // Arrange - Exactly 3 keywords
        var text = "I'm not sure, unclear, and ambiguous about the rules.";
        var result = await _service.DetectHallucinationsAsync(text, "en", TestCancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(3, result.DetectedKeywords.Count);
        Assert.Equal(HallucinationSeverity.Medium, result.Severity);
    }

    [Fact]
    public async Task Severity_ExactlyFiveKeywords_ReturnsHigh()
    {
        // Arrange - Exactly 5 keywords
        var text = "I'm not sure, unclear, ambiguous, possibly incorrect, and perhaps wrong.";
        var result = await _service.DetectHallucinationsAsync(text, "en", TestCancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(5, result.DetectedKeywords.Count);
        Assert.Equal(HallucinationSeverity.High, result.Severity);
    }

    [Fact]
    public async Task PartialMatch_DoesNotTrigger()
    {
        // Arrange - "known" contains "know" but should not trigger "I don't know"
        var text = "The well-known rules are clear.";
        var result = await _service.DetectHallucinationsAsync(text, "en", TestCancellationToken);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.DetectedKeywords);
    }

    [Fact]
    public async Task AllSupportedLanguages_GetKeywordCount()
    {
        // Arrange & Act
        var languages = new[] { "en", "it", "de", "fr", "es" };
        var counts = languages.Select(lang => _service.GetForbiddenKeywordCount(lang)).ToList();

        // Assert - All languages have keywords defined
        Assert.All(counts, count => Assert.True(count > 0));
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
        Assert.False(result.IsValid);
        Assert.Contains("I don't know", result.DetectedKeywords);
    }

    [Fact]
    public async Task Italian_MultipleKeywords_DetectsAll()
    {
        // Arrange - Multiple Italian keywords
        var text = "Non lo so e non sono sicuro della regola. È poco chiaro.";
        var result = await _service.DetectHallucinationsAsync(text, "it", TestCancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(3, result.DetectedKeywords.Count);
        Assert.Contains("non lo so", result.DetectedKeywords);
        Assert.Contains("non sono sicuro", result.DetectedKeywords);
        Assert.Contains("poco chiaro", result.DetectedKeywords);
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
        Assert.False(result1.IsValid);
        Assert.False(result2.IsValid);
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
        Assert.False(result1.IsValid);
        Assert.False(result2.IsValid);
    }

    [Fact]
    public async Task Spanish_AccentVariations_Detects()
    {
        // Arrange
        var text = "No lo sé cuántos jugadores.";
        var result = await _service.DetectHallucinationsAsync(text, "es", TestCancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("No lo sé", result.DetectedKeywords);
    }

    [Fact]
    public async Task MessageFormat_ReflectsKeywordCount()
    {
        // Arrange - Test message formatting
        var text = "I'm not sure and unclear.";
        var result = await _service.DetectHallucinationsAsync(text, "en", TestCancellationToken);

        // Assert
        Assert.Contains("2 hallucination indicator(s) detected", result.Message);
        Assert.Contains("en", result.Message);
    }

    [Fact]
    public async Task TotalKeywordsChecked_ReflectsLanguage()
    {
        // Arrange & Act
        var resultEn = await _service.DetectHallucinationsAsync("Valid text.", "en", TestCancellationToken);
        var resultIt = await _service.DetectHallucinationsAsync("Testo valido.", "it", TestCancellationToken);

        // Assert - Different languages may have different keyword counts
        Assert.True(resultEn.TotalKeywordsChecked > 0);
        Assert.True(resultIt.TotalKeywordsChecked > 0);
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
            Assert.False(result.IsValid);
            Assert.Equal(HallucinationSeverity.High, result.Severity);
        }
    }

    [Fact]
    public async Task NullText_ThrowsOrReturnsValid()
    {
        // Arrange & Act
        var result = await _service.DetectHallucinationsAsync(null!, "en", TestCancellationToken);

        // Assert - Null treated as empty (implementation detail)
        Assert.True(result.IsValid);
        Assert.Empty(result.DetectedKeywords);
    }

    [Fact]
    public async Task WhitespaceOnlyText_ReturnsValid()
    {
        // Arrange
        var result = await _service.DetectHallucinationsAsync("   \t\n   ", "en", TestCancellationToken);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.DetectedKeywords);
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
        Assert.False(result1.IsValid);
        Assert.False(result2.IsValid);
        Assert.False(result3.IsValid);
        Assert.Equal(result1.DetectedKeywords.Count, result2.DetectedKeywords.Count);
        Assert.Equal(result2.DetectedKeywords.Count, result3.DetectedKeywords.Count);
        Assert.Equal(result1.Severity, result2.Severity);
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
        Assert.False(result.IsValid);
        Assert.Contains(keyword, result.DetectedKeywords, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(HallucinationSeverity.High, result.Severity);
    }
}
