using Api.BoundedContexts.KnowledgeBase.Domain.Services.Enhancements;
using Api.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Unit;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class RaptorIndexerTests
{
    private readonly Mock<ILlmService> _llmServiceMock = new();
    private readonly RaptorIndexer _sut;

    public RaptorIndexerTests()
    {
        _sut = new RaptorIndexer(
            _llmServiceMock.Object,
            NullLogger<RaptorIndexer>.Instance);
    }

    [Fact]
    public async Task BuildTreeAsync_EmptyChunks_ReturnsEmptyResult()
    {
        var result = await _sut.BuildTreeAsync(
            Guid.NewGuid(), Guid.NewGuid(),
            Array.Empty<string>(), maxLevels: 3,
            CancellationToken.None);

        Assert.Equal(0, result.TotalNodes);
        Assert.Equal(0, result.Levels);
        Assert.Empty(result.Summaries);
    }

    [Fact]
    public async Task BuildTreeAsync_SingleChunk_ReturnsEmptyResult()
    {
        // A single chunk has no peers to cluster, so no summaries are generated
        var result = await _sut.BuildTreeAsync(
            Guid.NewGuid(), Guid.NewGuid(),
            new[] { "Only one chunk." }, maxLevels: 3,
            CancellationToken.None);

        Assert.Equal(0, result.TotalNodes);
        Assert.Equal(0, result.Levels);
        Assert.Empty(result.Summaries);
    }

    [Fact]
    public async Task BuildTreeAsync_SmallInput_ProducesSingleLevel()
    {
        // 3 chunks → 1 cluster of 3 → 1 summary → done (1 cluster = single text, loop exits)
        SetupLlmSuccess("Summary of 3 chunks");

        var chunks = new[] { "Chunk 1 text.", "Chunk 2 text.", "Chunk 3 text." };

        var result = await _sut.BuildTreeAsync(
            Guid.NewGuid(), Guid.NewGuid(),
            chunks, maxLevels: 3,
            CancellationToken.None);

        Assert.Equal(1, result.TotalNodes);
        Assert.Equal(1, result.Levels);
        Assert.Single(result.Summaries);

        var summary = result.Summaries[0];
        Assert.Equal(1, summary.TreeLevel);
        Assert.Equal(0, summary.ClusterIndex);
        Assert.Equal(3, summary.SourceChunkCount);
        Assert.Equal("Summary of 3 chunks", summary.SummaryText);
    }

    [Fact]
    public async Task BuildTreeAsync_LargerInput_ProducesMultipleLevels()
    {
        // 15 chunks → 3 clusters of 5 → 3 summaries at level 1
        // 3 summaries → 1 cluster of 3 → 1 summary at level 2
        // 1 summary → loop exits
        SetupLlmSuccess("LLM summary");

        var chunks = Enumerable.Range(1, 15).Select(i => $"Chunk {i} content.").ToList();

        var result = await _sut.BuildTreeAsync(
            Guid.NewGuid(), Guid.NewGuid(),
            chunks, maxLevels: 3,
            CancellationToken.None);

        // Level 1: 3 summaries (3 clusters of 5)
        // Level 2: 1 summary (1 cluster of 3)
        Assert.Equal(4, result.TotalNodes);
        Assert.Equal(2, result.Levels);

        var level1 = result.Summaries.Where(s => s.TreeLevel == 1).ToList();
        var level2 = result.Summaries.Where(s => s.TreeLevel == 2).ToList();

        Assert.Equal(3, level1.Count);
        Assert.Single(level2);

        // Each level-1 summary covers 5 source chunks
        Assert.All(level1, s => Assert.Equal(5, s.SourceChunkCount));

        // Level-2 summary covers 3 level-1 summaries
        Assert.Equal(3, level2[0].SourceChunkCount);
    }

    [Fact]
    public async Task BuildTreeAsync_LlmFailure_UsesTruncatedFallback()
    {
        _llmServiceMock
            .Setup(x => x.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("LLM unavailable"));

        var chunks = new[] { "Short A.", "Short B.", "Short C." };

        var result = await _sut.BuildTreeAsync(
            Guid.NewGuid(), Guid.NewGuid(),
            chunks, maxLevels: 3,
            CancellationToken.None);

        Assert.Equal(1, result.TotalNodes);
        // Fallback summary should contain the concatenated text (within 200 chars)
        Assert.Contains("Short A.", result.Summaries[0].SummaryText);
        Assert.Contains("Short B.", result.Summaries[0].SummaryText);
    }

    [Fact]
    public async Task BuildTreeAsync_LlmReturnsUnsuccessful_UsesFallback()
    {
        _llmServiceMock
            .Setup(x => x.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateFailure("error"));

        var chunks = new[] { "Alpha.", "Beta." };

        var result = await _sut.BuildTreeAsync(
            Guid.NewGuid(), Guid.NewGuid(),
            chunks, maxLevels: 3,
            CancellationToken.None);

        Assert.Equal(1, result.TotalNodes);
        Assert.Contains("Alpha.", result.Summaries[0].SummaryText);
    }

    [Fact]
    public async Task BuildTreeAsync_RespectsMaxLevelsCap()
    {
        // With maxLevels=1, should produce only 1 level even if more clustering is possible
        SetupLlmSuccess("LLM summary");

        // 15 chunks → 3 clusters at level 1, but maxLevels=1 stops recursion
        var chunks = Enumerable.Range(1, 15).Select(i => $"Chunk {i}.").ToList();

        var result = await _sut.BuildTreeAsync(
            Guid.NewGuid(), Guid.NewGuid(),
            chunks, maxLevels: 1,
            CancellationToken.None);

        Assert.Equal(1, result.Levels);
        Assert.Equal(3, result.TotalNodes); // 3 clusters at level 1
        Assert.All(result.Summaries, s => Assert.Equal(1, s.TreeLevel));
    }

    [Fact]
    public async Task BuildTreeAsync_UsesRagClassificationSource()
    {
        SetupLlmSuccess("summary");

        var chunks = new[] { "A.", "B." };

        await _sut.BuildTreeAsync(
            Guid.NewGuid(), Guid.NewGuid(),
            chunks, maxLevels: 3,
            CancellationToken.None);

        _llmServiceMock.Verify(
            x => x.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                RequestSource.RagClassification, It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task BuildTreeAsync_CancellationRequested_Throws()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var chunks = new[] { "A.", "B.", "C." };

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _sut.BuildTreeAsync(
                Guid.NewGuid(), Guid.NewGuid(),
                chunks, maxLevels: 3,
                cts.Token));
    }

    [Fact]
    public async Task BuildTreeAsync_ClusterIndexesAreSequential()
    {
        SetupLlmSuccess("summary");

        // 12 chunks → 3 clusters (5+5+2) at level 1
        var chunks = Enumerable.Range(1, 12).Select(i => $"Chunk {i}.").ToList();

        var result = await _sut.BuildTreeAsync(
            Guid.NewGuid(), Guid.NewGuid(),
            chunks, maxLevels: 3,
            CancellationToken.None);

        var level1 = result.Summaries.Where(s => s.TreeLevel == 1).ToList();
        Assert.Equal(3, level1.Count);
        Assert.Equal(0, level1[0].ClusterIndex);
        Assert.Equal(1, level1[1].ClusterIndex);
        Assert.Equal(2, level1[2].ClusterIndex);

        // Last cluster has 2 chunks (remainder)
        Assert.Equal(2, level1[2].SourceChunkCount);
    }

    [Fact]
    public async Task BuildTreeAsync_FallbackTruncatesLongContent()
    {
        _llmServiceMock
            .Setup(x => x.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("LLM down"));

        // Create chunks with content that exceeds 200 chars when concatenated
        var longChunk = new string('x', 150);
        var chunks = new[] { longChunk, longChunk };

        var result = await _sut.BuildTreeAsync(
            Guid.NewGuid(), Guid.NewGuid(),
            chunks, maxLevels: 3,
            CancellationToken.None);

        Assert.Equal(200, result.Summaries[0].SummaryText.Length);
    }

    private void SetupLlmSuccess(string response)
    {
        _llmServiceMock
            .Setup(x => x.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(response));
    }
}
