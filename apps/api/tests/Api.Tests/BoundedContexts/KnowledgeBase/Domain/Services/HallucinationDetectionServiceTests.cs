using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Unit tests for HallucinationDetectionService
/// ISSUE-972: BGAI-030 - Hallucination detection (forbidden keywords)
/// </summary>
public class HallucinationDetectionServiceTests
{
    private readonly HallucinationDetectionService _service;

    public HallucinationDetectionServiceTests()
    {
        var mockLogger = new Mock<ILogger<HallucinationDetectionService>>();
        _service = new HallucinationDetectionService(mockLogger.Object);
    }

    // ========== English Tests ==========

    [Fact]
    public async Task Test01_English_NoHallucination_ReturnsValid()
    {
        var result = await _service.DetectHallucinationsAsync(
            "The game supports 2-4 players.", "en");

        Assert.True(result.IsValid);
        Assert.Empty(result.DetectedKeywords);
        Assert.Equal(HallucinationSeverity.None, result.Severity);
        Assert.Equal("en", result.Language);
    }

    [Fact]
    public async Task Test02_English_ContainsDontKnow_ReturnsInvalid()
    {
        var result = await _service.DetectHallucinationsAsync(
            "I don't know how many players can play.", "en");

        Assert.False(result.IsValid);
        Assert.Contains("I don't know", result.DetectedKeywords);
        Assert.Equal(HallucinationSeverity.High, result.Severity);
    }

    [Fact]
    public async Task Test03_English_ContainsNotSure_ReturnsInvalid()
    {
        var result = await _service.DetectHallucinationsAsync(
            "I'm not sure about the setup rules.", "en");

        Assert.False(result.IsValid);
        Assert.Contains("I'm not sure", result.DetectedKeywords);
    }

    // ========== Italian Tests (Primary language) ==========

    [Fact]
    public async Task Test04_Italian_NoHallucination_ReturnsValid()
    {
        var result = await _service.DetectHallucinationsAsync(
            "Il gioco supporta da 2 a 4 giocatori.", "it");

        Assert.True(result.IsValid);
        Assert.Empty(result.DetectedKeywords);
        Assert.Equal("it", result.Language);
    }

    [Fact]
    public async Task Test05_Italian_ContainsNonLoSo_ReturnsInvalid()
    {
        var result = await _service.DetectHallucinationsAsync(
            "Non lo so quanti giocatori possono giocare.", "it");

        Assert.False(result.IsValid);
        Assert.Contains("non lo so", result.DetectedKeywords);
        Assert.Equal(HallucinationSeverity.High, result.Severity);
    }

    [Fact]
    public async Task Test06_Italian_ContainsPocoChiaro_ReturnsInvalid()
    {
        var result = await _service.DetectHallucinationsAsync(
            "La regola è poco chiaro nel manuale.", "it");

        Assert.False(result.IsValid);
        Assert.Contains("poco chiaro", result.DetectedKeywords);
    }

    // ========== German Tests ==========

    [Fact]
    public async Task Test07_German_NoHallucination_ReturnsValid()
    {
        var result = await _service.DetectHallucinationsAsync(
            "Das Spiel unterstützt 2-4 Spieler.", "de");

        Assert.True(result.IsValid);
        Assert.Equal("de", result.Language);
    }

    [Fact]
    public async Task Test08_German_ContainsIchWeissNicht_ReturnsInvalid()
    {
        var result = await _service.DetectHallucinationsAsync(
            "Ich weiß nicht wie viele Spieler.", "de");

        Assert.False(result.IsValid);
        Assert.Contains("Ich weiß nicht", result.DetectedKeywords);
    }

    // ========== French Tests ==========

    [Fact]
    public async Task Test09_French_NoHallucination_ReturnsValid()
    {
        var result = await _service.DetectHallucinationsAsync(
            "Le jeu supporte 2-4 joueurs.", "fr");

        Assert.True(result.IsValid);
        Assert.Equal("fr", result.Language);
    }

    [Fact]
    public async Task Test10_French_ContainsJeNeSaisPas_ReturnsInvalid()
    {
        var result = await _service.DetectHallucinationsAsync(
            "Je ne sais pas combien de joueurs.", "fr");

        Assert.False(result.IsValid);
        Assert.Contains("Je ne sais pas", result.DetectedKeywords);
    }

    // ========== Spanish Tests ==========

    [Fact]
    public async Task Test11_Spanish_NoHallucination_ReturnsValid()
    {
        var result = await _service.DetectHallucinationsAsync(
            "El juego admite de 2 a 4 jugadores.", "es");

        Assert.True(result.IsValid);
        Assert.Equal("es", result.Language);
    }

    [Fact]
    public async Task Test12_Spanish_ContainsNoLoSe_ReturnsInvalid()
    {
        var result = await _service.DetectHallucinationsAsync(
            "No lo sé cuántos jugadores pueden jugar.", "es");

        Assert.False(result.IsValid);
        Assert.Contains("No lo sé", result.DetectedKeywords);
    }

    // ========== Edge Cases ==========

    [Fact]
    public async Task Test13_EmptyText_ReturnsValid()
    {
        var result = await _service.DetectHallucinationsAsync("", "en");

        Assert.True(result.IsValid);
        Assert.Empty(result.DetectedKeywords);
    }

    [Fact]
    public async Task Test14_NullLanguage_DefaultsToEnglish()
    {
        var result = await _service.DetectHallucinationsAsync(
            "Valid response", language: null);

        Assert.Equal("en", result.Language);
    }

    [Fact]
    public async Task Test15_MultipleKeywords_CalculatesSeverity()
    {
        var text = "I'm not sure, unclear, and possibly incorrect.";
        var result = await _service.DetectHallucinationsAsync(text, "en");

        Assert.False(result.IsValid);
        Assert.Equal(3, result.DetectedKeywords.Count);
        Assert.Equal(HallucinationSeverity.Medium, result.Severity);
    }

    [Fact]
    public async Task Test16_CaseInsensitive_DetectsKeywords()
    {
        var result = await _service.DetectHallucinationsAsync(
            "I DON'T KNOW the answer.", "en");

        Assert.False(result.IsValid);
        Assert.Single(result.DetectedKeywords);
    }

    [Fact]
    public async Task Test17_GetKeywordCount_ReturnsCorrectCount()
    {
        var enCount = _service.GetForbiddenKeywordCount("en");
        var itCount = _service.GetForbiddenKeywordCount("it");

        Assert.True(enCount >= 10);
        Assert.True(itCount >= 10);
    }
}
