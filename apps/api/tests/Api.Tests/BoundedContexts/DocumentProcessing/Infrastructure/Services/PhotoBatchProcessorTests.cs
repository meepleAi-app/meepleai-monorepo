using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.Services.Pdf;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Infrastructure.Services;

/// <summary>
/// Unit tests for <see cref="PhotoBatchProcessor"/>.
/// Libro Game AI Assistant MVP Phase 1 — Task 1.6 / Phase 2 — Task 2.3.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public class PhotoBatchProcessorTests
{
    private readonly IPhotoBatchUploadRepository _repo = Substitute.For<IPhotoBatchUploadRepository>();
    private readonly IBlobStorageService _blob = Substitute.For<IBlobStorageService>();
    private readonly IPhotoPreprocessor _preprocessor = Substitute.For<IPhotoPreprocessor>();
    private readonly IDocumentChunker _chunker = Substitute.For<IDocumentChunker>();
    private readonly IKnowledgeBaseIndexer _kbIndexer = Substitute.For<IKnowledgeBaseIndexer>();
    // Issue #747 PR-C: real (regex) extractor used here — it's pure, stateless,
    // and the OCR text fed by these tests doesn't carry paragraph headers, so
    // the extractor returns empty arrays without polluting the test's
    // PhotoBatchPage assertions. Dedicated extractor tests live in
    // RegexParagraphNumberExtractorTests.
    private readonly IParagraphNumberExtractor _paragraphExtractor = new RegexParagraphNumberExtractor();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    private PhotoBatchProcessor CreateSut(int maxParallelism = 4)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PhotoBatch:MaxParallelism"] = maxParallelism.ToString()
            })
            .Build();

        return new PhotoBatchProcessor(
            _repo,
            _blob,
            _preprocessor,
            _chunker,
            _kbIndexer,
            _paragraphExtractor,
            _uow,
            config,
            NullLogger<PhotoBatchProcessor>.Instance);
    }

    private static PhotoPreprocessResult MakePreprocessResult(
        string text = "Sample rulebook text",
        double confidence = 0.9,
        bool isBlank = false)
        => new(
            ProcessedImage: new byte[] { 1, 2, 3 },
            ExtractedText: text,
            ConfidenceScore: confidence,
            DetectedOrientation: PageOrientation.Portrait,
            IsBlankPage: isBlank,
            Warnings: Array.Empty<string>());

    private static KnowledgeChunk MakeChunk(Guid batchId, Guid pageId, int pageNumber = 1)
        => KnowledgeChunk.Create(batchId, pageId, pageNumber, "test chunk text", 0, 0, 16, "en", 0.9f);

    [Fact]
    public async Task ProcessAsync_BatchOf3Pages_IndexesAllAndCompletes()
    {
        // Arrange
        var batch = PhotoBatchUpload.Create(Guid.NewGuid(), Guid.NewGuid(), "en", 3);
        var batchId = batch.Id;

        _repo.FindByIdWithPagesAsync(batchId, Arg.Any<CancellationToken>()).Returns(batch);

        _blob.RetrieveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => (Stream)new MemoryStream(new byte[] { 10, 20, 30 }));

        _preprocessor.PreprocessAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(MakePreprocessResult());

        // Chunker returns empty list by default (no chunks → indexer not called)
        _chunker.ChunkPage(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<int>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>())
            .Returns(Array.Empty<KnowledgeChunk>());

        var sut = CreateSut(maxParallelism: 4);

        // Act
        await sut.ProcessAsync(batchId, CancellationToken.None);

        // Assert — all 3 pages indexed, batch completed
        batch.IndexedPages.Should().Be(3);
        batch.Status.Should().Be(PhotoBatchStatus.Completed);
        await _preprocessor.Received(3).PreprocessAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
        await _uow.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_BatchAlreadyInProcessingState_DoesNotCallStartProcessing_AndContinues()
    {
        // Arrange — batch is already Processing (e.g. retry after crash)
        var batch = PhotoBatchUpload.Create(Guid.NewGuid(), Guid.NewGuid(), "en", 2);
        batch.StartProcessing(); // put it in Processing before the SUT sees it
        var batchId = batch.Id;

        _repo.FindByIdWithPagesAsync(batchId, Arg.Any<CancellationToken>()).Returns(batch);

        _blob.RetrieveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => (Stream)new MemoryStream(new byte[] { 1, 2, 3 }));

        _preprocessor.PreprocessAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(MakePreprocessResult());

        _chunker.ChunkPage(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<int>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>())
            .Returns(Array.Empty<KnowledgeChunk>());

        var sut = CreateSut();

        // Act — should NOT throw even though StartProcessing would throw on Processing status
        await sut.ProcessAsync(batchId, CancellationToken.None);

        // Assert
        batch.IndexedPages.Should().Be(2);
        batch.Status.Should().Be(PhotoBatchStatus.Completed);
    }

    [Fact]
    public async Task ProcessAsync_BlobMissingForOnePage_LogsWarningAndSkipsThatPage()
    {
        // Arrange — 2-page batch; first blob exists, second returns null (blob not found)
        var batch = PhotoBatchUpload.Create(Guid.NewGuid(), Guid.NewGuid(), "en", 2);
        var batchId = batch.Id;

        _repo.FindByIdWithPagesAsync(batchId, Arg.Any<CancellationToken>()).Returns(batch);

        // Sequential calls: first returns valid stream, second returns null
        _blob.RetrieveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(
                _ => (Stream?)new MemoryStream(new byte[] { 1 }),
                _ => (Stream?)null);

        _preprocessor.PreprocessAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(MakePreprocessResult());

        _chunker.ChunkPage(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<int>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>())
            .Returns(Array.Empty<KnowledgeChunk>());

        // Use parallelism=1 for deterministic page ordering
        var sut = CreateSut(maxParallelism: 1);

        // Act
        await sut.ProcessAsync(batchId, CancellationToken.None);

        // Assert — only 1 page indexed (missing blob skipped); batch still Processing
        batch.IndexedPages.Should().Be(1);
        batch.Status.Should().Be(PhotoBatchStatus.Processing);
        await _preprocessor.Received(1).PreprocessAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_BatchNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        _repo.FindByIdWithPagesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((PhotoBatchUpload?)null);

        var sut = CreateSut();

        // Act & Assert
        Func<Task> act = () => sut.ProcessAsync(Guid.NewGuid(), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task ProcessAsync_SinglePage_SavesChangesAfterProcessing()
    {
        // Arrange
        var batch = PhotoBatchUpload.Create(Guid.NewGuid(), Guid.NewGuid(), "it", 1);
        var batchId = batch.Id;

        _repo.FindByIdWithPagesAsync(batchId, Arg.Any<CancellationToken>()).Returns(batch);
        _blob.RetrieveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => (Stream)new MemoryStream(new byte[] { 99 }));
        _preprocessor.PreprocessAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(MakePreprocessResult(confidence: 0.95));

        _chunker.ChunkPage(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<int>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>())
            .Returns(Array.Empty<KnowledgeChunk>());

        var sut = CreateSut();

        // Act
        await sut.ProcessAsync(batchId, CancellationToken.None);

        // Assert — SaveChangesAsync called at least twice: after StartProcessing + after all pages done
        await _uow.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
        batch.Status.Should().Be(PhotoBatchStatus.Completed);
    }

    [Fact]
    public async Task ProcessAsync_UsesMissingConfigKey_DefaultsToParallelism4()
    {
        // Arrange — no PhotoBatch:MaxParallelism configured
        var batch = PhotoBatchUpload.Create(Guid.NewGuid(), Guid.NewGuid(), "en", 1);
        var batchId = batch.Id;

        _repo.FindByIdWithPagesAsync(batchId, Arg.Any<CancellationToken>()).Returns(batch);
        _blob.RetrieveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => (Stream)new MemoryStream(new byte[] { 1 }));
        _preprocessor.PreprocessAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(MakePreprocessResult());

        _chunker.ChunkPage(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<int>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>())
            .Returns(Array.Empty<KnowledgeChunk>());

        // Empty config → no MaxParallelism key
        var config = new ConfigurationBuilder().Build();
        var sut = new PhotoBatchProcessor(
            _repo, _blob, _preprocessor, _chunker, _kbIndexer, _paragraphExtractor, _uow, config,
            NullLogger<PhotoBatchProcessor>.Instance);

        // Act — should not throw, default parallelism applied
        Func<Task> act = () => sut.ProcessAsync(batchId, CancellationToken.None);
        await act.Should().NotThrowAsync();

        batch.IndexedPages.Should().Be(1);
        batch.Status.Should().Be(PhotoBatchStatus.Completed);
    }

    // ── New tests: G2 wiring (IDocumentChunker + IKnowledgeBaseIndexer) ──────────────────

    [Fact]
    public async Task ProcessAsync_WithExtractedText_CallsChunkerAndIndexer()
    {
        // Arrange: 1 non-blank page with extracted text
        var batch = PhotoBatchUpload.Create(Guid.NewGuid(), Guid.NewGuid(), "en", 1);
        var batchId = batch.Id;

        _repo.FindByIdWithPagesAsync(batchId, Arg.Any<CancellationToken>()).Returns(batch);
        _blob.RetrieveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => (Stream)new MemoryStream(new byte[] { 1, 2, 3 }));
        _preprocessor.PreprocessAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(MakePreprocessResult(text: "Move your piece forward three spaces.", confidence: 0.9, isBlank: false));

        // Chunker returns 1 chunk → indexer should be called
        _chunker.ChunkPage(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<int>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>())
            .Returns(ci => new[] { MakeChunk(ci.ArgAt<Guid>(0), ci.ArgAt<Guid>(1)) });

        _kbIndexer.IndexBatchAsync(Arg.Any<Guid>(), Arg.Any<Guid>(),
                Arg.Any<IReadOnlyList<KnowledgeChunk>>(), Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>())
            .Returns(1);

        var sut = CreateSut(maxParallelism: 1);

        // Act
        await sut.ProcessAsync(batchId, CancellationToken.None);

        // Assert: chunker called once, indexer called once
        _chunker.Received(1).ChunkPage(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<int>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
        await _kbIndexer.Received(1).IndexBatchAsync(Arg.Any<Guid>(), Arg.Any<Guid>(),
            Arg.Any<IReadOnlyList<KnowledgeChunk>>(), Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>());

        // Batch still completes normally
        batch.Status.Should().Be(PhotoBatchStatus.Completed);
        batch.IndexedPages.Should().Be(1);
    }

    [Fact]
    public async Task ProcessAsync_WithBlankPage_DoesNotCallChunkerOrIndexer()
    {
        // Arrange: 1 page where IsBlankPage=true
        var batch = PhotoBatchUpload.Create(Guid.NewGuid(), Guid.NewGuid(), "en", 1);
        var batchId = batch.Id;

        _repo.FindByIdWithPagesAsync(batchId, Arg.Any<CancellationToken>()).Returns(batch);
        _blob.RetrieveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => (Stream)new MemoryStream(new byte[] { 1 }));
        _preprocessor.PreprocessAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(MakePreprocessResult(text: "", confidence: 0.3, isBlank: true));

        var sut = CreateSut(maxParallelism: 1);

        // Act
        await sut.ProcessAsync(batchId, CancellationToken.None);

        // Assert: neither chunker nor indexer called
        _chunker.DidNotReceive().ChunkPage(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<int>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
        await _kbIndexer.DidNotReceive().IndexBatchAsync(Arg.Any<Guid>(), Arg.Any<Guid>(),
            Arg.Any<IReadOnlyList<KnowledgeChunk>>(), Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>());

        // Batch still completes (blank page still recorded via RecordPageIndexed)
        batch.IndexedPages.Should().Be(1);
        batch.Status.Should().Be(PhotoBatchStatus.Completed);
    }

    [Fact]
    public async Task ProcessAsync_KbIndexingThrows_DoesNotAbortBatch()
    {
        // Arrange: chunker returns 1 chunk, indexer throws a non-cancellation exception
        var batch = PhotoBatchUpload.Create(Guid.NewGuid(), Guid.NewGuid(), "en", 1);
        var batchId = batch.Id;

        _repo.FindByIdWithPagesAsync(batchId, Arg.Any<CancellationToken>()).Returns(batch);
        _blob.RetrieveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => (Stream)new MemoryStream(new byte[] { 1, 2, 3 }));
        _preprocessor.PreprocessAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(MakePreprocessResult(text: "Some board game text.", confidence: 0.85));

        _chunker.ChunkPage(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<int>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>())
            .Returns(ci => new[] { MakeChunk(ci.ArgAt<Guid>(0), ci.ArgAt<Guid>(1)) });

        // Indexer throws a transient infrastructure error
        _kbIndexer.IndexBatchAsync(Arg.Any<Guid>(), Arg.Any<Guid>(),
                Arg.Any<IReadOnlyList<KnowledgeChunk>>(), Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Embedding service unavailable"));

        var sut = CreateSut(maxParallelism: 1);

        // Act — must not propagate the indexer exception
        Func<Task> act = () => sut.ProcessAsync(batchId, CancellationToken.None);
        await act.Should().NotThrowAsync();

        // Assert: batch still reaches Completed (graceful degradation)
        batch.Status.Should().Be(PhotoBatchStatus.Completed);
        batch.IndexedPages.Should().Be(1);
    }
}
