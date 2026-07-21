using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class RoleClassifierServiceTests
{
    private readonly Mock<ILlmService> _llmServiceMock = new();

    private RoleClassifierService CreateSut() =>
        new(_llmServiceMock.Object, NullLogger<RoleClassifierService>.Instance);

    [Fact]
    public async Task ClassifyAsync_ChunkWithSetupHeading_AssignsTutorialAndSetupTags()
    {
        var classifier = CreateSut();
        var chunk = new ChunkInput("Setup > Number of Players", "For 4 players, lay out the board...");

        var result = await classifier.ClassifyAsync(new[] { chunk }, TestContext.Current.CancellationToken);

        result.Should().HaveCount(1);
        result[0].HasFlag(GameBookRole.Tutorial).Should().BeTrue();
        result[0].HasFlag(GameBookRole.Setup).Should().BeTrue();
    }

    [Fact]
    public async Task ClassifyAsync_ChunkWithRulesHeading_AssignsRulesReferenceTag()
    {
        var classifier = CreateSut();
        var chunk = new ChunkInput("Combat > Magic Combat", "The wizard rolls d6 for fire damage...");

        var result = await classifier.ClassifyAsync(new[] { chunk }, TestContext.Current.CancellationToken);

        result.Should().HaveCount(1);
        result[0].HasFlag(GameBookRole.RulesReference).Should().BeTrue();
    }

    [Fact]
    public async Task ClassifyAsync_ChunkWithParagraphNumber_AssignsNarrativeTag()
    {
        var classifier = CreateSut();
        var chunk = new ChunkInput("Adventures > Paragraph 147", "You enter the dark cave...");

        var result = await classifier.ClassifyAsync(new[] { chunk }, TestContext.Current.CancellationToken);

        result.Should().HaveCount(1);
        result[0].HasFlag(GameBookRole.Narrative).Should().BeTrue();
    }

    [Fact]
    public async Task ClassifyAsync_AmbiguousChunk_FallsBackToLlm()
    {
        // Issue #1395: service must use GenerateJsonAsync (which strips prose/markdown preamble)
        // instead of raw GenerateCompletionAsync + manual JsonSerializer.Deserialize.
        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<string[][]>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                RequestSource.RagClassification,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new[] { "tutorial", "setup" } });

        var classifier = CreateSut();
        var chunk = new ChunkInput("Random Notes", "To start the game, ...");

        var result = await classifier.ClassifyAsync(new[] { chunk }, TestContext.Current.CancellationToken);

        result.Should().HaveCount(1);
        result[0].HasFlag(GameBookRole.Tutorial).Should().BeTrue();
        result[0].HasFlag(GameBookRole.Setup).Should().BeTrue();
        _llmServiceMock.Verify(
            x => x.GenerateJsonAsync<string[][]>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                RequestSource.RagClassification,
                It.IsAny<CancellationToken>()),
            Times.Once);
        _llmServiceMock.Verify(
            x => x.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ClassifyAsync_AmbiguousChunk_LlmReturnsNull_DefaultsToRulesReference()
    {
        // Issue #1395: GenerateJsonAsync returns null on unrecoverable parse failures (e.g. truncated/garbage LLM output).
        // Safe default for manuals: RulesReference.
        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<string[][]>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                RequestSource.RagClassification,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string[][]?)null);

        var classifier = CreateSut();
        var chunk = new ChunkInput("Random Notes", "Body text here");

        var result = await classifier.ClassifyAsync(new[] { chunk }, TestContext.Current.CancellationToken);

        result.Should().HaveCount(1);
        result[0].Should().Be(GameBookRole.RulesReference);
        _llmServiceMock.Verify(
            x => x.GenerateJsonAsync<string[][]>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                RequestSource.RagClassification,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ClassifyAsync_AmbiguousChunk_LlmThrows_DefaultsToRulesReference()
    {
        // Issue #1395: ingestion must never fail because of LLM exceptions.
        // Catch + log + degrade to safe default (RulesReference).
        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<string[][]>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                RequestSource.RagClassification,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("LLM provider unreachable"));

        var classifier = CreateSut();
        var chunk = new ChunkInput("Random Notes", "Body text here");

        var result = await classifier.ClassifyAsync(new[] { chunk }, TestContext.Current.CancellationToken);

        result.Should().HaveCount(1);
        result[0].Should().Be(GameBookRole.RulesReference);
        _llmServiceMock.Verify(
            x => x.GenerateJsonAsync<string[][]>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                RequestSource.RagClassification,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ClassifyAsync_HeadingMatchesRule_ResolvesDeterministicallyAndNeverInvokesLlm()
    {
        // SP2 task 8 (#3268) role-fast-path regression: a heading that matches a
        // HeadingRule (here "Setup", HeadingRule 1 → Tutorial|Setup) must resolve
        // via the rule table alone. The batch contains ONLY rule-matched chunks, so
        // the ambiguous list stays empty and the LLM fallback must never be reached —
        // guards against a regression that always calls the LLM regardless of match.
        var classifier = CreateSut();
        var chunk = new ChunkInput("Setup", "Place the board in the middle of the table.");

        var result = await classifier.ClassifyAsync(new[] { chunk }, TestContext.Current.CancellationToken);

        result.Should().HaveCount(1);
        result[0].Should().NotBe(GameBookRole.None);
        result[0].HasFlag(GameBookRole.Tutorial).Should().BeTrue();
        result[0].HasFlag(GameBookRole.Setup).Should().BeTrue();
        _llmServiceMock.Verify(
            x => x.GenerateJsonAsync<string[][]>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _llmServiceMock.Verify(
            x => x.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ClassifyAsync_HeadingMatchesNoRule_RoutesToLlmFallback()
    {
        // Companion to the fast-path test above: a heading that matches NO HeadingRule
        // (SP2 reduces, but does not eliminate, the LLM fallback) must still route
        // through ClassifyViaLlmAsync exactly once.
        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<string[][]>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                RequestSource.RagClassification,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new[] { "lore" } });

        var classifier = CreateSut();
        var chunk = new ChunkInput("Zzz Random", "Some body text that matches no heading rule.");

        var result = await classifier.ClassifyAsync(new[] { chunk }, TestContext.Current.CancellationToken);

        result.Should().HaveCount(1);
        result[0].HasFlag(GameBookRole.Lore).Should().BeTrue();
        _llmServiceMock.Verify(
            x => x.GenerateJsonAsync<string[][]>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                RequestSource.RagClassification,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ClassifyAsync_AmbiguousChunk_LlmReturnsFewerArrays_PadsRemainingWithRulesReference()
    {
        // Issue #1395: parity with legacy ParseLlmResponse fallback path —
        // if LLM returns fewer chunk classifications than requested, missing entries default to RulesReference.
        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<string[][]>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                RequestSource.RagClassification,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new[] { "narrative" } });

        var classifier = CreateSut();
        var chunks = new[]
        {
            new ChunkInput("Random Note 1", "First chunk body"),
            new ChunkInput("Random Note 2", "Second chunk body"),
        };

        var result = await classifier.ClassifyAsync(chunks, TestContext.Current.CancellationToken);

        result.Should().HaveCount(2);
        result[0].Should().Be(GameBookRole.Narrative);
        result[1].Should().Be(GameBookRole.RulesReference);
    }
}
