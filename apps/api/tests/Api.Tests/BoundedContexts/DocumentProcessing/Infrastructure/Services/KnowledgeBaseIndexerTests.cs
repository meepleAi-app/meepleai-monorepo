using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Infrastructure.Services;

/// <summary>
/// Unit tests for <see cref="KnowledgeBaseIndexer"/>.
/// Libro Game AI Assistant MVP Phase 2 — Task 2.3a.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public class KnowledgeBaseIndexerTests
{
    private readonly IEmbeddingService _embeddingService = Substitute.For<IEmbeddingService>();

    private KnowledgeBaseIndexer CreateSut() =>
        new(_embeddingService, NullLogger<KnowledgeBaseIndexer>.Instance);

    private static EmbeddingResult SuccessResult() =>
        EmbeddingResult.CreateSuccess(new List<float[]> { new float[768] });

    private static KnowledgeChunk MakeChunk(
        string text = "Some page text.",
        int chunkIndex = 0) =>
        KnowledgeChunk.Create(
            batchId: Guid.NewGuid(),
            pageId: Guid.NewGuid(),
            pageNumber: 1,
            text: text,
            chunkIndex: chunkIndex,
            startOffset: 0,
            endOffset: text.Length,
            language: "en",
            confidence: 0.92f);

    [Fact]
    public async Task IndexBatchAsync_WithChunks_GeneratesEmbeddingsAndReturnsCount()
    {
        // ARRANGE
        var batchId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var chunks = new[]
        {
            MakeChunk("First paragraph content.", chunkIndex: 0),
            MakeChunk("Second paragraph content.", chunkIndex: 1),
        };

        _embeddingService
            .GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(SuccessResult());

        var sut = CreateSut();

        // ACT
        var count = await sut.IndexBatchAsync(batchId, gameId, chunks, progress: null, CancellationToken.None);

        // ASSERT
        count.Should().Be(2);
        await _embeddingService.Received(2)
            .GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IndexBatchAsync_EmptyChunks_ReturnsZeroWithoutCallingEmbeddingService()
    {
        // ARRANGE
        var sut = CreateSut();

        // ACT
        var count = await sut.IndexBatchAsync(
            Guid.NewGuid(), Guid.NewGuid(),
            Array.Empty<KnowledgeChunk>(),
            null, CancellationToken.None);

        // ASSERT
        count.Should().Be(0);
        await _embeddingService.DidNotReceive()
            .GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IndexBatchAsync_WithProgressCallback_ReportsProgress()
    {
        // ARRANGE
        var chunks = new[]
        {
            MakeChunk("chunk one", chunkIndex: 0),
            MakeChunk("chunk two", chunkIndex: 1),
            MakeChunk("chunk three", chunkIndex: 2),
        };

        _embeddingService
            .GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(SuccessResult());

        var progressReports = new List<int>();
        var progress = new Progress<int>(v => progressReports.Add(v));

        var sut = CreateSut();

        // ACT
        await sut.IndexBatchAsync(Guid.NewGuid(), Guid.NewGuid(), chunks, progress, CancellationToken.None);

        // Allow Progress<T> callbacks to flush on the thread pool
        await Task.Delay(50);

        // ASSERT
        progressReports.Should().NotBeEmpty();
        progressReports.Should().BeInAscendingOrder();
        progressReports.Last().Should().Be(3);
    }

    [Fact]
    public async Task IndexBatchAsync_EmbeddingServiceReturnsFailure_SkipsChunkAndContinues()
    {
        // ARRANGE
        var chunks = new[]
        {
            MakeChunk("good chunk", chunkIndex: 0),
            MakeChunk("bad chunk", chunkIndex: 1),
            MakeChunk("good chunk again", chunkIndex: 2),
        };

        _embeddingService
            .GenerateEmbeddingAsync("good chunk", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(SuccessResult());

        _embeddingService
            .GenerateEmbeddingAsync("good chunk again", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(SuccessResult());

        _embeddingService
            .GenerateEmbeddingAsync("bad chunk", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(EmbeddingResult.CreateFailure("service unavailable"));

        var sut = CreateSut();

        // ACT
        var count = await sut.IndexBatchAsync(Guid.NewGuid(), Guid.NewGuid(), chunks, null, CancellationToken.None);

        // ASSERT — failed chunk skipped, other two counted
        count.Should().Be(2);
    }

    [Fact]
    public async Task IndexBatchAsync_EmbeddingThrowsException_SkipsChunkAndContinues()
    {
        // ARRANGE
        var chunks = new[]
        {
            MakeChunk("chunk a", chunkIndex: 0),
            MakeChunk("chunk b throws", chunkIndex: 1),
        };

        _embeddingService
            .GenerateEmbeddingAsync("chunk a", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(SuccessResult());

        _embeddingService
            .GenerateEmbeddingAsync("chunk b throws", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Throws(new HttpRequestException("network error"));

        var sut = CreateSut();

        // ACT — should not throw
        var count = await sut.IndexBatchAsync(Guid.NewGuid(), Guid.NewGuid(), chunks, null, CancellationToken.None);

        // ASSERT
        count.Should().Be(1);
    }

    [Fact]
    public async Task IndexBatchAsync_Cancellation_ThrowsOperationCanceledException()
    {
        // ARRANGE
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var chunks = new[] { MakeChunk() };
        var sut = CreateSut();

        // ACT + ASSERT
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => sut.IndexBatchAsync(Guid.NewGuid(), Guid.NewGuid(), chunks, null, cts.Token));
    }

    [Fact]
    public async Task DeleteBatchAsync_DoesNotThrow()
    {
        // ARRANGE
        var sut = CreateSut();

        // ACT + ASSERT — stub implementation must be callable without error
        var act = async () => await sut.DeleteBatchAsync(Guid.NewGuid(), CancellationToken.None);
        await act.Should().NotThrowAsync();
    }
}
