using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services.Pdf;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.Unit.DocumentProcessing;

/// <summary>
/// #3435 (SP4): orchestration tests for the async VLM table-extraction batch. The cropper, VLM
/// client and RAG indexer are mocked, so this exercises the control flow (candidate consume, skip
/// terminal, crop→VLM→index→mark, retry-cap/dead-letter) on an in-memory db. Blind spot (like the
/// SP1 sibling): the unique index + the DbUpdateException swallow paths are NOT exercised here (EF
/// InMemory ignores unique indexes and never raises DbUpdateException); those need Postgres.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3435")]
public sealed class RunTableExtractionBatchCommandHandlerTests
{
    private static IConfiguration Config(bool enabled, int? batchSize = null, int? maxAttempts = null) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [RunTableExtractionBatchCommandHandler.EnabledConfigKey] = enabled ? "true" : "false",
                [RunTableExtractionBatchCommandHandler.DelayMsConfigKey] = "0",
                [RunTableExtractionBatchCommandHandler.BatchSizeConfigKey] = batchSize?.ToString(),
                [RunTableExtractionBatchCommandHandler.MaxAttemptsConfigKey] = maxAttempts?.ToString(),
            })
            .Build();

    private static Guid SeedPdf(MeepleAiDbContext db)
    {
        var id = Guid.NewGuid();
        db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = id,
            FileName = "t.pdf",
            FilePath = "pdfs/abc/doc.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedByUserId = Guid.NewGuid(),
            ProcessingState = "Ready",
            IndexerVersion = "v1",
            UploadedAt = DateTime.UtcNow,
        });
        return id;
    }

    private static void SeedRegion(
        MeepleAiDbContext db, Guid pdfId, int page = 3,
        double x = 0.1, double y = 0.2, double w = 0.8, double h = 0.3, string elementType = "Image")
    {
        db.PdfImageRegions.Add(new PdfImageRegionEntity
        {
            Id = Guid.NewGuid(),
            PdfDocumentId = pdfId,
            PageNumber = page,
            X = x,
            Y = y,
            Width = w,
            Height = h,
            ElementType = elementType,
        });
    }

    private static Mock<IMediator> MediatorWithCandidates(params Guid[] pdfIds)
    {
        var mediator = new Mock<IMediator>();
        IReadOnlyList<TableRegionCandidateDto> dtos =
            pdfIds.Select(id => new TableRegionCandidateDto(id, 1, 1)).ToList();
        mediator
            .Setup(m => m.Send(It.IsAny<GetTableRegionCandidatesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dtos);
        return mediator;
    }

    private static Mock<IBlobStorageService> BlobReturning(byte[]? bytes)
    {
        var blob = new Mock<IBlobStorageService>();
        blob.Setup(b => b.RetrieveAsync(
                It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult<Stream?>(bytes is null ? null : new MemoryStream(bytes)));
        return blob;
    }

    private static Mock<IPdfRegionCropper> CropperReturning(byte[]? crop)
    {
        var cropper = new Mock<IPdfRegionCropper>();
        cropper.Setup(c => c.CropRegion(
                It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .Returns(crop);
        return cropper;
    }

    private static Mock<ISmolDoclingTableExtractor> ExtractorReturning(CropTableExtractionResult result)
    {
        var extractor = new Mock<ISmolDoclingTableExtractor>();
        extractor.Setup(e => e.ExtractTableAsync(It.IsAny<byte[]>(), It.IsAny<bool?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        return extractor;
    }

    private static Mock<ITableChunkIndexer> IndexerReturning(Guid? chunkId)
    {
        var indexer = new Mock<ITableChunkIndexer>();
        indexer.Setup(i => i.IndexTableAsync(
                It.IsAny<PdfDocumentEntity>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(chunkId);
        return indexer;
    }

    private static RunTableExtractionBatchCommandHandler Create(
        MeepleAiDbContext db, IMediator mediator, IPdfRegionCropper cropper,
        ISmolDoclingTableExtractor extractor, ITableChunkIndexer indexer, IBlobStorageService blob, IConfiguration config)
        => new(db, mediator, cropper, extractor, indexer, blob, config,
            NullLogger<RunTableExtractionBatchCommandHandler>.Instance);

    private static CropTableExtractionResult Table(string markdown = "| a |\n|---|\n| 1 |")
        => new(IsTable: true, Reason: "table-otsl", Markdown: markdown, Confidence: 0.9, Prefiltered: false, Degenerated: false);

    private static CropTableExtractionResult NotTable(string reason = "no-otsl")
        => new(IsTable: false, Reason: reason, Markdown: "", Confidence: 0.0, Prefiltered: false, Degenerated: false);

    private static async Task<PdfTableExtractionEntity?> StateOf(MeepleAiDbContext db, Guid pdfId)
        => await db.PdfTableExtractions.AsNoTracking().FirstOrDefaultAsync(e => e.PdfDocumentId == pdfId);

    [Fact]
    public async Task Handle_FlagDisabled_ReturnsDisabled_AndNeverCrops()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"tvlm_{Guid.NewGuid():N}");
        var pdf = SeedPdf(db);
        SeedRegion(db, pdf);
        await db.SaveChangesAsync();

        var cropper = new Mock<IPdfRegionCropper>(MockBehavior.Strict);
        var handler = Create(db, MediatorWithCandidates(pdf).Object, cropper.Object,
            ExtractorReturning(Table()).Object, IndexerReturning(Guid.NewGuid()).Object,
            BlobReturning(new byte[] { 1 }).Object, Config(enabled: false));

        var result = await handler.Handle(new RunTableExtractionBatchCommand(), CancellationToken.None);

        result.Enabled.Should().BeFalse();
        result.Processed.Should().Be(0);
        cropper.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_Table_IndexesChunk_AndMarksExtracted()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"tvlm_{Guid.NewGuid():N}");
        var pdf = SeedPdf(db);
        SeedRegion(db, pdf);
        await db.SaveChangesAsync();

        var chunkId = Guid.NewGuid();
        var indexer = IndexerReturning(chunkId);
        var handler = Create(db, MediatorWithCandidates(pdf).Object, CropperReturning(new byte[] { 9 }).Object,
            ExtractorReturning(Table()).Object, indexer.Object, BlobReturning(new byte[] { 1 }).Object,
            Config(enabled: true));

        var result = await handler.Handle(new RunTableExtractionBatchCommand(), CancellationToken.None);

        result.Extracted.Should().Be(1);
        result.Processed.Should().Be(1);
        indexer.Verify(i => i.IndexTableAsync(
            It.IsAny<PdfDocumentEntity>(), 3, 0.1, 0.2, 0.8, 0.3, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        var state = await StateOf(db, pdf);
        state!.Status.Should().Be(PdfTableExtractionEntity.StatusExtracted);
        state.TextChunkId.Should().Be(chunkId);
        state.TableMarkdown.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_NotTable_MarksNotTable_AndNeverIndexes()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"tvlm_{Guid.NewGuid():N}");
        var pdf = SeedPdf(db);
        SeedRegion(db, pdf);
        await db.SaveChangesAsync();

        var indexer = new Mock<ITableChunkIndexer>(MockBehavior.Strict);
        var handler = Create(db, MediatorWithCandidates(pdf).Object, CropperReturning(new byte[] { 9 }).Object,
            ExtractorReturning(NotTable()).Object, indexer.Object, BlobReturning(new byte[] { 1 }).Object,
            Config(enabled: true));

        var result = await handler.Handle(new RunTableExtractionBatchCommand(), CancellationToken.None);

        result.NotTable.Should().Be(1);
        result.Extracted.Should().Be(0);
        indexer.VerifyNoOtherCalls();
        (await StateOf(db, pdf))!.Status.Should().Be(PdfTableExtractionEntity.StatusNotTable);
    }

    [Fact]
    public async Task Handle_CropFails_MarksFailed_AndCountsAttempt()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"tvlm_{Guid.NewGuid():N}");
        var pdf = SeedPdf(db);
        SeedRegion(db, pdf);
        await db.SaveChangesAsync();

        var extractor = new Mock<ISmolDoclingTableExtractor>(MockBehavior.Strict);
        var handler = Create(db, MediatorWithCandidates(pdf).Object, CropperReturning(null).Object,
            extractor.Object, IndexerReturning(Guid.NewGuid()).Object, BlobReturning(new byte[] { 1 }).Object,
            Config(enabled: true));

        var result = await handler.Handle(new RunTableExtractionBatchCommand(), CancellationToken.None);

        result.Failed.Should().Be(1);
        extractor.VerifyNoOtherCalls(); // crop failed → VLM never called
        var state = await StateOf(db, pdf);
        state!.Status.Should().Be(PdfTableExtractionEntity.StatusFailed);
        state.Attempts.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ExtractedRegion_WithLiveChunk_IsSkipped()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"tvlm_{Guid.NewGuid():N}");
        var pdf = SeedPdf(db);
        SeedRegion(db, pdf, page: 3, x: 0.1, y: 0.2, w: 0.8, h: 0.3);
        var chunkId = Guid.NewGuid();
        db.TextChunks.Add(new TextChunkEntity
        {
            Id = chunkId, PdfDocumentId = pdf, Content = "| t |", ChunkIndex = 5, ElementType = "Table",
        });
        db.PdfTableExtractions.Add(new PdfTableExtractionEntity
        {
            Id = Guid.NewGuid(),
            PdfDocumentId = pdf,
            RegionHash = TableRegionKey.ComputeRegionHash(pdf, 3, 0.1, 0.2, 0.8, 0.3),
            PageNumber = 3,
            X = 0.1, Y = 0.2, Width = 0.8, Height = 0.3,
            Status = PdfTableExtractionEntity.StatusExtracted,
            TextChunkId = chunkId, // the produced chunk is still live -> truly done
            TableMarkdown = "| t |",
        });
        await db.SaveChangesAsync();

        var cropper = new Mock<IPdfRegionCropper>(MockBehavior.Strict);
        var handler = Create(db, MediatorWithCandidates(pdf).Object, cropper.Object,
            ExtractorReturning(Table()).Object, IndexerReturning(Guid.NewGuid()).Object,
            BlobReturning(new byte[] { 1 }).Object, Config(enabled: true));

        var result = await handler.Handle(new RunTableExtractionBatchCommand(), CancellationToken.None);

        result.Processed.Should().Be(0);
        cropper.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_TableButNotRetrievable_MarksFailed_NotExtracted()
    {
        // IndexTableAsync returns null (no game / vector document) -> transient, must NOT be marked
        // terminal 'extracted' (findings #3/#4): it retries and eventually dead-letters.
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"tvlm_{Guid.NewGuid():N}");
        var pdf = SeedPdf(db);
        SeedRegion(db, pdf);
        await db.SaveChangesAsync();

        var handler = Create(db, MediatorWithCandidates(pdf).Object, CropperReturning(new byte[] { 9 }).Object,
            ExtractorReturning(Table()).Object, IndexerReturning(null).Object,
            BlobReturning(new byte[] { 1 }).Object, Config(enabled: true));

        var result = await handler.Handle(new RunTableExtractionBatchCommand(), CancellationToken.None);

        result.Failed.Should().Be(1);
        result.Extracted.Should().Be(0);
        var state = await StateOf(db, pdf);
        state!.Status.Should().Be(PdfTableExtractionEntity.StatusFailed);
        state.Attempts.Should().Be(1);
        state.TextChunkId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ExtractedButChunkDeleted_ReindexesFromCachedMarkdown_WithoutVlm()
    {
        // Self-heal after a document reindex wiped the produced chunk (finding #1): the region is
        // re-indexed from the cached markdown, without re-running crop/VLM.
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"tvlm_{Guid.NewGuid():N}");
        var pdf = SeedPdf(db);
        SeedRegion(db, pdf, page: 3, x: 0.1, y: 0.2, w: 0.8, h: 0.3);
        db.PdfTableExtractions.Add(new PdfTableExtractionEntity
        {
            Id = Guid.NewGuid(),
            PdfDocumentId = pdf,
            RegionHash = TableRegionKey.ComputeRegionHash(pdf, 3, 0.1, 0.2, 0.8, 0.3),
            PageNumber = 3,
            X = 0.1, Y = 0.2, Width = 0.8, Height = 0.3,
            Status = PdfTableExtractionEntity.StatusExtracted,
            TextChunkId = Guid.NewGuid(), // dangling — no matching text_chunk row
            TableMarkdown = "| cached |\n|---|\n| 1 |",
        });
        await db.SaveChangesAsync();

        var newChunkId = Guid.NewGuid();
        var cropper = new Mock<IPdfRegionCropper>(MockBehavior.Strict);         // must NOT crop
        var extractor = new Mock<ISmolDoclingTableExtractor>(MockBehavior.Strict); // must NOT call VLM
        var indexer = IndexerReturning(newChunkId);
        var handler = Create(db, MediatorWithCandidates(pdf).Object, cropper.Object,
            extractor.Object, indexer.Object, BlobReturning(new byte[] { 1 }).Object, Config(enabled: true));

        var result = await handler.Handle(new RunTableExtractionBatchCommand(), CancellationToken.None);

        result.Extracted.Should().Be(1);
        cropper.VerifyNoOtherCalls();
        extractor.VerifyNoOtherCalls();
        indexer.Verify(i => i.IndexTableAsync(
            It.IsAny<PdfDocumentEntity>(), 3, 0.1, 0.2, 0.8, 0.3,
            "| cached |\n|---|\n| 1 |", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        var state = await StateOf(db, pdf);
        state!.Status.Should().Be(PdfTableExtractionEntity.StatusExtracted);
        state.TextChunkId.Should().Be(newChunkId);
    }

    [Fact]
    public async Task Handle_RepeatedFailure_DeadLettersAtMaxAttempts()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"tvlm_{Guid.NewGuid():N}");
        var pdf = SeedPdf(db);
        SeedRegion(db, pdf, page: 3, x: 0.1, y: 0.2, w: 0.8, h: 0.3);
        db.PdfTableExtractions.Add(new PdfTableExtractionEntity
        {
            Id = Guid.NewGuid(),
            PdfDocumentId = pdf,
            RegionHash = TableRegionKey.ComputeRegionHash(pdf, 3, 0.1, 0.2, 0.8, 0.3),
            PageNumber = 3,
            X = 0.1, Y = 0.2, Width = 0.8, Height = 0.3,
            Status = PdfTableExtractionEntity.StatusFailed,
            Attempts = 2, // one more failure hits MaxAttempts=3
        });
        await db.SaveChangesAsync();

        var handler = Create(db, MediatorWithCandidates(pdf).Object, CropperReturning(null).Object,
            ExtractorReturning(Table()).Object, IndexerReturning(Guid.NewGuid()).Object,
            BlobReturning(new byte[] { 1 }).Object, Config(enabled: true, maxAttempts: 3));

        var result = await handler.Handle(new RunTableExtractionBatchCommand(), CancellationToken.None);

        result.Failed.Should().Be(1);
        var state = await StateOf(db, pdf);
        state!.Status.Should().Be(PdfTableExtractionEntity.StatusDeadLetter);
        state.Attempts.Should().Be(3);
    }

    [Fact]
    public async Task Handle_RespectsRegionBudget()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"tvlm_{Guid.NewGuid():N}");
        var pdf = SeedPdf(db);
        SeedRegion(db, pdf, page: 1, x: 0.1, y: 0.1, w: 0.3, h: 0.3);
        SeedRegion(db, pdf, page: 2, x: 0.1, y: 0.1, w: 0.3, h: 0.3);
        SeedRegion(db, pdf, page: 3, x: 0.1, y: 0.1, w: 0.3, h: 0.3);
        await db.SaveChangesAsync();

        var handler = Create(db, MediatorWithCandidates(pdf).Object, CropperReturning(new byte[] { 9 }).Object,
            ExtractorReturning(Table()).Object, IndexerReturning(Guid.NewGuid()).Object,
            BlobReturning(new byte[] { 1 }).Object, Config(enabled: true, batchSize: 2));

        var result = await handler.Handle(new RunTableExtractionBatchCommand(), CancellationToken.None);

        result.Processed.Should().Be(2);
    }
}
