using System.Diagnostics.Metrics;
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Observability;
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
/// #3435 (SP1): unit tests for the automatic image-region seed batch. hi_res is ~200s and not
/// reproducible in CI, so the hi_res extractor + blob are stubbed and IMediator delegates the
/// inner <see cref="SeedPdfImageRegionsCommand"/> to the real handler on the same in-memory db.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3435")]
public sealed class RunImageRegionSeedBatchCommandHandlerTests
{
    private const string ImageRegionJson = """
    {"elements":[{"text":"","page_number":4,"category":"Image","bbox":{"x":0.1,"y":0.5,"width":0.8,"height":0.3}}]}
    """;
    private const string EmptyRegionsJson = """{"elements":[]}""";

    private static IConfiguration Config(bool enabled, int delayMs = 0) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [RunImageRegionSeedBatchCommandHandler.EnabledConfigKey] = enabled ? "true" : "false",
                [RunImageRegionSeedBatchCommandHandler.DelayMsConfigKey] = delayMs.ToString(),
            })
            .Build();

    private static Guid SeedPdf(
        MeepleAiDbContext db,
        string state = "Ready",
        string? indexerVersion = "v1",
        DateTime? seededAt = null,
        string filePath = "pdfs/abc/doc.pdf",
        DateTime? uploadedAt = null,
        int attempts = 0)
    {
        var id = Guid.NewGuid();
        db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = id,
            FileName = "test.pdf",
            FilePath = filePath,
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedByUserId = Guid.NewGuid(),
            ProcessingState = state,
            IndexerVersion = indexerVersion,
            ImageRegionsSeededAt = seededAt,
            UploadedAt = uploadedAt ?? DateTime.UtcNow,
            ImageRegionSeedAttempts = attempts,
        });
        return id;
    }

    private static async Task<int> AttemptsOf(MeepleAiDbContext db, Guid id)
        => (await db.PdfDocuments.AsNoTracking().FirstAsync(p => p.Id == id)).ImageRegionSeedAttempts;

    private static Mock<IBlobStorageService> BlobReturning(byte[]? bytes)
    {
        var blob = new Mock<IBlobStorageService>();
        blob.Setup(b => b.RetrieveAsync(
                It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult<Stream?>(bytes is null ? null : new MemoryStream(bytes)));
        return blob;
    }

    private static Mock<IRawHiResExtractor> ExtractorReturning(string json)
    {
        var extractor = new Mock<IRawHiResExtractor>();
        extractor.Setup(e => e.ExtractRawHiResAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);
        return extractor;
    }

    // IMediator that delegates SeedPdfImageRegionsCommand to the real handler on the same db,
    // so the batch test exercises the real region write path end-to-end.
    private static Mock<IMediator> DelegatingMediator(MeepleAiDbContext db)
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<SeedPdfImageRegionsCommand>(), It.IsAny<CancellationToken>()))
            .Returns((SeedPdfImageRegionsCommand cmd, CancellationToken ct) =>
                new SeedPdfImageRegionsCommandHandler(db, NullLogger<SeedPdfImageRegionsCommandHandler>.Instance)
                    .Handle(cmd, ct));
        return mediator;
    }

    private static RunImageRegionSeedBatchCommandHandler Create(
        MeepleAiDbContext db, IRawHiResExtractor extractor, IBlobStorageService blob, IMediator mediator, IConfiguration config)
        => new(db, extractor, blob, mediator, config, NullLogger<RunImageRegionSeedBatchCommandHandler>.Instance);

    private static async Task<DateTime?> MarkerOf(MeepleAiDbContext db, Guid id)
        => (await db.PdfDocuments.AsNoTracking().FirstAsync(p => p.Id == id)).ImageRegionsSeededAt;

    [Fact]
    public async Task Handle_FlagDisabled_ReturnsDisabled_AndDoesNotExtract()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"sib_{Guid.NewGuid():N}");
        SeedPdf(db);
        await db.SaveChangesAsync();
        var extractor = new Mock<IRawHiResExtractor>(MockBehavior.Strict);

        var handler = Create(db, extractor.Object, BlobReturning(new byte[] { 1 }).Object,
            new Mock<IMediator>().Object, Config(enabled: false));
        var result = await handler.Handle(new RunImageRegionSeedBatchCommand(), CancellationToken.None);

        result.Enabled.Should().BeFalse();
        result.Processed.Should().Be(0);
        extractor.VerifyNoOtherCalls(); // strict mock: extractor never touched
    }

    [Fact]
    public async Task Handle_SeedsRegions_AndStampsMarker()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"sib_{Guid.NewGuid():N}");
        var id = SeedPdf(db);
        await db.SaveChangesAsync();

        var handler = Create(db, ExtractorReturning(ImageRegionJson).Object,
            BlobReturning(new byte[] { 1, 2, 3 }).Object, DelegatingMediator(db).Object, Config(enabled: true));
        var result = await handler.Handle(new RunImageRegionSeedBatchCommand(), CancellationToken.None);

        result.Enabled.Should().BeTrue();
        result.Processed.Should().Be(1);
        result.TotalRegionsSeeded.Should().Be(1);
        (await db.PdfImageRegions.CountAsync(r => r.PdfDocumentId == id)).Should().Be(1);
        (await MarkerOf(db, id)).Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ZeroRegions_StillStampsMarker()
    {
        // NFR1: a region-free PDF must still be marked so it is not re-hi_res'd forever.
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"sib_{Guid.NewGuid():N}");
        var id = SeedPdf(db);
        await db.SaveChangesAsync();

        var handler = Create(db, ExtractorReturning(EmptyRegionsJson).Object,
            BlobReturning(new byte[] { 1 }).Object, DelegatingMediator(db).Object, Config(enabled: true));
        var result = await handler.Handle(new RunImageRegionSeedBatchCommand(), CancellationToken.None);

        result.TotalRegionsSeeded.Should().Be(0);
        result.Processed.Should().Be(1);
        (await MarkerOf(db, id)).Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ExtractorThrows_LeavesUnmarked_AndCounts()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"sib_{Guid.NewGuid():N}");
        var id = SeedPdf(db);
        await db.SaveChangesAsync();
        var extractor = new Mock<IRawHiResExtractor>();
        extractor.Setup(e => e.ExtractRawHiResAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("hi_res timeout"));

        var handler = Create(db, extractor.Object, BlobReturning(new byte[] { 1 }).Object,
            DelegatingMediator(db).Object, Config(enabled: true));
        var result = await handler.Handle(new RunImageRegionSeedBatchCommand(), CancellationToken.None);

        result.Failed.Should().Be(1);
        result.Processed.Should().Be(0);
        (await MarkerOf(db, id)).Should().BeNull(); // unmarked → retried next batch
        (await AttemptsOf(db, id)).Should().Be(1);  // failed attempt counted (slice 2 retry-cap)
    }

    [Fact]
    public async Task Handle_SkipsDeadLetteredPdfs()
    {
        // A PDF that exhausted its retry budget is excluded from the selector (dead-letter),
        // so it no longer re-runs the ~200s hi_res pass and doesn't starve newer PDFs.
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"sib_{Guid.NewGuid():N}");
        SeedPdf(db, attempts: RunImageRegionSeedBatchCommandHandler.MaxSeedAttempts);
        await db.SaveChangesAsync();
        var extractor = new Mock<IRawHiResExtractor>(MockBehavior.Strict);

        var handler = Create(db, extractor.Object, BlobReturning(new byte[] { 1 }).Object,
            new Mock<IMediator>().Object, Config(enabled: true));
        var result = await handler.Handle(new RunImageRegionSeedBatchCommand(), CancellationToken.None);

        result.Processed.Should().Be(0);
        result.Failed.Should().Be(0);
        extractor.VerifyNoOtherCalls(); // dead-lettered PDF never re-attempted
    }

    [Fact]
    public async Task Handle_ExtractorThrows_DeadLettersAtMaxAttempts()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"sib_{Guid.NewGuid():N}");
        // one attempt short of the cap → this failure pushes it to the cap
        var id = SeedPdf(db, attempts: RunImageRegionSeedBatchCommandHandler.MaxSeedAttempts - 1);
        await db.SaveChangesAsync();
        var extractor = new Mock<IRawHiResExtractor>();
        extractor.Setup(e => e.ExtractRawHiResAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("hi_res timeout"));

        var handler = Create(db, extractor.Object, BlobReturning(new byte[] { 1 }).Object,
            DelegatingMediator(db).Object, Config(enabled: true));
        var result = await handler.Handle(new RunImageRegionSeedBatchCommand(), CancellationToken.None);

        result.Failed.Should().Be(1);
        (await AttemptsOf(db, id)).Should().Be(RunImageRegionSeedBatchCommandHandler.MaxSeedAttempts);
        (await MarkerOf(db, id)).Should().BeNull();
    }

    [Fact]
    public async Task Handle_BlobMissing_LeavesUnmarked()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"sib_{Guid.NewGuid():N}");
        var id = SeedPdf(db);
        await db.SaveChangesAsync();
        var extractor = new Mock<IRawHiResExtractor>(MockBehavior.Strict);

        var handler = Create(db, extractor.Object, BlobReturning(null).Object,
            new Mock<IMediator>().Object, Config(enabled: true));
        var result = await handler.Handle(new RunImageRegionSeedBatchCommand(), CancellationToken.None);

        result.Failed.Should().Be(1);
        result.Processed.Should().Be(0);
        (await MarkerOf(db, id)).Should().BeNull();
        (await AttemptsOf(db, id)).Should().Be(1); // storage-miss counts a failed attempt (slice 2)
        extractor.VerifyNoOtherCalls(); // missing blob → no hi_res call
    }

    [Fact]
    public async Task Handle_SelectsOnlyEligiblePdfs()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"sib_{Guid.NewGuid():N}");
        var eligible = SeedPdf(db);
        SeedPdf(db, state: "Chunking");                              // not Ready
        SeedPdf(db, seededAt: DateTime.UtcNow);                      // already seeded
        SeedPdf(db, indexerVersion: null);                          // not in KB corpus
        SeedPdf(db, filePath: PdfDocumentEntity.DemoMockFilePathPrefix + "catan/rulebook.pdf"); // demo-mock shell
        await db.SaveChangesAsync();

        var handler = Create(db, ExtractorReturning(ImageRegionJson).Object,
            BlobReturning(new byte[] { 1 }).Object, DelegatingMediator(db).Object, Config(enabled: true));
        var result = await handler.Handle(new RunImageRegionSeedBatchCommand(), CancellationToken.None);

        result.Processed.Should().Be(1); // only the eligible PDF
        (await MarkerOf(db, eligible)).Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_MixedBatch_OneFailure_DoesNotPoisonOthers()
    {
        // #534 regression: a failed item must not cascade — the other items still persist their
        // regions and get marked. Oldest-first, so the first (t0) fails and the next two succeed.
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"sib_{Guid.NewGuid():N}");
        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var failId = SeedPdf(db, uploadedAt: t0);
        var ok1 = SeedPdf(db, uploadedAt: t0.AddMinutes(1));
        var ok2 = SeedPdf(db, uploadedAt: t0.AddMinutes(2));
        await db.SaveChangesAsync();

        var extractor = new Mock<IRawHiResExtractor>();
        extractor.SetupSequence(e => e.ExtractRawHiResAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("hi_res timeout"))
            .ReturnsAsync(ImageRegionJson)
            .ReturnsAsync(ImageRegionJson);

        var handler = Create(db, extractor.Object, BlobReturning(new byte[] { 1 }).Object,
            DelegatingMediator(db).Object, Config(enabled: true, delayMs: 0));
        var result = await handler.Handle(new RunImageRegionSeedBatchCommand(), CancellationToken.None);

        result.Processed.Should().Be(2);
        result.Failed.Should().Be(1);
        result.TotalRegionsSeeded.Should().Be(2);
        (await MarkerOf(db, failId)).Should().BeNull();       // failed item unmarked → retried
        (await MarkerOf(db, ok1)).Should().NotBeNull();       // later items unaffected by the failure
        (await MarkerOf(db, ok2)).Should().NotBeNull();
        (await db.PdfImageRegions.CountAsync(r => r.PdfDocumentId == ok1)).Should().Be(1);
        (await db.PdfImageRegions.CountAsync(r => r.PdfDocumentId == ok2)).Should().Be(1);
    }

    [Fact]
    public async Task Handle_RespectsBatchSize()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"sib_{Guid.NewGuid():N}");
        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 5; i++)
        {
            SeedPdf(db, uploadedAt: t0.AddMinutes(i));
        }
        await db.SaveChangesAsync();

        var handler = Create(db, ExtractorReturning(ImageRegionJson).Object,
            BlobReturning(new byte[] { 1 }).Object, DelegatingMediator(db).Object, Config(enabled: true, delayMs: 0));
        var result = await handler.Handle(new RunImageRegionSeedBatchCommand(BatchSize: 2), CancellationToken.None);

        result.Processed.Should().Be(2); // only BatchSize picked up, not all 5
    }

    [Fact]
    public async Task Handle_CancelledToken_Throws()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"sib_{Guid.NewGuid():N}");
        SeedPdf(db);
        await db.SaveChangesAsync();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var handler = Create(db, ExtractorReturning(ImageRegionJson).Object,
            BlobReturning(new byte[] { 1 }).Object, DelegatingMediator(db).Object, Config(enabled: true));
        var act = () => handler.Handle(new RunImageRegionSeedBatchCommand(), cts.Token);

        // Genuine caller cancellation must propagate, not be swallowed as a per-item failure.
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Handle_EmitsMetricForEachOutcome()
    {
        // Capture the outcome tag of meepleai.image_region_seed.total — only this handler emits it,
        // so Contain() is robust to any concurrent test class.
        var outcomes = new List<string>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Name == "meepleai.image_region_seed.total")
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == "outcome")
                {
                    lock (outcomes) { outcomes.Add(tag.Value?.ToString() ?? ""); }
                }
            }
        });
        listener.Start();

        using var db = TestDbContextFactory.CreateInMemoryDbContext($"sib_{Guid.NewGuid():N}");
        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        SeedPdf(db, uploadedAt: t0);                                                        // seeded
        SeedPdf(db, uploadedAt: t0.AddMinutes(1));                                          // empty
        SeedPdf(db, uploadedAt: t0.AddMinutes(2));                                          // failed (throw, attempts 0->1)
        SeedPdf(db, uploadedAt: t0.AddMinutes(3),
            attempts: RunImageRegionSeedBatchCommandHandler.MaxSeedAttempts - 1);          // dead_letter (throw at cap)
        await db.SaveChangesAsync();

        var extractor = new Mock<IRawHiResExtractor>();
        extractor.SetupSequence(e => e.ExtractRawHiResAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImageRegionJson)
            .ReturnsAsync(EmptyRegionsJson)
            .ThrowsAsync(new InvalidOperationException("hi_res timeout"))
            .ThrowsAsync(new InvalidOperationException("hi_res timeout"));

        var handler = Create(db, extractor.Object, BlobReturning(new byte[] { 1 }).Object,
            DelegatingMediator(db).Object, Config(enabled: true, delayMs: 0));
        await handler.Handle(new RunImageRegionSeedBatchCommand(BatchSize: 4), CancellationToken.None);

        outcomes.Should().Contain(MeepleAiMetrics.ImageRegionSeedOutcomeSeeded);
        outcomes.Should().Contain(MeepleAiMetrics.ImageRegionSeedOutcomeEmpty);
        outcomes.Should().Contain(MeepleAiMetrics.ImageRegionSeedOutcomeFailed);
        outcomes.Should().Contain(MeepleAiMetrics.ImageRegionSeedOutcomeDeadLetter);
    }
}
