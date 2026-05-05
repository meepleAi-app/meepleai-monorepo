using Api.Infrastructure.Translation;
using Api.Services;
using Api.Services.LlmClients;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Api.Tests.Infrastructure.Translation;

[Trait("Category", TestCategories.Unit)]
public class OpenRouterTranslationServiceTests
{
    private readonly ILlmService _llmService = Substitute.For<ILlmService>();

    [Fact]
    public async Task TranslateNarrativeAsync_GivenEnglishParagraph_ReturnsItalianTranslation()
    {
        // Arrange
        var svc = new OpenRouterTranslationService(_llmService, NullLogger<OpenRouterTranslationService>.Instance);
        _llmService.GenerateCompletionWithModelAsync(
                "anthropic/claude-sonnet-4-5",
                Arg.Any<string>(),
                Arg.Any<string>(),
                RequestSource.Translation,
                Arg.Any<int?>(),
                Arg.Any<CancellationToken>())
            .Returns(LlmCompletionResult.CreateSuccess("Sei arrivato al Villaggio di Avalon."));

        // Act
        var result = await svc.TranslateNarrativeAsync(
            "You have arrived at the Village of Avalon.",
            sourceLanguage: "en",
            targetLanguage: "it",
            gameContext: "Tainted Grail",
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TranslatedText.Should().Contain("Avalon");
        result.Success.Should().BeTrue();
        result.EstimatedCostUsd.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task TranslateNarrativeAsync_WhenLlmFails_ReturnsFailureResult()
    {
        // Arrange
        var svc = new OpenRouterTranslationService(_llmService, NullLogger<OpenRouterTranslationService>.Instance);
        _llmService.GenerateCompletionWithModelAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<RequestSource>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(LlmCompletionResult.CreateFailure("rate_limit_exceeded"));

        // Act
        var result = await svc.TranslateNarrativeAsync(
            "Some paragraph", "en", "it", null, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("LLM_FAILURE");
    }

    [Fact]
    public async Task TranslateGenericAsync_UsesLowerCostModel()
    {
        // Arrange
        var svc = new OpenRouterTranslationService(_llmService, NullLogger<OpenRouterTranslationService>.Instance);
        _llmService.GenerateCompletionWithModelAsync(
                "deepseek/deepseek-chat",
                Arg.Any<string>(),
                Arg.Any<string>(),
                RequestSource.Translation,
                Arg.Any<int?>(),
                Arg.Any<CancellationToken>())
            .Returns(LlmCompletionResult.CreateSuccess("Non disponibile nel manuale."));

        // Act
        var result = await svc.TranslateGenericAsync(
            "Not available in the rulebook.", "en", "it", CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        await _llmService.Received(1).GenerateCompletionWithModelAsync(
            "deepseek/deepseek-chat",
            Arg.Any<string>(),
            Arg.Any<string>(),
            RequestSource.Translation,
            Arg.Any<int?>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task TranslateNarrativeAsync_GivenEmptyInput_ThrowsArgumentException(string? input)
    {
        // Arrange
        var svc = new OpenRouterTranslationService(_llmService, NullLogger<OpenRouterTranslationService>.Instance);

        // Act
        var act = () => svc.TranslateNarrativeAsync(input!, "en", "it", null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
