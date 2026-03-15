using Api.BoundedContexts.KnowledgeBase.Domain.Services.Enhancements;
using Api.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

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

        Assert.Equal(3, result.Relations.Count);
        Assert.Equal(4, result.TotalEntities); // Catan, Trading, Hex Tiles, Resource Collection

        var first = result.Relations[0];
        Assert.Equal("Catan", first.SourceEntity);
        Assert.Equal("Game", first.SourceType);
        Assert.Equal("HasMechanic", first.Relation);
        Assert.Equal("Trading", first.TargetEntity);
        Assert.Equal("Mechanic", first.TargetType);
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
        Assert.Equal(0, result.TotalEntities);
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
        Assert.Equal(0, result.TotalEntities);
    }

    [Fact]
    public async Task ExtractEntitiesAsync_EmptyTextContent_ReturnsEmptyWithoutLlmCall()
    {
        var result = await _sut.ExtractEntitiesAsync(
            Guid.NewGuid(), "Catan", "",
            CancellationToken.None);

        Assert.Empty(result.Relations);
        Assert.Equal(0, result.TotalEntities);

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
        Assert.Equal(0, result.TotalEntities);

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
        Assert.Equal(0, result.TotalEntities);
    }
}
