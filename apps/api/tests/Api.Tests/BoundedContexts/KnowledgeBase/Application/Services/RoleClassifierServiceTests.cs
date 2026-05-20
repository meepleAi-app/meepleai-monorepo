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
}
