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
public class QueryExpanderTests
{
    private readonly Mock<ILlmService> _llmServiceMock = new();
    private readonly QueryExpander _sut;

    public QueryExpanderTests()
    {
        _sut = new QueryExpander(
            _llmServiceMock.Object,
            NullLogger<QueryExpander>.Instance);
    }

    [Fact]
    public async Task ExpandAsync_LlmReturns3Variations_ShouldReturnOriginalPlus3()
    {
        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<QueryVariations>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryVariations(["v1", "v2", "v3"]));

        var result = await _sut.ExpandAsync("How many players in Catan?", CancellationToken.None);

        result.Count.Should().Be(4);
        result[0].Should().Be("How many players in Catan?");
        result.Should().Contain("v1");
        result.Should().Contain("v2");
        result.Should().Contain("v3");
    }

    [Fact]
    public async Task ExpandAsync_OriginalQueryIsAlwaysFirst()
    {
        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<QueryVariations>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryVariations(["alt1", "alt2"]));

        var result = await _sut.ExpandAsync("What are the rules?", CancellationToken.None);

        result[0].Should().Be("What are the rules?");
    }

    [Fact]
    public async Task ExpandAsync_LlmThrows_ShouldReturnOnlyOriginal()
    {
        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<QueryVariations>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("LLM unavailable"));

        var result = await _sut.ExpandAsync("How to win?", CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Should().Be("How to win?");
    }

    [Fact]
    public async Task ExpandAsync_LlmReturnsNull_ShouldReturnOnlyOriginal()
    {
        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<QueryVariations>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((QueryVariations?)null);

        var result = await _sut.ExpandAsync("Setup guide?", CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Should().Be("Setup guide?");
    }

    [Fact]
    public async Task ExpandAsync_LlmReturnsEmptyList_ShouldReturnOnlyOriginal()
    {
        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<QueryVariations>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryVariations([]));

        var result = await _sut.ExpandAsync("Best strategy?", CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Should().Be("Best strategy?");
    }

    [Fact]
    public async Task ExpandAsync_CancellationRequested_ShouldThrow()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<QueryVariations>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _sut.ExpandAsync("test query", cts.Token));
    }

    [Fact]
    public async Task ExpandAsync_LlmReturns6Variations_ShouldTakeAtMost4()
    {
        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<QueryVariations>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryVariations(["v1", "v2", "v3", "v4", "v5", "v6"]));

        var result = await _sut.ExpandAsync("original", CancellationToken.None);

        Assert.Equal(5, result.Count); // original + 4 max
        result[0].Should().Be("original");
    }

    [Fact]
    public async Task ExpandAsync_UsesRagClassificationSource()
    {
        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<QueryVariations>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryVariations(["v1"]));

        await _sut.ExpandAsync("test", CancellationToken.None);

        _llmServiceMock.Verify(
            x => x.GenerateJsonAsync<QueryVariations>(
                It.IsAny<string>(), It.IsAny<string>(), RequestSource.RagClassification, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
