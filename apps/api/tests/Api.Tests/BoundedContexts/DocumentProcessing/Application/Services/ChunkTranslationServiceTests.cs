using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// La traduzione dei chunk passa dal gateway LLM (ADR-095 fetta 2, #4165): stesso modello
/// configurabile, stessa lunghezza massima, una fonte di costo propria. Il parsing della risposta
/// non cambia.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "4165")]
public sealed class ChunkTranslationServiceTests
{
    private readonly Mock<ILlmService> _llm = new(MockBehavior.Strict);

    private ChunkTranslationService CreateService(Dictionary<string, string?>? settings = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings ?? new Dictionary<string, string?>(StringComparer.Ordinal))
            .Build();
        return new ChunkTranslationService(_llm.Object, configuration, NullLogger<ChunkTranslationService>.Instance);
    }

    private void GatewayAnswers(string response) =>
        _llm.Setup(l => l.GenerateCompletionWithModelAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(response));

    [Fact]
    public async Task Translate_CallsTheGateway_WithTheDefaultModel_ItsOwnRequestSource_AndTheSameMaxTokens()
    {
        GatewayAnswers("Place the board in the middle.");
        var service = CreateService();

        var result = await service.TranslateChunksAsync(["Metti il tabellone al centro."], "it", "en", TestContext.Current.CancellationToken);

        result.Should().ContainSingle().Which.TranslatedText.Should().Be("Place the board in the middle.");
        _llm.Verify(l => l.GenerateCompletionWithModelAsync(
            "meta-llama/llama-3.3-70b-instruct",
            It.Is<string>(prompt => prompt.Contains("from it to en", StringComparison.Ordinal)),
            "Metti il tabellone al centro.",
            RequestSource.ChunkTranslation,
            4096,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Translate_UsesTheModelFromConfiguration()
    {
        GatewayAnswers("ok");
        var service = CreateService(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["RagTranslation:Model"] = "openai/gpt-4o-mini",
        });

        await service.TranslateChunksAsync(["uno"], "it", "en", TestContext.Current.CancellationToken);

        _llm.Verify(l => l.GenerateCompletionWithModelAsync(
            "openai/gpt-4o-mini", It.IsAny<string>(), It.IsAny<string>(),
            RequestSource.ChunkTranslation, 4096, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Translate_ABatch_SplitsTheNumberedSections_BackIntoChunks()
    {
        GatewayAnswers("[1]\nFirst rule.\n\n[2]\nSecond rule.\n");
        var service = CreateService();

        var result = await service.TranslateChunksAsync(["Prima regola.", "Seconda regola."], "it", "en", TestContext.Current.CancellationToken);

        result.Select(r => (r.OriginalIndex, r.TranslatedText))
            .Should().Equal((0, "First rule."), (1, "Second rule."));
        _llm.Verify(l => l.GenerateCompletionWithModelAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.Is<string>(user => user.Contains("[1]", StringComparison.Ordinal) && user.Contains("[2]", StringComparison.Ordinal)),
            It.IsAny<RequestSource>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Translate_WhenTheGatewayFails_SkipsThatBatch_AndKeepsTheOthers()
    {
        _llm.SetupSequence(l => l.GenerateCompletionWithModelAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateFailure("provider down"))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess("Second."));
        var service = CreateService(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["RagTranslation:BatchSize"] = "1",
        });

        var result = await service.TranslateChunksAsync(["Primo.", "Secondo."], "it", "en", TestContext.Current.CancellationToken);

        result.Should().ContainSingle().Which.Should().Match<TranslatedChunk>(
            r => r.OriginalIndex == 1 && r.TranslatedText == "Second.");
    }

    [Fact]
    public async Task Translate_NoChunks_DoesNotCallTheGateway()
    {
        var service = CreateService();

        var result = await service.TranslateChunksAsync([], "it", "en", TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
        _llm.VerifyNoOtherCalls();
    }
}
