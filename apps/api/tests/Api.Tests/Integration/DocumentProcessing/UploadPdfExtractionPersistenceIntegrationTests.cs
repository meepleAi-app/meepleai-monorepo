using System.Security.Cryptography;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Configuration;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Chunking;
using Api.BoundedContexts.KnowledgeBase.Domain.Chunking;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Services;
using Api.Services.Pdf;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// Issue #3281 (Task 4): regression test for the pre-existing AsNoTracking persistence bug in
/// <see cref="UploadPdfCommandHandler"/>'s background pipeline. The pipeline's working
/// <c>pdfDoc</c> is loaded <c>AsNoTracking()</c> and state transitions route through
/// <c>IPdfDocumentRepository.UpdateAsync</c> (whose <c>MapToPersistence</c> omits +
/// whole-row-clobbers the extraction content columns), so <c>ExtractedText</c> and
/// <c>StructuredElementsJson</c> written during extraction never durably persisted for
/// Upload-originated PDFs — blocking re-index parity (<c>IndexPdfCommandHandler</c> reads
/// <c>StructuredElementsJson</c> and requires non-null <c>ExtractedText</c>).
///
/// Modeled on <c>UploadPdfHeadingAwareIntegrationTests</c>'s harness (Task 3): real Postgres via
/// <see cref="SharedTestcontainersFixture"/>, <c>InlineBackgroundTaskService</c> so the BG
/// pipeline (extract → chunk → embed → index → finalize) runs synchronously before assertions.
/// </summary>
[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3281")]
public sealed class UploadPdfExtractionPersistenceIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;
    private IConnectionMultiplexer? _redis;
    private string? _testDataDirectory;
    private InlineBackgroundTaskService? _inlineBgService;

    /// <summary>#4443 R1: holds the background task at its first read of the aggregate.</summary>
    private readonly BackgroundTaskGate _gate = new();
    private Mock<IPdfTextExtractor>? _extractorMock;

    /// <summary>
    /// Issue #3846: when true the blob double reports a successful <c>StoreAsync</c> but keeps
    /// nothing, reproducing an object that never reached the bucket (or was removed out of band).
    /// </summary>
    private bool _simulateObjectNeverStored;

    /// <summary>Issue #3846: what the structured-content extractor was handed, and whether it was readable.</summary>
    private string? _structuredExtractionPath;
    private byte[]? _structuredExtractionBytes;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public UploadPdfExtractionPersistenceIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_extractionpersist_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        _testDataDirectory = Path.Combine(Path.GetTempPath(), "meepleai-test-extractionpersist-" + Guid.NewGuid());
        Directory.CreateDirectory(_testDataDirectory);

        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);

        _redis = await ConnectionMultiplexer.ConnectAsync(_fixture.RedisConnectionString);
        services.AddSingleton<IConnectionMultiplexer>(_redis);
        // #4339: un gestore degli eventi di stato del PDF chiede la cache semantica (da #3982). Senza,
        // la pubblicazione fallisce e la pipeline finisce con "Invalid operation" per una ragione
        // che non è quella che questo test prova.
        services.AddSingleton(Mock.Of<Api.BoundedContexts.KnowledgeBase.Application.Services.ISemanticResponseCache>());

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<PdfDocumentRepository>();
        services.AddScoped<IPdfDocumentRepository>(sp =>
            new GatedPdfDocumentRepository(sp.GetRequiredService<PdfDocumentRepository>(), _gate));
        services.AddScoped<IPdfClaimService, RelationalPdfClaimService>();
        // #4443 R2: an admin or editor upload goes to the queue.
        services.AddScoped<IProcessingJobRepository, ProcessingJobRepository>();

        services.AddScoped<UploadPdfCommandHandler>();

        services.Configure<PdfProcessingOptions>(options =>
        {
            options.MaxFileSizeBytes = 10 * 1024 * 1024;
        });

        // Inline background task service so the BG pipeline runs synchronously before assertions
        // (mirrors UploadPdfHeadingAwareIntegrationTests / PdfIndexingFlowKbFlagIntegrationTests).
        _inlineBgService = new InlineBackgroundTaskService();
        services.AddSingleton<IBackgroundTaskService>(_inlineBgService);

        services.AddSingleton<IEmbeddingService>(new MockEmbeddingService(dimensions: 768));

        // Extractor returns a paged result WITH non-empty StructuredElements (a Title + body
        // element) so ExtractPdfContentAsync has something to serialize into StructuredElementsJson.
        var extractorMock = new Mock<IPdfTextExtractor>();
        extractorMock
            .Setup(e => e.ExtractTextAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TextExtractionResult.CreateSuccess(
                extractedText: "Setup. Place 3 tiles on the board.",
                pageCount: 1,
                characterCount: 34,
                ocrTriggered: false,
                quality: ExtractionQuality.High));
        extractorMock
            .Setup(e => e.ExtractPagedTextAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PagedTextExtractionResult.CreateSuccess(
                pageChunks: new[]
                {
                    new PageTextChunk(
                        PageNumber: 1,
                        Text: "Setup. Place 3 tiles on the board.",
                        CharStartIndex: 0,
                        CharEndIndex: 34)
                },
                totalPages: 1,
                totalCharacters: 34,
                ocrTriggered: false,
                structuredElements: new List<ExtractedElement>
                {
                    new("Setup", 1, "Title"),
                    new("Place 3 tiles on the board.", 1, "NarrativeText")
                }));
        services.AddSingleton<IPdfTextExtractor>(extractorMock.Object);
        _extractorMock = extractorMock;

        var tableExtractorMock = new Mock<IPdfTableExtractor>();
        tableExtractorMock
            .Setup(t => t.ExtractStructuredContentAsync(It.IsAny<string>()!, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string path, CancellationToken _) =>
            {
                // Issue #3846: record what the extractor was actually handed. Every path-based adapter
                // (iText tables, Tesseract, Vision) starts with File.Exists(path) and returns a failure
                // result the caller drops with `if (!Success) return;` — so a bucket key here is a
                // SILENT loss of tables, diagrams and atomic rules on every S3 upload, with the PDF
                // still reaching Ready.
                _structuredExtractionPath = path;
                _structuredExtractionBytes = File.Exists(path) ? File.ReadAllBytes(path) : null;

                return new StructuredContentResult
                {
                    Success = false,
                    ErrorMessage = "Skipped in test"
                };
            });
        services.AddSingleton<IPdfTableExtractor>(tableExtractorMock.Object);

        // Mocked IAdvancedChunkingService so ChunkExtractedTextAsync (Task 3 wiring) takes the
        // heading-aware branch — irrelevant to this test's assertions but keeps the pipeline shape
        // identical to production (avoids depending on the flat-fallback branch by coincidence).
        var advancedChunkingMock = new Mock<IAdvancedChunkingService>();
        advancedChunkingMock
            .Setup(s => s.ChunkDocumentAsync(
                It.IsAny<ExtractedDocument>(),
                It.IsAny<ChunkingConfiguration?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExtractedDocument doc, ChunkingConfiguration? _, CancellationToken _) =>
            {
                var parent = HierarchicalChunk.CreateParent(
                    "Setup section content",
                    new ChunkMetadata { Page = 1, Heading = "Setup", ElementType = "Title", DocumentId = doc.Id });
                var child = HierarchicalChunk.CreateChild(
                    "Place 3 tiles on the board.",
                    level: 2,
                    new ChunkMetadata { Page = 1, Heading = "Setup", ElementType = "NarrativeText", DocumentId = doc.Id },
                    parent.Id);
                return new List<HierarchicalChunk> { parent, child };
            });
        services.AddSingleton<IAdvancedChunkingService>(advancedChunkingMock.Object);

        // Issue #3846: this double used to write the blob to a flat file under the temp directory and
        // resolve reads with File.Exists(firstArgument), ignoring the resourceKey entirely — so every
        // key answered and the S3 read defect stayed invisible, exactly like STORAGE_PROVIDER=local
        // where the filesystem fallback covers the miss. It now behaves like a bucket: StoreAsync mints
        // a random fileId and returns the real key layout {category}/{resourceKey}/{fileId}_{name},
        // RetrieveAsync resolves by EXACT (category, resourceKey, fileId) with no cross-folder search,
        // and nothing is written to disk so the filesystem fallback cannot mask a miss.
        var storedObjects = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var blobStorageMock = new Mock<IBlobStorageService>();
        blobStorageMock
            .Setup(b => b.StoreAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>()!,
                It.IsAny<BlobCategory>(),
                It.IsAny<string>()!,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream stream, string fileName, BlobCategory category, string resourceKey, CancellationToken ct) =>
            {
                // A Guid without hyphens: UploadPdfCommandHandler parses the returned FileId into
                // PdfDocumentEntity.Id, so it has to be a real Guid, like the production backends emit.
                var fileId = Guid.NewGuid().ToString("N");
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                if (!_simulateObjectNeverStored)
                {
                    storedObjects[ObjectKey(category, resourceKey, fileId)] = buffer.ToArray();
                }
                return new BlobStorageResult(
                    true,
                    fileId,
                    $"{category.ToS3Folder()}/{resourceKey}/{fileId}_{fileName}",
                    buffer.Length,
                    null);
            });
        blobStorageMock
            .Setup(b => b.RetrieveAsync(It.IsAny<string>()!, It.IsAny<BlobCategory>(), It.IsAny<string>()!, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string fileId, BlobCategory category, string resourceKey, CancellationToken ct) =>
                storedObjects.TryGetValue(ObjectKey(category, resourceKey, fileId), out var bytes)
                    ? new MemoryStream(bytes)
                    : (Stream?)null);
        blobStorageMock
            .Setup(b => b.DeleteAsync(It.IsAny<string>()!, It.IsAny<BlobCategory>(), It.IsAny<string>()!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        services.AddSingleton<IBlobStorageService>(blobStorageMock.Object);

        var cacheMock = new Mock<IAiResponseCacheService>();
        cacheMock
            .Setup(c => c.InvalidateGameAsync(It.IsAny<string>()!, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        services.AddSingleton<IAiResponseCacheService>(cacheMock.Object);

        var configServiceMock = new Mock<IConfigurationService>();
        configServiceMock
            .Setup(c => c.GetValueAsync<int?>(It.IsAny<string>()!, It.IsAny<int?>(), It.IsAny<string>()))
            .Returns(Task.FromResult<int?>(null));
        services.AddSingleton<IConfigurationService>(configServiceMock.Object);

        services.AddScoped<IPdfUploadQuotaService, PdfUploadQuotaService>();

        // Real TextChunkingService still registered so the (unexercised in this test) flat
        // fallback path continues to resolve — mirrors production DI shape.
        services.AddSingleton<ITextChunkingService, TextChunkingService>();

        services.AddSingleton(Mock.Of<Api.BoundedContexts.DocumentProcessing.Application.Services.IProcessingMetricsService>());

        services.AddScoped(_ => Mock.Of<Api.BoundedContexts.KnowledgeBase.Domain.Repositories.IVectorDocumentRepository>());

        services.AddScoped<Api.BoundedContexts.KnowledgeBase.Application.Services.IPdfIndexingPipeline,
            Api.BoundedContexts.KnowledgeBase.Application.Services.PdfIndexingPipeline>();

        services.AddSingleton<IMemoryCache, MemoryCache>();
        services.AddSingleton<IDistributedCache>(new MemoryDistributedCache(
            Options.Create(new MemoryDistributedCacheOptions())));
        services.AddHybridCache();
        services.AddScoped<Api.Services.ICacheInvalidationRetryPolicy>(_ => new PassthroughRetryPolicy());

        services.Configure<Api.Configuration.HybridCacheConfiguration>(opts =>
        {
            opts.EnableL2Cache = true;
            opts.EnableTags = true;
            opts.DefaultExpiration = TimeSpan.FromMinutes(5);
            opts.MaxTagsPerEntry = 10;
        });
        services.AddSingleton<Api.Services.IHybridCacheService, Api.Services.HybridCacheService>();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        await _dbContext.Database.MigrateAsync(TestCancellationToken);

        await SeedTestDataAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_testDataDirectory != null && Directory.Exists(_testDataDirectory))
        {
            try
            {
                Directory.Delete(_testDataDirectory, true);
            }
            catch (IOException)
            {
                // Best effort cleanup
            }
        }

        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }

        _redis?.Dispose();

        if (_serviceProvider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else
        {
            (_serviceProvider as IDisposable)?.Dispose();
        }

        if (!string.IsNullOrEmpty(_databaseName))
        {
            try
            {
                await _fixture.DropIsolatedDatabaseAsync(_databaseName);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private async Task SeedTestDataAsync()
    {
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = $"extractionpersist-{Guid.NewGuid():N}@test.com",
            DisplayName = "Extraction Persistence Test User",
            Role = "User",
            Tier = "Free",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext!.Users.Add(user);

        var game = new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = $"Extraction Persistence Test Game {Guid.NewGuid():N}",
            BggId = RandomNumberGenerator.GetInt32(100000, 1_000_000),
            YearPublished = 2024,
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 30,
            HasKnowledgeBase = false,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.SharedGames.Add(game);

        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    private static IFormFile CreateMockFormFile(string fileName, byte[] content, string contentType = "application/pdf")
    {
        var formFile = new Mock<IFormFile>();
        formFile.Setup(f => f.FileName).Returns(fileName);
        formFile.Setup(f => f.Length).Returns(content.Length);
        formFile.Setup(f => f.ContentType).Returns(contentType);
        formFile.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream(content));
        return formFile.Object;
    }

    /// <summary>
    /// Issue #3846: the identity of a stored object is the whole triple. Both production backends
    /// resolve by exact <c>{category}/{resourceKey}/{fileId}_</c> prefix, so a read that gets any of
    /// the three wrong finds nothing — it does not fall back to another folder.
    /// </summary>
    private static string ObjectKey(BlobCategory category, string resourceKey, string fileId) =>
        $"{category}|{resourceKey}|{fileId}";

    private static byte[] CreateValidPdfBytes(int sizeInBytes)
    {
        var header = "%PDF-1.4\n"u8.ToArray();
        var trailer = "%%EOF\n"u8.ToArray();
        var padding = new byte[Math.Max(0, sizeInBytes - header.Length - trailer.Length)];

        var pdf = new byte[header.Length + padding.Length + trailer.Length];
        Buffer.BlockCopy(header, 0, pdf, 0, header.Length);
        Buffer.BlockCopy(padding, 0, pdf, header.Length, padding.Length);
        Buffer.BlockCopy(trailer, 0, pdf, header.Length + padding.Length, trailer.Length);

        return pdf;
    }

    [Fact(Timeout = 90000)]
    public async Task ProcessPdfAsync_UploadPathReachesReady_PersistsExtractedTextAndStructuredElementsJson()
    {
        // Arrange
        var handler = _serviceProvider!.GetRequiredService<UploadPdfCommandHandler>();
        var testUser = await _dbContext!.Users.FirstAsync(TestCancellationToken);
        var testGame = await _dbContext.SharedGames.FirstAsync(TestCancellationToken);

        var pdfBytes = CreateValidPdfBytes(1024);
        var formFile = CreateMockFormFile("extraction-persistence.pdf", pdfBytes);

        var command = new Api.BoundedContexts.DocumentProcessing.Application.Commands.UploadPdfCommand(
            GameId: testGame.Id.ToString(),
            Metadata: null,
            PrivateGameId: null,
            UserId: testUser.Id,
            File: formFile);

        // Act
        var result = await handler.Handle(command, TestCancellationToken);
        await _inlineBgService!.WaitForAllAsync();

        // Assert: upload accept-stage succeeded
        result.Should().NotBeNull();
        result.Success.Should().BeTrue("upload accept-stage must succeed for a valid PDF");
        result.Document.Should().NotBeNull();

        var pdfDocId = Guid.Parse(result.Document!.Id.ToString());

        // Re-query from a FRESH AsNoTracking projection (bypasses the change tracker
        // entirely) so this assertion actually exercises the durable DB row, not an
        // in-memory reference that happens to still carry the value.
        var persisted = await _dbContext.PdfDocuments
            .AsNoTracking()
            .FirstAsync(p => p.Id == pdfDocId, TestCancellationToken);

        persisted.ProcessingState.Should().Be(
            nameof(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Ready),
            $"BG pipeline must reach Ready for this regression test to be meaningful. ProcessingError='{persisted.ProcessingError}'.");

        // Regression assertions for Issue #3281 Task 4 (pre-existing AsNoTracking bug): before
        // the fix, both columns are null here because the working pdfDoc is AsNoTracking and the
        // Ready transition routes through IPdfDocumentRepository.UpdateAsync, whose
        // MapToPersistence omits + whole-row-clobbers ExtractedText/StructuredElementsJson.
        persisted.ExtractedText.Should().NotBeNullOrEmpty(
            "ExtractedText must durably persist for Upload-originated PDFs (re-index parity: " +
            "IndexPdfCommandHandler requires non-null ExtractedText)");
        persisted.StructuredElementsJson.Should().NotBeNull(
            "StructuredElementsJson must durably persist for Upload-originated PDFs (re-index " +
            "parity: IndexPdfCommandHandler reads it to rebuild headings)");
    }

    /// <summary>
    /// #4339: the upload page follows the progress (GET /pdfs/{id}/progress) and waits for
    /// Completed. Finalization wrote Completed and THEN moved the PDF to Ready through the
    /// repository, whose MapToPersistence omits the progress column and rewrites the whole row:
    /// the progress came back null, and the page stayed at "Indexing 80%" forever.
    /// </summary>
    [Fact(Timeout = 90000)]
    public async Task ProcessPdfAsync_UploadPathReachesReady_LeavesTheProgressAtCompleted()
    {
        var handler = _serviceProvider!.GetRequiredService<UploadPdfCommandHandler>();
        var testUser = await _dbContext!.Users.FirstAsync(TestCancellationToken);
        var testGame = await _dbContext.SharedGames.FirstAsync(TestCancellationToken);
        var command = new Api.BoundedContexts.DocumentProcessing.Application.Commands.UploadPdfCommand(
            GameId: testGame.Id.ToString(),
            Metadata: null,
            PrivateGameId: null,
            UserId: testUser.Id,
            File: CreateMockFormFile("progress-completed.pdf", CreateValidPdfBytes(1024)));

        var result = await handler.Handle(command, TestCancellationToken);
        await _inlineBgService!.WaitForAllAsync();

        result.Success.Should().BeTrue();
        var pdfDocId = Guid.Parse(result.Document!.Id.ToString());
        var persisted = await _dbContext.PdfDocuments
            .AsNoTracking()
            .FirstAsync(p => p.Id == pdfDocId, TestCancellationToken);
        persisted.ProcessingState.Should().Be(
            nameof(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Ready),
            $"the test needs the pipeline to reach Ready. ProcessingError='{persisted.ProcessingError}'.");

        persisted.ProcessingProgressJson.Should().NotBeNull("the page reads the progress to know the PDF is done");
        var progress = System.Text.Json.JsonSerializer.Deserialize<Api.Models.ProcessingProgress>(persisted.ProcessingProgressJson!);
        progress!.CurrentStep.Should().Be(Api.Models.ProcessingStep.Completed);
    }

    /// <summary>
    /// Issue #3846, second (silent) half: the structured-content extractor takes a FILE PATH. Under S3
    /// the pipeline handed it the persisted <c>FilePath</c>, which is a bucket key — the adapter's
    /// <c>File.Exists</c> failed, it returned a failure result, and the caller dropped it with
    /// <c>if (!structuredResult.Success) return;</c>. No log, no Failed state: tables, diagrams and
    /// atomic rules were simply never extracted while the PDF still reached <c>Ready</c>. Fixing only
    /// the resourceKey would have turned "an upload reaches Ready" green on an amputated document.
    /// </summary>
    [Fact(Timeout = 90000)]
    public async Task ProcessPdfAsync_HandsTheStructuredExtractorAReadableFile_NotABucketKey()
    {
        var handler = _serviceProvider!.GetRequiredService<UploadPdfCommandHandler>();
        var testUser = await _dbContext!.Users.FirstAsync(TestCancellationToken);
        var testGame = await _dbContext.SharedGames.FirstAsync(TestCancellationToken);

        var pdfBytes = CreateValidPdfBytes(1024);
        var command = new Api.BoundedContexts.DocumentProcessing.Application.Commands.UploadPdfCommand(
            GameId: testGame.Id.ToString(),
            Metadata: null,
            PrivateGameId: null,
            UserId: testUser.Id,
            File: CreateMockFormFile("structured-content.pdf", pdfBytes));

        await handler.Handle(command, TestCancellationToken);
        await _inlineBgService!.WaitForAllAsync();

        _structuredExtractionPath.Should().NotBeNull("the structured-content stage must run");
        _structuredExtractionBytes.Should().Equal(
            pdfBytes,
            "the extractor must receive a readable local file holding the uploaded PDF, not a bucket key");
    }

    /// <summary>
    /// Issue #3846: an object that is not in the bucket must be reported as such. Before the fix the
    /// upload pipeline fell back to <c>new FileStream(filePath)</c> without checking, and under S3
    /// <c>FilePath</c> is an object key: the recorded error read "Could not find a part of the path
    /// '/app/pdfs/{gameId}/…'" — a location that never existed — and the NULL error category made
    /// <c>RetryFailedPdfsJob</c> retry an object that cannot come back.
    /// </summary>
    [Fact(Timeout = 90000)]
    public async Task ProcessPdfAsync_WhenTheObjectNeverReachedTheBucket_FailsAsStorageObjectMissing()
    {
        _simulateObjectNeverStored = true;

        var handler = _serviceProvider!.GetRequiredService<UploadPdfCommandHandler>();
        var testUser = await _dbContext!.Users.FirstAsync(TestCancellationToken);
        var testGame = await _dbContext.SharedGames.FirstAsync(TestCancellationToken);

        var command = new Api.BoundedContexts.DocumentProcessing.Application.Commands.UploadPdfCommand(
            GameId: testGame.Id.ToString(),
            Metadata: null,
            PrivateGameId: null,
            UserId: testUser.Id,
            File: CreateMockFormFile("object-never-stored.pdf", CreateValidPdfBytes(1024)));

        var result = await handler.Handle(command, TestCancellationToken);
        await _inlineBgService!.WaitForAllAsync();

        result.Success.Should().BeTrue("the accept stage still succeeds; the pipeline is what fails");

        var persisted = await _dbContext.PdfDocuments
            .AsNoTracking()
            .FirstAsync(p => p.Id == result.Document!.Id, TestCancellationToken);

        persisted.ProcessingState.Should().Be(
            nameof(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Failed));
        persisted.ErrorCategory.Should().Be(
            nameof(Api.BoundedContexts.DocumentProcessing.Domain.Enums.ErrorCategory.StorageObjectMissing),
            "an absent object does not reappear on retry, so RetryFailedPdfsJob must skip it");
        persisted.ProcessingError.Should().Contain(
            "not found in storage at key",
            "the error must name the bucket key that was looked up, not a filesystem path derived from it");
    }

    /// <summary>
    /// #4443 R1: an upload's background task and another owner (the queue worker, the admin
    /// process-pending, recovery at startup — all start with <see cref="IPdfClaimService"/>) must not
    /// both work on the same document. The task used to check «Pending» and then write
    /// <c>Uploading</c> in a separate step: an owner that claimed the document in between left it
    /// with an aggregate in <c>Extracting</c>, the transition threw, and the task marked
    /// <c>Failed</c> a document someone else was processing.
    /// </summary>
    /// <remarks>
    /// The gate holds the background task at its first read of the aggregate — between the check
    /// and the write in the old code, after its own claim and extraction in the new one — and the
    /// other owner claims while it waits. The assertion is the contract, not the gate's position:
    /// exactly one of the two did the work, and the document did not end <c>Failed</c>.
    /// </remarks>
    [Fact(Timeout = 90000)]
    [Trait("Issue", "4443")]
    public async Task BackgroundTask_AndAnotherClaimant_OnlyOneOfThemOwnsTheDocument()
    {
        var handler = _serviceProvider!.GetRequiredService<UploadPdfCommandHandler>();
        var testUser = await _dbContext!.Users.FirstAsync(TestCancellationToken);
        var testGame = await _dbContext.SharedGames.FirstAsync(TestCancellationToken);
        var command = new Api.BoundedContexts.DocumentProcessing.Application.Commands.UploadPdfCommand(
            GameId: testGame.Id.ToString(),
            Metadata: null,
            PrivateGameId: null,
            UserId: testUser.Id,
            File: CreateMockFormFile("two-owners.pdf", CreateValidPdfBytes(1024)));

        _gate.Arm();
        var result = await handler.Handle(command, TestCancellationToken);
        result.Success.Should().BeTrue(result.Message);
        var pdfId = result.Document!.Id;

        await _gate.Reached.WaitAsync(TimeSpan.FromSeconds(30), TestCancellationToken);
        bool claimedByTheOther;
        using (var scope = _serviceProvider.CreateScope())
        {
            claimedByTheOther = await new RelationalPdfClaimService(scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>())
                .TryClaimPendingAsync(pdfId, TestCancellationToken);
        }

        _gate.Release();
        await _inlineBgService!.WaitForAllAsync();

        var extractionsByTheTask = _extractorMock!.Invocations.Count(i => i.Method.Name.StartsWith("Extract", StringComparison.Ordinal));
        var persisted = await _dbContext.PdfDocuments.AsNoTracking().FirstAsync(p => p.Id == pdfId, TestCancellationToken);
        ((claimedByTheOther ? 1 : 0) + extractionsByTheTask).Should().Be(1,
            $"exactly one owner works on the document (other claimed: {claimedByTheOther}, extractions by the task: {extractionsByTheTask})");
        persisted.ProcessingState.Should().NotBe(
            nameof(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Failed),
            $"the loser must leave the document to its owner. ProcessingError='{persisted.ProcessingError}'");
    }

    // ── #4443 R2: who processes an upload ──────────────────────────────────────────────────────

    /// <summary>
    /// An admin or editor upload is processed by the queue, its only owner (decision A): a job with the
    /// priority received, no background task. The panel used to get a background task here AND enqueue
    /// the same PDF itself, and the job ended Failed («SkippedNotClaimed») while the task worked.
    /// </summary>
    [Fact(Timeout = 90000)]
    [Trait("Issue", "4443")]
    public async Task AdminUpload_IsQueuedWithTheReceivedPriority_AndNoBackgroundTaskRuns()
    {
        var admin = await SeedUserAsync("Admin");

        var result = await UploadAsAsync(admin, "admin-urgent.pdf", priority: "urgent");

        result.Success.Should().BeTrue(result.Message);
        result.ProcessingJobId.Should().NotBeNull();
        _inlineBgService!.Started.Should().Be(0, "the queue owns the processing; no background task");
        var jobs = await JobsForAsync(result.Document!.Id);
        jobs.Should().ContainSingle();
        jobs[0].Id.Should().Be(result.ProcessingJobId!.Value);
        jobs[0].Status.Should().Be(nameof(Api.BoundedContexts.DocumentProcessing.Domain.Enums.JobStatus.Queued));
        jobs[0].Priority.Should().Be((int)Api.BoundedContexts.DocumentProcessing.Domain.Enums.ProcessingPriority.Urgent);
        (await StateOfAsync(result.Document.Id)).Should().Be("Pending", "the worker has not picked it up yet");
    }

    [Fact(Timeout = 90000)]
    [Trait("Issue", "4443")]
    public async Task EditorUpload_WithoutPriority_IsQueuedAtNormal()
    {
        var editor = await SeedUserAsync("Editor");

        var result = await UploadAsAsync(editor, "editor-normal.pdf", priority: null);

        result.Success.Should().BeTrue(result.Message);
        _inlineBgService!.Started.Should().Be(0);
        var jobs = await JobsForAsync(result.Document!.Id);
        jobs.Should().ContainSingle().Which.Priority.Should().Be((int)Api.BoundedContexts.DocumentProcessing.Domain.Enums.ProcessingPriority.Normal);
    }

    /// <summary>The priority is an admin control: a user who sends one gets a 403 (it used to be dropped in silence).</summary>
    [Fact(Timeout = 90000)]
    [Trait("Issue", "4443")]
    public async Task UserUpload_WithAPriority_IsForbidden_AndStoresNothing()
    {
        var user = await _dbContext!.Users.FirstAsync(u => u.Role == "User", TestCancellationToken);

        var act = () => UploadAsAsync(user, "user-urgent.pdf", priority: "urgent");

        await act.Should().ThrowAsync<Api.Middleware.Exceptions.ForbiddenException>();
        (await _dbContext.PdfDocuments.AsNoTracking().CountAsync(p => p.FileName == "user-urgent.pdf", TestCancellationToken)).Should().Be(0);
    }

    /// <summary>
    /// An interactive admin upload skips the queue cap (decision 1a): a bulk reindex filling the queue must
    /// not refuse it.
    /// </summary>
    [Fact(Timeout = 90000)]
    [Trait("Issue", "4443")]
    public async Task AdminUpload_WithAFullQueue_IsStillQueued()
    {
        var admin = await SeedUserAsync("Admin");
        await FillTheQueueAsync(admin.Id);

        var result = await UploadAsAsync(admin, "admin-full-queue.pdf", priority: null);

        result.Success.Should().BeTrue(result.Message);
        (await JobsForAsync(result.Document!.Id)).Should().ContainSingle();
    }

    /// <summary>A user's upload is still processed by its background task — with no job beside it.</summary>
    [Fact(Timeout = 90000)]
    [Trait("Issue", "4443")]
    public async Task UserUpload_IsProcessedByTheBackgroundTask_WithNoJob()
    {
        var user = await _dbContext!.Users.FirstAsync(u => u.Role == "User", TestCancellationToken);

        var result = await UploadAsAsync(user, "user-normal.pdf", priority: null);
        await _inlineBgService!.WaitForAllAsync();

        result.Success.Should().BeTrue(result.Message);
        result.ProcessingJobId.Should().BeNull();
        _inlineBgService.Started.Should().Be(1);
        (await JobsForAsync(result.Document!.Id)).Should().BeEmpty();
        (await StateOfAsync(result.Document.Id)).Should().Be("Ready");
    }

    private async Task<UserEntity> SeedUserAsync(string role)
    {
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@test.com",
            DisplayName = $"{role} #4443",
            Role = role,
            Tier = "Free",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext!.Users.Add(user);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
        return user;
    }

    private async Task<Api.BoundedContexts.DocumentProcessing.Application.DTOs.PdfUploadResult> UploadAsAsync(
        UserEntity user, string fileName, string? priority)
    {
        using var scope = _serviceProvider!.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<UploadPdfCommandHandler>();
        var game = await _dbContext!.SharedGames.AsNoTracking().FirstAsync(TestCancellationToken);
        // Distinct bytes per upload: the content-hash dedup would otherwise return the first document.
        var bytes = CreateValidPdfBytes(1024);
        System.Text.Encoding.ASCII.GetBytes(fileName).CopyTo(bytes, 16);
        return await handler.Handle(
            new Api.BoundedContexts.DocumentProcessing.Application.Commands.UploadPdfCommand(
                GameId: game.Id.ToString(),
                Metadata: null,
                PrivateGameId: null,
                UserId: user.Id,
                File: CreateMockFormFile(fileName, bytes),
                Priority: priority),
            TestCancellationToken);
    }

    private async Task<List<Api.Infrastructure.Entities.DocumentProcessing.ProcessingJobEntity>> JobsForAsync(Guid pdfId) =>
        await _dbContext!.ProcessingJobs.AsNoTracking().Where(j => j.PdfDocumentId == pdfId).ToListAsync(TestCancellationToken);

    private async Task<string> StateOfAsync(Guid pdfId) =>
        await _dbContext!.PdfDocuments.AsNoTracking().Where(p => p.Id == pdfId).Select(p => p.ProcessingState).FirstAsync(TestCancellationToken);

    private async Task FillTheQueueAsync(Guid userId)
    {
        var game = await _dbContext!.SharedGames.AsNoTracking().FirstAsync(TestCancellationToken);
        for (var i = 0; i < Api.BoundedContexts.DocumentProcessing.Domain.Entities.ProcessingJob.MaxQueueSize; i++)
        {
            var pdfId = Guid.NewGuid();
            _dbContext.PdfDocuments.Add(new PdfDocumentEntity
            {
                Id = pdfId,
                SharedGameId = game.Id,
                FileName = $"filler-{i}.pdf",
                FilePath = $"pdfs/{game.Id}/{pdfId:N}_filler-{i}.pdf",
                FileSizeBytes = 1,
                ContentType = "application/pdf",
                UploadedByUserId = userId,
                UploadedAt = DateTime.UtcNow,
            });
            _dbContext.ProcessingJobs.Add(new Api.Infrastructure.Entities.DocumentProcessing.ProcessingJobEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = pdfId,
                UserId = userId,
                Status = "Queued",
                Priority = 0,
                CreatedAt = DateTimeOffset.UtcNow,
                MaxRetries = 3,
            });
        }

        await _dbContext.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();
    }

    /// <summary>Holds the first <c>GetByIdAsync</c> made inside a background task, once, while armed.</summary>
    internal sealed class BackgroundTaskGate
    {
        public static readonly AsyncLocal<bool> InBackgroundTask = new();

        private readonly TaskCompletionSource _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _armed;

        public Task Reached => _reached.Task;

        public void Arm() => Interlocked.Exchange(ref _armed, 1);

        public void Release() => _release.TrySetResult();

        public async Task HoldIfArmedAsync(CancellationToken ct)
        {
            if (!InBackgroundTask.Value || Interlocked.Exchange(ref _armed, 0) == 0)
            {
                return;
            }

            _reached.TrySetResult();
            await _release.Task.WaitAsync(ct);
        }
    }

    /// <summary>Delegates everything; <see cref="GetByIdAsync"/> passes through the gate first.</summary>
    internal sealed class GatedPdfDocumentRepository(IPdfDocumentRepository inner, BackgroundTaskGate gate) : IPdfDocumentRepository
    {
        public async Task<Api.BoundedContexts.DocumentProcessing.Domain.Entities.PdfDocument?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            await gate.HoldIfArmedAsync(cancellationToken);
            return await inner.GetByIdAsync(id, cancellationToken);
        }

        public Task<IReadOnlyList<Api.BoundedContexts.DocumentProcessing.Domain.Entities.PdfDocument>> GetAllAsync(CancellationToken cancellationToken = default) => inner.GetAllAsync(cancellationToken);
        public Task AddAsync(Api.BoundedContexts.DocumentProcessing.Domain.Entities.PdfDocument entity, CancellationToken cancellationToken = default) => inner.AddAsync(entity, cancellationToken);
        public Task UpdateAsync(Api.BoundedContexts.DocumentProcessing.Domain.Entities.PdfDocument entity, CancellationToken cancellationToken = default) => inner.UpdateAsync(entity, cancellationToken);
        public Task DeleteAsync(Api.BoundedContexts.DocumentProcessing.Domain.Entities.PdfDocument entity, CancellationToken cancellationToken = default) => inner.DeleteAsync(entity, cancellationToken);
        public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) => inner.ExistsAsync(id, cancellationToken);
        public Task<IReadOnlyList<Api.BoundedContexts.DocumentProcessing.Domain.Entities.PdfDocument>> FindByGameIdAsync(Guid gameId, CancellationToken cancellationToken = default) => inner.FindByGameIdAsync(gameId, cancellationToken);
        public Task<IReadOnlyList<Api.BoundedContexts.DocumentProcessing.Domain.Entities.PdfDocument>> FindByPrivateGameIdAsync(Guid privateGameId, CancellationToken cancellationToken = default) => inner.FindByPrivateGameIdAsync(privateGameId, cancellationToken);
        public Task<IReadOnlyList<Api.BoundedContexts.DocumentProcessing.Domain.Entities.PdfDocument>> FindByStateAsync(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState state, CancellationToken cancellationToken = default) => inner.FindByStateAsync(state, cancellationToken);
        public Task<IReadOnlyList<Api.BoundedContexts.DocumentProcessing.Domain.Entities.PdfDocument>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default) => inner.GetByIdsAsync(ids, cancellationToken);
        public Task<IReadOnlyList<Api.BoundedContexts.DocumentProcessing.Domain.Entities.PdfDocument>> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => inner.FindByUserIdAsync(userId, cancellationToken);
        public Task<bool> ExistsByContentHashAsync(string contentHash, Guid? gameId, Guid? privateGameId, CancellationToken cancellationToken = default) => inner.ExistsByContentHashAsync(contentHash, gameId, privateGameId, cancellationToken);
        public Task<Api.BoundedContexts.DocumentProcessing.Domain.Entities.PdfDocument?> FindCatalogByContentHashAsync(string contentHash, CancellationToken cancellationToken = default) => inner.FindCatalogByContentHashAsync(contentHash, cancellationToken);
        public Task<Api.BoundedContexts.DocumentProcessing.Domain.Entities.PdfDocument?> FindByContentHashForPrivateGameAsync(string contentHash, Guid userId, Guid privateGameId, CancellationToken cancellationToken = default) => inner.FindByContentHashForPrivateGameAsync(contentHash, userId, privateGameId, cancellationToken);
        public Task<Api.BoundedContexts.DocumentProcessing.Domain.Entities.PdfDocument?> FindByContentHashForUserAsync(string contentHash, Guid userId, CancellationToken cancellationToken = default) => inner.FindByContentHashForUserAsync(contentHash, userId, cancellationToken);
    }

    /// <summary>
    /// Test-only IBackgroundTaskService that runs the queued task synchronously
    /// while still letting the caller await all in-flight tasks before assertions.
    /// Mirrors UploadPdfHeadingAwareIntegrationTests.InlineBackgroundTaskService.
    /// </summary>
    internal sealed class InlineBackgroundTaskService : IBackgroundTaskService
    {
        private readonly List<Task> _running = new();

        /// <summary>How many background tasks were started (#4443 R2: none for a queued upload).</summary>
        public int Started { get { lock (_gate) { return _running.Count; } } }
        private readonly Lock _gate = new();

        public void Execute(Func<Task> task)
        {
            ArgumentNullException.ThrowIfNull(task);
            lock (_gate)
            {
                _running.Add(Task.Run(task));
            }
        }

        public void ExecuteWithCancellation(string taskId, Func<CancellationToken, Task> taskFactory)
        {
            ArgumentNullException.ThrowIfNull(taskFactory);
            lock (_gate)
            {
                _running.Add(Task.Run(() =>
                {
                    // #4443: lets BackgroundTaskGate hold the background task and nothing else.
                    BackgroundTaskGate.InBackgroundTask.Value = true;
                    return taskFactory(CancellationToken.None);
                }));
            }
        }

        public bool CancelTask(string taskId) => false;

        public Task WaitForAllAsync()
        {
            Task[] snapshot;
            lock (_gate)
            {
                snapshot = _running.ToArray();
            }
            return Task.WhenAll(snapshot);
        }
    }
}
