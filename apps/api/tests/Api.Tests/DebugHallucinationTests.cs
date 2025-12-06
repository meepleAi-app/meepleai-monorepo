using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Debug tests for hallucination detection issues
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class DebugHallucinationTests
{
    private readonly HallucinationDetectionService _service;

    public DebugHallucinationTests()
    {
        var mockLogger = new Mock<ILogger<HallucinationDetectionService>>();
        _service = new HallucinationDetectionService(mockLogger.Object);
    }

    [Fact]
    public async Task Debug_GermanCaseSensitivity()
    {
        // Test case from failing test - FIXED with ß/SS normalization
        var text1 = "ich weiß nicht wie viele."; // lowercase
        var text2 = "ICH WEISS NICHT WIE VIELE."; // uppercase (ß -> ss in uppercase)

        var result1 = await _service.DetectHallucinationsAsync(text1, "de", TestContext.Current.CancellationToken);
        var result2 = await _service.DetectHallucinationsAsync(text2, "de", TestContext.Current.CancellationToken);

        // Assert: Both should detect (fixed with ß/SS normalization)
        Assert.False(result1.IsValid, "Lowercase German text should be detected");
        Assert.False(result2.IsValid, "Uppercase German text should be detected");
    }

    [Fact]
    public async Task Debug_CriticalPhrases()
    {
        // FIXED: Multi-language critical phrase detection
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
            var result = await _service.DetectHallucinationsAsync(text, language: null, TestContext.Current.CancellationToken);

            // Assert: All critical phrases should be detected as invalid with High severity
            Assert.False(result.IsValid, $"Critical phrase '{text}' should be detected");
            Assert.Equal(HallucinationSeverity.High, result.Severity);
        }
    }
}

