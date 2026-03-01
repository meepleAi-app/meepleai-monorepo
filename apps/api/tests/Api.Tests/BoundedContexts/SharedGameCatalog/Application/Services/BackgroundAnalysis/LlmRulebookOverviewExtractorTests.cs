using Api.BoundedContexts.SharedGameCatalog.Application.Configuration;
using Api.BoundedContexts.SharedGameCatalog.Application.Services.BackgroundAnalysis;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using LlmOverviewResponse = Api.BoundedContexts.SharedGameCatalog.Application.Services.BackgroundAnalysis.LlmRulebookOverviewExtractor.LlmOverviewResponse;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Services.BackgroundAnalysis;

/// <summary>
/// Unit tests for LlmRulebookOverviewExtractor service.
/// Issue #2525: Background Rulebook Analysis Tests
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class LlmRulebookOverviewExtractorTests
{
    private readonly Mock<ILlmService> _mockLlmService;
    private readonly Mock<ILogger<LlmRulebookOverviewExtractor>> _mockLogger;
    private readonly BackgroundAnalysisOptions _options;
    private readonly LlmRulebookOverviewExtractor _service;

    public LlmRulebookOverviewExtractorTests()
    {
        _mockLlmService = new Mock<ILlmService>();
        _mockLogger = new Mock<ILogger<LlmRulebookOverviewExtractor>>();

        // Use default options for testing
        _options = new BackgroundAnalysisOptions
        {
            OverviewSampleBeginning = 10000,
            OverviewSampleMiddle = 5000,
            OverviewSampleEnd = 2000
        };

        _service = new LlmRulebookOverviewExtractor(
            _mockLlmService.Object,
            Options.Create(_options),
            _mockLogger.Object);
    }

    #region ExtractOverviewAsync - Happy Path

    [Fact]
    public async Task ExtractOverviewAsync_WithValidRulebook_ReturnsSuccessfulOverview()
    {
        // Arrange
        var gameName = "Catan";
        var rulebookContent = CreateTestRulebook(15000);

        var llmResponse = new LlmOverviewResponse
        {
            GameTitle = "Catan",
            GameSummary = "Resource management and trading game",
            MainMechanics = ["Dice Rolling", "Trading", "Resource Management"],
            VictoryConditionSummary = "First to 10 victory points wins",
            PlayerCountMin = 3,
            PlayerCountMax = 4,
            PlaytimeMinutes = 90,
            SectionHeaders = ["Setup", "Gameplay", "Trading", "Victory"]
        };

        _mockLlmService
            .Setup(x => x.GenerateJsonAsync<LlmOverviewResponse>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmOverviewResponse?)llmResponse);

        // Act
        var result = await _service.ExtractOverviewAsync(gameName, rulebookContent);

        // Assert
        result.Should().NotBeNull();
        result.GameTitle.Should().Be("Catan");
        result.GameSummary.Should().Be("Resource management and trading game");
        result.MainMechanics.Should().HaveCount(3);
        result.MainMechanics.Should().Contain("Dice Rolling");
        result.VictoryConditionSummary.Should().Be("First to 10 victory points wins");
        result.PlayerCountMin.Should().Be(3);
        result.PlayerCountMax.Should().Be(4);
        result.PlaytimeMinutes.Should().Be(90);
        result.SectionHeaders.Should().HaveCount(4);

        _mockLlmService.Verify(
            x => x.GenerateJsonAsync<LlmRulebookOverviewExtractor.LlmOverviewResponse>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region ExtractOverviewAsync - LLM Null Response Fallback

    [Fact]
    public async Task ExtractOverviewAsync_WhenLlmReturnsNull_ReturnsFallbackOverview()
    {
        // Arrange
        var gameName = "Gloomhaven";
        var rulebookContent = CreateTestRulebookWithHeaders(20000);

        _mockLlmService
            .Setup(x => x.GenerateJsonAsync<LlmOverviewResponse>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmOverviewResponse?)null);

        // Act
        var result = await _service.ExtractOverviewAsync(gameName, rulebookContent);

        // Assert
        result.Should().NotBeNull();
        result.GameTitle.Should().Be(gameName);
        result.GameSummary.Should().Be("Board game rulebook analysis in progress");
        result.MainMechanics.Should().Contain("Strategy");
        result.MainMechanics.Should().Contain("Turn-based");
        result.VictoryConditionSummary.Should().Be("See full analysis for victory conditions");
        result.PlayerCountMin.Should().Be(2);
        result.PlayerCountMax.Should().Be(6);
        result.PlaytimeMinutes.Should().Be(60);
        result.SectionHeaders.Should().NotBeEmpty(); // Extracted via regex
    }

    #endregion

    #region ExtractOverviewAsync - Exception Handling

    [Fact]
    public async Task ExtractOverviewAsync_WhenLlmThrowsException_ReturnsFallbackOverview()
    {
        // Arrange
        var gameName = "Wingspan";
        var rulebookContent = CreateTestRulebook(25000);

        _mockLlmService
            .Setup(x => x.GenerateJsonAsync<LlmOverviewResponse>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("LLM service unavailable"));

        // Act
        var result = await _service.ExtractOverviewAsync(gameName, rulebookContent);

        // Assert
        result.Should().NotBeNull();
        result.GameTitle.Should().Be(gameName);
        result.GameSummary.Should().Be("Board game rulebook analysis in progress");
        result.SectionHeaders.Should().NotBeEmpty(); // Fallback extracts headers
    }

    #endregion

    #region Sampling Logic Tests

    [Fact]
    public async Task ExtractOverviewAsync_WithSmallRulebook_DoesNotSampleContent()
    {
        // Arrange
        var gameName = "Love Letter";
        var rulebookContent = CreateTestRulebook(5000); // Less than beginning + end

        var llmResponse = CreateValidLlmResponse();
        _mockLlmService
            .Setup(x => x.GenerateJsonAsync<LlmOverviewResponse>(
                It.IsAny<string>(),
                It.Is<string>(s => s.Contains("[Beginning]") && !s.Contains("[Middle Section]")),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResponse);

        // Act
        var result = await _service.ExtractOverviewAsync(gameName, rulebookContent);

        // Assert - Full content passed (no sampling)
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ExtractOverviewAsync_WithLargeRulebook_SamplesBeginningMiddleEnd()
    {
        // Arrange
        var gameName = "Twilight Imperium";
        var rulebookContent = CreateTestRulebook(50000); // Large rulebook

        var llmResponse = CreateValidLlmResponse();

        string? capturedUserPrompt = null;
        _mockLlmService
            .Setup(x => x.GenerateJsonAsync<LlmOverviewResponse>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, RequestSource, CancellationToken>((_, user, _, _) => capturedUserPrompt = user)
            .ReturnsAsync(llmResponse);

        // Act
        await _service.ExtractOverviewAsync(gameName, rulebookContent);

        // Assert - Sampled content includes markers
        capturedUserPrompt.Should().NotBeNull();
        capturedUserPrompt.Should().Contain("[Beginning]");
        capturedUserPrompt.Should().Contain("[Middle Section]");
        capturedUserPrompt.Should().Contain("[End]");
    }

    #endregion

    #region Fallback Header Extraction Tests

    [Fact]
    public async Task ExtractOverviewAsync_FallbackWithMarkdownHeaders_ExtractsHeaders()
    {
        // Arrange
        var gameName = "Test Game";
        var rulebookContent = """
            # Setup Instructions
            Place the board.

            ## Player Setup
            Each player takes components.

            ### Resource Distribution
            Deal resources equally.

            # Gameplay
            Take turns clockwise.
            """;

        _mockLlmService
            .Setup(x => x.GenerateJsonAsync<LlmOverviewResponse>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmOverviewResponse?)null); // Force fallback

        // Act
        var result = await _service.ExtractOverviewAsync(gameName, rulebookContent);

        // Assert
        result.SectionHeaders.Should().Contain("# Setup Instructions");
        result.SectionHeaders.Should().Contain("## Player Setup");
        result.SectionHeaders.Should().Contain("### Resource Distribution");
        result.SectionHeaders.Should().Contain("# Gameplay");
    }

    [Fact]
    public async Task ExtractOverviewAsync_FallbackWithAllCapsHeaders_ExtractsHeaders()
    {
        // Arrange
        var gameName = "Test Game";
        var rulebookContent = """
            GAME SETUP
            Place all components.

            TURN STRUCTURE
            Each turn has 3 phases.

            VICTORY CONDITIONS
            First to 10 points wins.
            """;

        _mockLlmService
            .Setup(x => x.GenerateJsonAsync<LlmOverviewResponse>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmOverviewResponse?)null); // Force fallback

        // Act
        var result = await _service.ExtractOverviewAsync(gameName, rulebookContent);

        // Assert
        result.SectionHeaders.Should().Contain("GAME SETUP");
        result.SectionHeaders.Should().Contain("TURN STRUCTURE");
        result.SectionHeaders.Should().Contain("VICTORY CONDITIONS");
    }

    [Fact]
    public async Task ExtractOverviewAsync_FallbackWithNoHeaders_ReturnsDefaultHeaders()
    {
        // Arrange
        var gameName = "Simple Game";
        var rulebookContent = "Just plain text with no headers at all. Some gameplay description.";

        _mockLlmService
            .Setup(x => x.GenerateJsonAsync<LlmOverviewResponse>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmOverviewResponse?)null); // Force fallback

        // Act
        var result = await _service.ExtractOverviewAsync(gameName, rulebookContent);

        // Assert
        result.SectionHeaders.Should().HaveCount(3);
        result.SectionHeaders.Should().Contain("Setup");
        result.SectionHeaders.Should().Contain("Gameplay");
        result.SectionHeaders.Should().Contain("Ending");
    }

    [Fact]
    public async Task ExtractOverviewAsync_FallbackLimitsTo20Headers_WhenManyHeadersFound()
    {
        // Arrange
        var gameName = "Complex Game";
        var rulebookContent = string.Join("\n\n",
            Enumerable.Range(1, 50).Select(i => $"# Section {i}\nContent for section {i}."));

        _mockLlmService
            .Setup(x => x.GenerateJsonAsync<LlmOverviewResponse>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmOverviewResponse?)null); // Force fallback

        // Act
        var result = await _service.ExtractOverviewAsync(gameName, rulebookContent);

        // Assert
        result.SectionHeaders.Should().HaveCountLessThanOrEqualTo(20, "fallback limits to 20 headers");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task ExtractOverviewAsync_WithCancellationToken_PassesToLlmService()
    {
        // Arrange
        var gameName = "Test";
        var rulebookContent = CreateTestRulebook(10000);
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        var llmResponse = CreateValidLlmResponse();

        CancellationToken capturedToken = default;
        _mockLlmService
            .Setup(x => x.GenerateJsonAsync<LlmOverviewResponse>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, RequestSource, CancellationToken>((_, _, _, ct) => capturedToken = ct)
            .ReturnsAsync(llmResponse);

        // Act
        await _service.ExtractOverviewAsync(gameName, rulebookContent, cancellationToken);

        // Assert
        capturedToken.Should().Be(cancellationToken);
    }

    #endregion

    #region Empty Content Edge Cases

    [Fact]
    public async Task ExtractOverviewAsync_WithEmptyContent_ReturnsFallbackOverview()
    {
        // Arrange
        var gameName = "Empty Game";
        var rulebookContent = string.Empty;

        _mockLlmService
            .Setup(x => x.GenerateJsonAsync<LlmOverviewResponse>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmOverviewResponse?)null); // Force fallback

        // Act
        var result = await _service.ExtractOverviewAsync(gameName, rulebookContent);

        // Assert
        result.Should().NotBeNull();
        result.GameTitle.Should().Be(gameName);
        result.GameSummary.Should().Be("Board game rulebook analysis in progress");
        result.SectionHeaders.Should().HaveCount(3);
        result.SectionHeaders.Should().Contain("Setup");
        result.SectionHeaders.Should().Contain("Gameplay");
        result.SectionHeaders.Should().Contain("Ending");
    }

    [Fact]
    public async Task ExtractOverviewAsync_WithWhitespaceOnly_ReturnsFallbackOverview()
    {
        // Arrange
        var gameName = "Whitespace Game";
        var rulebookContent = "   \n\t\r\n   ";

        _mockLlmService
            .Setup(x => x.GenerateJsonAsync<LlmOverviewResponse>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmOverviewResponse?)null);

        // Act
        var result = await _service.ExtractOverviewAsync(gameName, rulebookContent);

        // Assert
        result.Should().NotBeNull();
        result.GameTitle.Should().Be(gameName);
        result.SectionHeaders.Should().NotBeEmpty();
    }

    #endregion

    #region Beginning/Middle/End Sampling Validation

    [Fact]
    public async Task ExtractOverviewAsync_SamplingLogic_ExtractsCorrectBeginningChars()
    {
        // Arrange
        var gameName = "Test";
        var rulebookContent = new string('A', 10000) + new string('B', 20000) + new string('C', 10000);

        string? capturedPrompt = null;
        _mockLlmService
            .Setup(x => x.GenerateJsonAsync<LlmOverviewResponse>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, RequestSource, CancellationToken>((_, user, _, _) => capturedPrompt = user)
            .ReturnsAsync((LlmOverviewResponse?)CreateValidLlmResponse());

        // Act
        await _service.ExtractOverviewAsync(gameName, rulebookContent);

        // Assert - Beginning section should contain only 'A' chars (first 10k)
        capturedPrompt.Should().NotBeNull();
        var beginningSection = capturedPrompt!.Split("[Middle Section]")[0];
        beginningSection.Should().Contain("AAAA"); // Beginning chars
        beginningSection.Should().NotContain("BBBB"); // Not middle yet
    }

    [Fact]
    public async Task ExtractOverviewAsync_SamplingLogic_ExtractsCorrectEndChars()
    {
        // Arrange
        var gameName = "Test";
        var rulebookContent = new string('A', 10000) + new string('B', 20000) + new string('C', 3000);

        string? capturedPrompt = null;
        _mockLlmService
            .Setup(x => x.GenerateJsonAsync<LlmOverviewResponse>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, RequestSource, CancellationToken>((_, user, _, _) => capturedPrompt = user)
            .ReturnsAsync((LlmOverviewResponse?)CreateValidLlmResponse());

        // Act
        await _service.ExtractOverviewAsync(gameName, rulebookContent);

        // Assert - End section should contain last 2k chars (C chars)
        capturedPrompt.Should().NotBeNull();
        var endSection = capturedPrompt!.Split("[End]")[1];
        endSection.Should().Contain("CCCC"); // End chars
        endSection.Length.Should().BeLessThanOrEqualTo(2500); // ~2000 + padding
    }

    [Fact]
    public async Task ExtractOverviewAsync_SamplingLogic_ExtractsCorrectMiddleChars()
    {
        // Arrange
        var gameName = "Test";
        var rulebookContent = new string('A', 15000) + new string('M', 10000) + new string('Z', 15000);

        string? capturedPrompt = null;
        _mockLlmService
            .Setup(x => x.GenerateJsonAsync<LlmOverviewResponse>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, RequestSource, CancellationToken>((_, user, _, _) => capturedPrompt = user)
            .ReturnsAsync((LlmOverviewResponse?)CreateValidLlmResponse());

        // Act
        await _service.ExtractOverviewAsync(gameName, rulebookContent);

        // Assert - Middle section should be centered
        capturedPrompt.Should().NotBeNull();
        var middleSection = capturedPrompt!
            .Split("[Middle Section]")[1]
            .Split("[End]")[0];
        middleSection.Should().Contain("MMMM"); // Middle chars
        middleSection.Length.Should().BeLessThanOrEqualTo(6000); // ~5000 + padding
    }

    #endregion

    #region Custom Configuration Tests

    [Fact]
    public async Task ExtractOverviewAsync_WithCustomOptions_UsesConfiguredSampleSizes()
    {
        // Arrange
        var customOptions = new BackgroundAnalysisOptions
        {
            OverviewSampleBeginning = 2000,
            OverviewSampleMiddle = 1000,
            OverviewSampleEnd = 500
        };

        var customService = new LlmRulebookOverviewExtractor(
            _mockLlmService.Object,
            Options.Create(customOptions),
            _mockLogger.Object);

        var gameName = "Custom Config Test";
        var rulebookContent = new string('X', 20000);

        string? capturedPrompt = null;
        _mockLlmService
            .Setup(x => x.GenerateJsonAsync<LlmOverviewResponse>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, RequestSource, CancellationToken>((_, user, _, _) => capturedPrompt = user)
            .ReturnsAsync((LlmOverviewResponse?)CreateValidLlmResponse());

        // Act
        await customService.ExtractOverviewAsync(gameName, rulebookContent);

        // Assert - Custom sample sizes applied
        capturedPrompt.Should().NotBeNull();
        capturedPrompt.Should().Contain("[Beginning]");
        capturedPrompt.Should().Contain("[Middle Section]");
        capturedPrompt.Should().Contain("[End]");

        // Verify reduced size (harder to check exact, but total should be much smaller)
        var totalSampledChars = capturedPrompt!.Count(c => c == 'X');
        totalSampledChars.Should().BeLessThan(5000); // 2000 + 1000 + 500 + overhead
    }

    #endregion

    #region Helper Methods

    private static string CreateTestRulebook(int chars)
    {
        return new string('A', chars);
    }

    private static string CreateTestRulebookWithHeaders(int totalChars)
    {
        var header1 = "# Game Overview\n";
        var header2 = "## Setup Instructions\n";
        var header3 = "### Victory Conditions\n";
        var headers = header1 + header2 + header3;
        var remainingChars = totalChars - headers.Length;

        return headers + new string('A', Math.Max(0, remainingChars));
    }

    private static LlmOverviewResponse CreateValidLlmResponse()
    {
        return new LlmOverviewResponse
        {
            GameTitle = "Test Game",
            GameSummary = "A test game for unit testing",
            MainMechanics = ["Strategy", "Dice"],
            VictoryConditionSummary = "Win by points",
            PlayerCountMin = 2,
            PlayerCountMax = 4,
            PlaytimeMinutes = 60,
            SectionHeaders = ["Setup", "Play"]
        };
    }

    #endregion
}
