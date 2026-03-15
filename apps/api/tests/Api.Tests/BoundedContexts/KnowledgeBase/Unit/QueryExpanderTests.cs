using Api.BoundedContexts.KnowledgeBase.Domain.Services.Enhancements;
using Api.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

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

        Assert.Equal(4, result.Count);
        Assert.Equal("How many players in Catan?", result[0]);
        Assert.Contains("v1", result);
        Assert.Contains("v2", result);
        Assert.Contains("v3", result);
    }

    [Fact]
    public async Task ExpandAsync_OriginalQueryIsAlwaysFirst()
    {
        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<QueryVariations>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryVariations(["alt1", "alt2"]));

        var result = await _sut.ExpandAsync("What are the rules?", CancellationToken.None);

        Assert.Equal("What are the rules?", result[0]);
    }

    [Fact]
    public async Task ExpandAsync_LlmThrows_ShouldReturnOnlyOriginal()
    {
        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<QueryVariations>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("LLM unavailable"));

        var result = await _sut.ExpandAsync("How to win?", CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("How to win?", result[0]);
    }

    [Fact]
    public async Task ExpandAsync_LlmReturnsNull_ShouldReturnOnlyOriginal()
    {
        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<QueryVariations>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((QueryVariations?)null);

        var result = await _sut.ExpandAsync("Setup guide?", CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("Setup guide?", result[0]);
    }

    [Fact]
    public async Task ExpandAsync_LlmReturnsEmptyList_ShouldReturnOnlyOriginal()
    {
        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<QueryVariations>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryVariations([]));

        var result = await _sut.ExpandAsync("Best strategy?", CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("Best strategy?", result[0]);
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
        Assert.Equal("original", result[0]);
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
