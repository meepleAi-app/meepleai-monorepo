using Api.BoundedContexts.KnowledgeBase.Domain.Services.Enhancements;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Unit;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class QueryComplexityClassifierTests
{
    private readonly Mock<ILlmService> _llmServiceMock = new();
    private readonly QueryComplexityClassifier _sut;

    public QueryComplexityClassifierTests()
    {
        _sut = new QueryComplexityClassifier(
            _llmServiceMock.Object,
            NullLogger<QueryComplexityClassifier>.Instance);
    }

    // --- Heuristic: Simple queries ---

    [Theory]
    [InlineData("what is Catan?")]
    [InlineData("define worker placement")]
    [InlineData("who is the designer?")]
    [InlineData("how many players?")]
    [InlineData("when was it released?")]
    [InlineData("where is it published?")]
    public async Task ClassifyAsync_ShortSimpleQuery_ShouldReturnSimple(string query)
    {
        var result = await _sut.ClassifyAsync(query);

        Assert.Equal(QueryComplexityLevel.Simple, result.Level);
        Assert.True(result.Confidence >= 0.9f);
        _llmServiceMock.VerifyNoOtherCalls();
    }

    // --- Heuristic: Complex queries ---

    [Theory]
    [InlineData("compare Catan vs Carcassonne and recommend the best strategy")]
    [InlineData("if I buy this expansion, can I use it with the base game after the first round?")]
    [InlineData("should i get the expansion versus the standalone, pros and cons")]
    public async Task ClassifyAsync_MultipleComplexIndicators_ShouldReturnComplex(string query)
    {
        var result = await _sut.ClassifyAsync(query);

        Assert.Equal(QueryComplexityLevel.Complex, result.Level);
        Assert.True(result.Confidence >= 0.8f);
        _llmServiceMock.VerifyNoOtherCalls();
    }

    // --- LLM fallback ---

    [Fact]
    public async Task ClassifyAsync_AmbiguousQuery_ShouldCallLlm()
    {
        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<ComplexityClassification>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComplexityClassification("Moderate", 0.75f, "Needs rules context"));

        var result = await _sut.ClassifyAsync("How does scoring work in Wingspan?");

        Assert.Equal(QueryComplexityLevel.Moderate, result.Level);
        Assert.Equal(0.75f, result.Confidence);
        Assert.Equal("Needs rules context", result.Reason);
        _llmServiceMock.Verify(
            x => x.GenerateJsonAsync<ComplexityClassification>(
                It.IsAny<string>(), It.IsAny<string>(), RequestSource.RagClassification, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ClassifyAsync_LlmReturnsSimple_ShouldMapCorrectly()
    {
        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<ComplexityClassification>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComplexityClassification("Simple", 0.92f, "Direct fact"));

        var result = await _sut.ClassifyAsync("How does scoring work in Wingspan?");

        Assert.Equal(QueryComplexityLevel.Simple, result.Level);
    }

    [Fact]
    public async Task ClassifyAsync_LlmReturnsComplex_ShouldMapCorrectly()
    {
        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<ComplexityClassification>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComplexityClassification("Complex", 0.88f, "Multi-step reasoning"));

        var result = await _sut.ClassifyAsync("How does scoring work in Wingspan?");

        Assert.Equal(QueryComplexityLevel.Complex, result.Level);
    }

    // --- Error fallback ---

    [Fact]
    public async Task ClassifyAsync_LlmThrows_ShouldReturnModerateFallback()
    {
        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<ComplexityClassification>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("LLM unavailable"));

        var result = await _sut.ClassifyAsync("How does scoring work in Wingspan?");

        Assert.Equal(QueryComplexityLevel.Moderate, result.Level);
        Assert.Equal(0.5f, result.Confidence);
    }

    [Fact]
    public async Task ClassifyAsync_LlmReturnsNull_ShouldReturnModerateFallback()
    {
        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<ComplexityClassification>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ComplexityClassification?)null);

        var result = await _sut.ClassifyAsync("How does scoring work in Wingspan?");

        Assert.Equal(QueryComplexityLevel.Moderate, result.Level);
    }

    // --- Edge cases ---

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task ClassifyAsync_EmptyOrNullQuery_ShouldReturnSimple(string? query)
    {
        var result = await _sut.ClassifyAsync(query!);

        Assert.Equal(QueryComplexityLevel.Simple, result.Level);
        Assert.Equal(1.0f, result.Confidence);
        _llmServiceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ClassifyAsync_LlmReturnsUnknownLevel_ShouldDefaultToModerate()
    {
        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<ComplexityClassification>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComplexityClassification("Unknown", 0.6f, "Unrecognized"));

        var result = await _sut.ClassifyAsync("How does scoring work in Wingspan?");

        Assert.Equal(QueryComplexityLevel.Moderate, result.Level);
    }

    [Fact]
    public async Task ClassifyAsync_LlmReturnsOutOfRangeConfidence_ShouldClamp()
    {
        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<ComplexityClassification>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComplexityClassification("Simple", 1.5f, "Overconfident"));

        var result = await _sut.ClassifyAsync("How does scoring work in Wingspan?");

        Assert.Equal(1.0f, result.Confidence);
    }

    // --- Heuristic unit tests (static method) ---

    [Fact]
    public void ClassifyByHeuristic_LongSimplePrefix_ShouldReturnNull()
    {
        // "what is" prefix but too many words (>6)
        var result = QueryComplexityClassifier.ClassifyByHeuristic(
            "what is the maximum number of players allowed in this game?");

        Assert.Null(result);
    }

    [Fact]
    public void ClassifyByHeuristic_SingleComplexIndicator_ShouldReturnNull()
    {
        // Only 1 complex indicator, needs 2+
        var result = QueryComplexityClassifier.ClassifyByHeuristic("compare these two games");

        Assert.Null(result);
    }
}
