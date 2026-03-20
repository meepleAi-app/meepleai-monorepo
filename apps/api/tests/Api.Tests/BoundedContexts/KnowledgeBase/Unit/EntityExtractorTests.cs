using Api.BoundedContexts.KnowledgeBase.Domain.Services.Enhancements;
using Api.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Unit;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class EntityExtractorTests
{
    private readonly Mock<ILlmService> _llmServiceMock = new();
    private readonly EntityExtractor _sut;

    public EntityExtractorTests()
    {
        _sut = new EntityExtractor(
            _llmServiceMock.Object,
            NullLogger<EntityExtractor>.Instance);
    }

    [Fact]
    public async Task ExtractEntitiesAsync_SuccessfulExtraction_ReturnsRelationsWithCorrectTypes()
    {
        var response = new EntityExtractionResponse(
        [
            new RawRelation("Catan", "Game", "HasMechanic", "Trading", "Mechanic"),
            new RawRelation("Catan", "Game", "HasComponent", "Hex Tiles", "Component"),
            new RawRelation("Trading", "Mechanic", "InteractsWith", "Resource Collection", "Mechanic")
        ]);

        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<EntityExtractionResponse>(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _sut.ExtractEntitiesAsync(
            Guid.NewGuid(), "Catan", "Catan is a game about trading resources.",
            CancellationToken.None);

        result.Relations.Count.Should().Be(3);
        Assert.Equal(4, result.TotalEntities); // Catan, Trading, Hex Tiles, Resource Collection

        var first = result.Relations[0];
        first.SourceEntity.Should().Be("Catan");
        first.SourceType.Should().Be("Game");
        first.Relation.Should().Be("HasMechanic");
        first.TargetEntity.Should().Be("Trading");
        first.TargetType.Should().Be("Mechanic");
    }

    [Fact]
    public async Task ExtractEntitiesAsync_LlmFailure_ReturnsEmptyResult()
    {
        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<EntityExtractionResponse>(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("LLM unavailable"));

        var result = await _sut.ExtractEntitiesAsync(
            Guid.NewGuid(), "Catan", "Some rulebook text.",
            CancellationToken.None);

        Assert.Empty(result.Relations);
        result.TotalEntities.Should().Be(0);
    }

    [Fact]
    public async Task ExtractEntitiesAsync_LlmReturnsNull_ReturnsEmptyResult()
    {
        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<EntityExtractionResponse>(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EntityExtractionResponse?)null);

        var result = await _sut.ExtractEntitiesAsync(
            Guid.NewGuid(), "Catan", "Some rulebook text.",
            CancellationToken.None);

        Assert.Empty(result.Relations);
        result.TotalEntities.Should().Be(0);
    }

    [Fact]
    public async Task ExtractEntitiesAsync_EmptyTextContent_ReturnsEmptyWithoutLlmCall()
    {
        var result = await _sut.ExtractEntitiesAsync(
            Guid.NewGuid(), "Catan", "",
            CancellationToken.None);

        Assert.Empty(result.Relations);
        result.TotalEntities.Should().Be(0);

        _llmServiceMock.Verify(
            x => x.GenerateJsonAsync<EntityExtractionResponse>(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExtractEntitiesAsync_WhitespaceOnlyContent_ReturnsEmptyWithoutLlmCall()
    {
        var result = await _sut.ExtractEntitiesAsync(
            Guid.NewGuid(), "Catan", "   \t\n  ",
            CancellationToken.None);

        Assert.Empty(result.Relations);
        result.TotalEntities.Should().Be(0);

        _llmServiceMock.Verify(
            x => x.GenerateJsonAsync<EntityExtractionResponse>(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExtractEntitiesAsync_AllRelationsHaveDefaultConfidence()
    {
        var response = new EntityExtractionResponse(
        [
            new RawRelation("Catan", "Game", "HasMechanic", "Trading", "Mechanic"),
            new RawRelation("Catan", "Game", "HasPhase", "Setup", "Phase")
        ]);

        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<EntityExtractionResponse>(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _sut.ExtractEntitiesAsync(
            Guid.NewGuid(), "Catan", "Catan rules text.",
            CancellationToken.None);

        Assert.All(result.Relations, r => Assert.Equal(0.8f, r.Confidence));
    }

    [Fact]
    public async Task ExtractEntitiesAsync_CancellationRequested_Throws()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _sut.ExtractEntitiesAsync(
                Guid.NewGuid(), "Catan", "Some text.",
                cts.Token));
    }

    [Fact]
    public async Task ExtractEntitiesAsync_UsesRagClassificationSource()
    {
        var response = new EntityExtractionResponse(
        [
            new RawRelation("Catan", "Game", "HasMechanic", "Trading", "Mechanic")
        ]);

        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<EntityExtractionResponse>(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        await _sut.ExtractEntitiesAsync(
            Guid.NewGuid(), "Catan", "Catan rules text.",
            CancellationToken.None);

        _llmServiceMock.Verify(
            x => x.GenerateJsonAsync<EntityExtractionResponse>(
                It.IsAny<string>(), It.IsAny<string>(),
                RequestSource.RagClassification, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExtractEntitiesAsync_EmptyRelationsList_ReturnsEmptyResult()
    {
        var response = new EntityExtractionResponse([]);

        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<EntityExtractionResponse>(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _sut.ExtractEntitiesAsync(
            Guid.NewGuid(), "Catan", "Some rules text.",
            CancellationToken.None);

        Assert.Empty(result.Relations);
        result.TotalEntities.Should().Be(0);
    }
}
