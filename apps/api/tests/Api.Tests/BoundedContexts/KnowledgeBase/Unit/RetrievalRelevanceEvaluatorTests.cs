using Api.BoundedContexts.KnowledgeBase.Domain.Services.Enhancements;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Unit;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class RetrievalRelevanceEvaluatorTests
{
    private readonly Mock<ILlmService> _llmServiceMock = new();
    private readonly RetrievalRelevanceEvaluator _sut;

    public RetrievalRelevanceEvaluatorTests()
    {
        _sut = new RetrievalRelevanceEvaluator(
            _llmServiceMock.Object,
            NullLogger<RetrievalRelevanceEvaluator>.Instance);
    }

    // --- Empty chunks ---

    [Fact]
    public async Task EvaluateAsync_EmptyChunks_ShouldReturnIncorrect()
    {
        var result = await _sut.EvaluateAsync("some query", Array.Empty<ScoredChunk>());

        result.Verdict.Should().Be(RelevanceVerdict.Incorrect);
        Assert.False(result.UseRetrievedDocuments);
        _llmServiceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EvaluateAsync_NullChunks_ShouldReturnIncorrect()
    {
        var result = await _sut.EvaluateAsync("some query", null!);

        result.Verdict.Should().Be(RelevanceVerdict.Incorrect);
        _llmServiceMock.VerifyNoOtherCalls();
    }

    // --- Heuristic: High scores ---

    [Fact]
    public async Task EvaluateAsync_HighAverageScore_ShouldReturnCorrectWithoutLlm()
    {
        var chunks = new[]
        {
            new ScoredChunk("1", "Relevant text about Catan rules", 0.90f),
            new ScoredChunk("2", "More Catan scoring details", 0.88f),
            new ScoredChunk("3", "Catan setup instructions", 0.86f),
        };

        var result = await _sut.EvaluateAsync("How do you score in Catan?", chunks);

        result.Verdict.Should().Be(RelevanceVerdict.Correct);
        Assert.True(result.UseRetrievedDocuments);
        Assert.False(result.ShouldRequery);
        _llmServiceMock.Verify(
            x => x.GenerateJsonAsync<RelevanceClassification>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // --- Heuristic: Low scores ---

    [Fact]
    public async Task EvaluateAsync_LowAverageScore_ShouldReturnIncorrectWithoutLlm()
    {
        var chunks = new[]
        {
            new ScoredChunk("1", "Unrelated cooking recipe", 0.30f),
            new ScoredChunk("2", "Weather forecast data", 0.40f),
            new ScoredChunk("3", "Random news article", 0.50f),
        };

        var result = await _sut.EvaluateAsync("How do you score in Catan?", chunks);

        result.Verdict.Should().Be(RelevanceVerdict.Incorrect);
        Assert.False(result.UseRetrievedDocuments);
        Assert.True(result.ShouldRequery);
        _llmServiceMock.Verify(
            x => x.GenerateJsonAsync<RelevanceClassification>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // --- LLM fallback: Borderline scores ---

    [Fact]
    public async Task EvaluateAsync_BorderlineScores_ShouldCallLlm()
    {
        var chunks = new[]
        {
            new ScoredChunk("1", "Somewhat relevant text", 0.70f),
            new ScoredChunk("2", "Partially matching content", 0.72f),
        };

        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<RelevanceClassification>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RelevanceClassification("correct", 0.80f, "Chunks answer the query"));

        var result = await _sut.EvaluateAsync("How do you score in Catan?", chunks);

        result.Verdict.Should().Be(RelevanceVerdict.Correct);
        result.Confidence.Should().Be(0.80f);
        _llmServiceMock.Verify(
            x => x.GenerateJsonAsync<RelevanceClassification>(
                It.IsAny<string>(), It.IsAny<string>(), RequestSource.RagClassification, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EvaluateAsync_LlmReturnsAmbiguous_ShouldReturnAmbiguous()
    {
        var chunks = new[]
        {
            new ScoredChunk("1", "Partial match", 0.65f),
            new ScoredChunk("2", "Some relevance", 0.70f),
        };

        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<RelevanceClassification>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RelevanceClassification("ambiguous", 0.60f, "Partially relevant"));

        var result = await _sut.EvaluateAsync("query", chunks);

        result.Verdict.Should().Be(RelevanceVerdict.Ambiguous);
        Assert.True(result.UseRetrievedDocuments);
        Assert.True(result.ShouldRequery);
    }

    // --- LLM error fallback ---

    [Fact]
    public async Task EvaluateAsync_LlmThrows_ShouldDefaultToCorrect()
    {
        var chunks = new[]
        {
            new ScoredChunk("1", "Some text", 0.70f),
            new ScoredChunk("2", "More text", 0.72f),
        };

        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<RelevanceClassification>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("LLM unavailable"));

        var result = await _sut.EvaluateAsync("query", chunks);

        result.Verdict.Should().Be(RelevanceVerdict.Correct);
        result.Confidence.Should().Be(0.5f);
        Assert.True(result.UseRetrievedDocuments);
    }

    [Fact]
    public async Task EvaluateAsync_LlmReturnsNull_ShouldDefaultToCorrect()
    {
        var chunks = new[]
        {
            new ScoredChunk("1", "Some text", 0.70f),
            new ScoredChunk("2", "More text", 0.72f),
        };

        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<RelevanceClassification>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RelevanceClassification?)null);

        var result = await _sut.EvaluateAsync("query", chunks);

        result.Verdict.Should().Be(RelevanceVerdict.Correct);
        result.Confidence.Should().Be(0.5f);
    }

    // --- Edge: Exact threshold boundaries ---

    [Fact]
    public async Task EvaluateAsync_ExactlyAtHighThreshold_ShouldReturnCorrect()
    {
        var chunks = new[] { new ScoredChunk("1", "text", 0.85f) };

        var result = await _sut.EvaluateAsync("query", chunks);

        result.Verdict.Should().Be(RelevanceVerdict.Correct);
        _llmServiceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EvaluateAsync_JustBelowHighThreshold_ShouldCallLlm()
    {
        var chunks = new[] { new ScoredChunk("1", "text", 0.84f) };

        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<RelevanceClassification>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RelevanceClassification("correct", 0.7f, "ok"));

        await _sut.EvaluateAsync("query", chunks);

        _llmServiceMock.Verify(
            x => x.GenerateJsonAsync<RelevanceClassification>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EvaluateAsync_ExactlyAtLowThreshold_ShouldCallLlm()
    {
        var chunks = new[] { new ScoredChunk("1", "text", 0.55f) };

        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<RelevanceClassification>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RelevanceClassification("ambiguous", 0.6f, "borderline"));

        await _sut.EvaluateAsync("query", chunks);

        _llmServiceMock.Verify(
            x => x.GenerateJsonAsync<RelevanceClassification>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EvaluateAsync_JustBelowLowThreshold_ShouldReturnIncorrectWithoutLlm()
    {
        var chunks = new[] { new ScoredChunk("1", "text", 0.54f) };

        var result = await _sut.EvaluateAsync("query", chunks);

        result.Verdict.Should().Be(RelevanceVerdict.Incorrect);
        _llmServiceMock.VerifyNoOtherCalls();
    }

    // --- LLM: Unknown verdict mapping ---

    [Fact]
    public async Task EvaluateAsync_LlmReturnsUnknownVerdict_ShouldDefaultToAmbiguous()
    {
        var chunks = new[] { new ScoredChunk("1", "text", 0.70f) };

        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<RelevanceClassification>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RelevanceClassification("unknown", 0.5f, "Unrecognized"));

        var result = await _sut.EvaluateAsync("query", chunks);

        result.Verdict.Should().Be(RelevanceVerdict.Ambiguous);
    }

    // --- LLM: Confidence clamping ---

    [Fact]
    public async Task EvaluateAsync_LlmReturnsOutOfRangeConfidence_ShouldClamp()
    {
        var chunks = new[] { new ScoredChunk("1", "text", 0.70f) };

        _llmServiceMock
            .Setup(x => x.GenerateJsonAsync<RelevanceClassification>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RelevanceClassification("correct", 1.5f, "Overconfident"));

        var result = await _sut.EvaluateAsync("query", chunks);

        result.Confidence.Should().Be(1.0f);
    }
}
