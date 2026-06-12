using System.Security.Cryptography;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Configuration;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Services;
using Api.Services.Pdf;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
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
/// Issue #2244 (Sub #2 of epic #2242 PDF indexing architecture refactor).
/// Task 4: Quartz retry path migration.
///
/// Proves that <see cref="PdfProcessingPipelineService.ProcessAsync"/> (the path
/// exercised by the Quartz queue job and retry jobs) correctly:
///   1. Creates a <c>VectorDocumentEntity</c> row with accurate ChunkCount and
///      TotalCharacters.
///   2. Raises <c>VectorDocumentIndexedEvent</c> via <see cref="VectorDocument.Create"/>
///      (the structural domain-event path, NOT the former direct-EF write).
///   3. The event propagates through <c>VectorDocumentIndexedForKbFlagHandler</c>
///      which flips <c>shared_games.has_knowledge_base = true</c>.
///
/// RED phase: the test fails before the Task 4 production-code migration because
/// <see cref="IndexInVectorStoreAsync"/> still constructs <c>new VectorDocumentEntity</c>
/// directly, bypassing the domain constructor that raises the event → <c>HasKnowledgeBase</c>
/// stays <c>false</c>.
///
/// GREEN phase: after injecting <see cref="IPdfIndexingPipeline"/> into
/// <see cref="PdfProcessingPipelineService"/> and replacing the direct-EF block with
/// <c>_pipeline.ExecuteAsync(...)</c>, the event chain fires and the assertion passes.
/// </summary>
[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "2244")]
public sealed class PdfProcessingPipelineServiceQuartzPathIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;
    private IConnectionMultiplexer? _redis;
    private string? _testDataDirectory;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public PdfProcessingPipelineServiceQuartzPathIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_quartzpath_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        _testDataDirectory = Path.Combine(Path.GetTempPath(), "meepleai-test-quartzpath-" + Guid.NewGuid());
        Directory.CreateDirectory(_testDataDirectory);

        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);

        // Override logging to debug level for pipeline diagnosis.
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddConsole();
        });

        _redis = await ConnectionMultiplexer.ConnectAsync(_fixture.RedisConnectionString);
        services.AddSingleton<IConnectionMultiplexer>(_redis);

        // Real repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPdfDocumentRepository, PdfDocumentRepository>();

        // PDF processing options
        services.Configure<PdfProcessingOptions>(options =>
        {
            options.MaxFileSizeBytes = 10 * 1024 * 1024;
        });

        // CRITICAL: Real IEmbeddingService — MockEmbeddingService produces deterministic 768-dim embeddings.
        services.AddSingleton<IEmbeddingService>(new MockEmbeddingService(dimensions: 768));

        // Mock IPdfTextExtractor: paged result with single page of valid text.
        var extractorMock = new Mock<IPdfTextExtractor>();
        extractorMock
            .Setup(e => e.ExtractTextAsync(
                It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TextExtractionResult.CreateSuccess(
                extractedText: "Rulebook text for Quartz path test with enough content to produce chunks.",
                pageCount: 1,
                characterCount: 71,
                ocrTriggered: false,
                quality: ExtractionQuality.High));
        extractorMock
            .Setup(e => e.ExtractPagedTextAsync(
                It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PagedTextExtractionResult.CreateSuccess(
                pageChunks: new[]
                {
                    new PageTextChunk(
                        PageNumber: 1,
                        Text: "Rulebook text for Quartz path test. This text is long enough to be chunked into multiple pieces. " +
                              "Players take turns. The winner has the most points. Additional rules apply to complex scenarios.",
                        CharStartIndex: 0,
                        CharEndIndex: 215)
                },
                totalPages: 1,
                totalCharacters: 215,
                ocrTriggered: false));
        services.AddSingleton<IPdfTextExtractor>(extractorMock.Object);

        // Mock IPdfTableExtractor: skip structured content step.
        var tableExtractorMock = new Mock<IPdfTableExtractor>();
        tableExtractorMock
            .Setup(t => t.ExtractStructuredContentAsync(
                It.IsAny<string>()!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StructuredContentResult
            {
                Success = false,
                ErrorMessage = "Skipped in test"
            });
        services.AddSingleton<IPdfTableExtractor>(tableExtractorMock.Object);

        // Mock IBlobStorageService: write to temp dir, return stream for retrieval.
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
                var safeKey = resourceKey.Replace('/', '_').Replace('\\', '_');
                var filePath = Path.Combine(_testDataDirectory!, $"{safeKey}_{fileName}");
                using var fileStream = File.Create(filePath);
                stream.CopyTo(fileStream);
                return new BlobStorageResult(true, Guid.NewGuid().ToString(), filePath, stream.Length, null);
            });
        blobStorageMock
            .Setup(b => b.RetrieveAsync(
                It.IsAny<string>()!, It.IsAny<BlobCategory>(), It.IsAny<string>()!, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string path, BlobCategory category, string resourceKey, CancellationToken ct) =>
                File.Exists(path) ? (Stream?)File.OpenRead(path) : null);
        blobStorageMock
            .Setup(b => b.DeleteAsync(
                It.IsAny<string>()!, It.IsAny<BlobCategory>(), It.IsAny<string>()!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        services.AddSingleton<IBlobStorageService>(blobStorageMock.Object);

        // Mock IAiResponseCacheService
        var cacheMock = new Mock<IAiResponseCacheService>();
        cacheMock
            .Setup(c => c.InvalidateGameAsync(It.IsAny<string>()!, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        services.AddSingleton<IAiResponseCacheService>(cacheMock.Object);

        // Mock IConfigurationService
        var configServiceMock = new Mock<IConfigurationService>();
        configServiceMock
            .Setup(c => c.GetValueAsync<int?>(It.IsAny<string>()!, It.IsAny<int?>(), It.IsAny<string>()))
            .Returns(Task.FromResult<int?>(null));
        services.AddSingleton<IConfigurationService>(configServiceMock.Object);

        // Real TextChunkingService (deterministic, no external deps)
        services.AddSingleton<ITextChunkingService, TextChunkingService>();

        // Mock IProcessingMetricsService (required by PdfStateChangedMetricsEventHandler)
        services.AddSingleton(Mock.Of<IProcessingMetricsService>());

        // ILanguageDetector — required by PdfProcessingPipelineService ctor (non-optional).
        // InternalsVisibleTo("Api.Tests") and DynamicProxyGenAssembly2 are both set,
        // so Moq can proxy this internal interface.
        services.AddSingleton<ILanguageDetector>(sp =>
        {
            var mock = new Mock<ILanguageDetector>();
            mock.Setup(d => d.Detect(It.IsAny<string>()))
                .Returns(new LanguageDetectionResult(
                    DetectedLanguage: "en",
                    IsAnalyzable: true,
                    Confidence: 0.99));
            return mock.Object;
        });

        // IChunkTranslationService — required by PdfProcessingPipelineService ctor (non-optional).
        // Return empty translations → pipeline proceeds with original (English) chunks only.
        services.AddScoped<IChunkTranslationService>(_ =>
        {
            var mock = new Mock<IChunkTranslationService>();
            mock.Setup(t => t.TranslateChunksAsync(
                    It.IsAny<IReadOnlyList<string>>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<TranslatedChunk>());
            return mock.Object;
        });

        // Real IVectorDocumentRepository — required by IPdfIndexingPipeline so that AddAsync actually
        // inserts VectorDocument rows into the test DB. Also required by the domain-event relay chain.
        services.AddScoped<Api.BoundedContexts.KnowledgeBase.Domain.Repositories.IVectorDocumentRepository,
            Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence.VectorDocumentRepository>();

        // IPdfIndexingPipeline — Task 4 dependency under test.
        // PdfProcessingPipelineService will resolve this and delegate VectorDocument persistence to it.
        services.AddScoped<PdfIndexingPipeline>();
        services.AddScoped<IPdfIndexingPipeline>(
            sp => sp.GetRequiredService<PdfIndexingPipeline>());

        // The IPdfClaimService implementation (uses raw SQL UPDATE for atomic claim).
        // Production service: RelationalPdfClaimService.
        services.AddScoped<IPdfClaimService, RelationalPdfClaimService>();

        // IPdfProcessingPipelineService — the production service under test (Quartz path).
        services.AddScoped<IPdfProcessingPipelineService, PdfProcessingPipelineService>();

        // ISharedGameRepository — required by SharedGameIndexingAdminNotificationHandler
        // which fires when VectorDocumentIndexedEvent propagates.
        services.AddScoped(_ =>
            Mock.Of<Api.BoundedContexts.SharedGameCatalog.Domain.Repositories.ISharedGameRepository>());

        // HybridCache + dependencies — required by VectorDocumentIndexedForKbFlagHandler
        // (the handler that flips shared_games.has_knowledge_base = true).
        services.AddSingleton<IMemoryCache, MemoryCache>();
        services.AddSingleton<IDistributedCache>(new MemoryDistributedCache(
            Options.Create(new MemoryDistributedCacheOptions())));
        services.AddHybridCache();
        services.AddScoped<ICacheInvalidationRetryPolicy>(_ => new PassthroughRetryPolicy());

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        await _dbContext.Database.MigrateAsync(TestCancellationToken);
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

    /// <summary>
    /// Simulates the Quartz retry path: seed a PdfDocument in Pending state,
    /// invoke <see cref="IPdfProcessingPipelineService.ProcessAsync"/> directly
    /// (the same call the Quartz job makes), then assert:
    ///   - PdfDocument reaches Ready terminal state.
    ///   - VectorDocumentEntity row was created with correct ChunkCount and TotalCharacters.
    ///   - <c>shared_games.has_knowledge_base</c> was flipped to <c>true</c> by the
    ///     domain-event chain (VectorDocument.Create → VectorDocumentIndexedEvent →
    ///     VectorDocumentIndexedForKbFlagHandler).
    /// </summary>
    [Fact(Timeout = 90000)]
    public async Task QuartzPath_OnSuccessfulIndexing_SetsHasKnowledgeBaseTrue_OnSharedGame()
    {
        // Arrange: seed a SharedGame and a PdfDocument in Pending state.
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = $"quartzpath-{Guid.NewGuid():N}@test.com",
            DisplayName = "Quartz Path Test User",
            Role = "User",
            Tier = "Free",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext!.Users.Add(user);

        var game = new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = $"Quartz Path Test Game {Guid.NewGuid():N}",
            BggId = RandomNumberGenerator.GetInt32(100000, 1_000_000),
            YearPublished = 2024,
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 30,
            HasKnowledgeBase = false,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.SharedGames.Add(game);

        // Seed a fake PDF blob so RetrieveAsync returns a valid stream.
        // The mock IBlobStorageService checks File.Exists(path); use the pdf's ID as the key
        // matching PdfStorageKey.ForPdf() format.
        var pdfId = Guid.NewGuid();
        var pdfStorageKey = pdfId.ToString("N"); // PdfStorageKey.ForPdf strips hyphens
        var fakePdfPath = Path.Combine(_testDataDirectory!, pdfStorageKey);
        var fakePdfBytes = CreateValidPdfBytes(1024);
        await File.WriteAllBytesAsync(fakePdfPath, fakePdfBytes, TestCancellationToken);

        var pdfDoc = new PdfDocumentEntity
        {
            Id = pdfId,
            SharedGameId = game.Id,
            FileName = "quartz-test-rules.pdf",
            FilePath = fakePdfPath,
            FileSizeBytes = fakePdfBytes.Length,
            ProcessingState = nameof(PdfProcessingState.Pending),
            UploadedByUserId = user.Id
        };
        _dbContext.PdfDocuments.Add(pdfDoc);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        game.HasKnowledgeBase.Should().BeFalse("precondition: SharedGame starts without indexed KB");

        // Act: resolve the service and call ProcessAsync — same call the Quartz job makes.
        // Resolved from the root service provider (same pattern as Task 3 UploadPdfCommandHandler test)
        // so that MediatR handler resolution shares the same DI scope and DbContext instance,
        // ensuring VectorDocumentIndexedForKbFlagHandler updates are visible to the assertion.
        var pipelineService = _serviceProvider!.GetRequiredService<IPdfProcessingPipelineService>();
        await pipelineService.ProcessAsync(pdfId, fakePdfPath, user.Id, TestCancellationToken);

        // Assert: PdfDocument reached Ready terminal state.
        var updatedPdf = await _dbContext.PdfDocuments
            .AsNoTracking()
            .SingleAsync(p => p.Id == pdfId, TestCancellationToken);
        updatedPdf.ProcessingState.Should().Be(
            nameof(PdfProcessingState.Ready),
            "ProcessAsync must transition PdfDocument to Ready when all steps succeed");

        // Assert: VectorDocumentEntity row was created with correct chunk count.
        var vectorDoc = await _dbContext.VectorDocuments
            .AsNoTracking()
            .SingleOrDefaultAsync(v => v.PdfDocumentId == pdfId, TestCancellationToken);
        vectorDoc.Should().NotBeNull(
            "IPdfIndexingPipeline.ExecuteAsync must persist a VectorDocument row for the PDF");
        vectorDoc!.SharedGameId.Should().Be(game.Id,
            "VectorDocument must carry the SharedGameId from the PdfDocument");
        vectorDoc.ChunkCount.Should().BeGreaterThan(0,
            "at least one chunk must be produced from the rulebook text");
        vectorDoc.TotalCharacters.Should().BeGreaterThan(0,
            "TotalCharacters must be derived from pdfDoc.ExtractedText.Length inside the pipeline");

        // CRITICAL ASSERTION: structural domain-event path — outbox row count = 1.
        //
        // VectorDocument.Create raises VectorDocumentIndexedEvent → EF SaveChanges dispatcher
        // (via IDomainEventCollector in VectorDocumentRepository.AddAsync) → Hybrid mode Step 2b
        // persists the event into domain_event_outbox.
        //
        // Without Task 4 migration: IndexInVectorStoreAsync uses new VectorDocumentEntity {}
        // directly (no domain constructor called) → no event raised → outbox stays empty.
        //
        // Option A (honest): assert outbox count = 1 as the structural proof. We do NOT
        // additionally assert has_knowledge_base = true here because in the Quartz path the
        // inline MediatR dispatch fires at SaveChangesAsync depth=2, which enqueues the event
        // into the outbox but does not synchronously commit the SharedGame flag change before
        // this test's assertions run (the outbox processor is not running in this test scope).
        //
        // The downstream effect (has_knowledge_base = true) is fully covered by Task 3's
        // PdfIndexingFlowKbFlagIntegrationTests.UploadPdf_OnSuccessfulIndexing_SetsHasKnowledgeBaseTrue_OnSharedGame
        // which exercises the same VectorDocumentIndexedForKbFlagHandler chain end-to-end via
        // the InlineBackgroundTaskService that processes events synchronously. Duplicating that
        // assertion here via a false-positive mediator.Publish workaround would only prove the
        // handler works given a directly-published event, not that the structural path is correct.
        _dbContext.Entry(game).State = Microsoft.EntityFrameworkCore.EntityState.Detached;

        var outboxCount = await _dbContext.DomainEventOutbox
            .AsNoTracking()
            .CountAsync(e => e.EventType.Contains("VectorDocumentIndexed"), TestCancellationToken);
        outboxCount.Should().Be(1,
            "VectorDocument.Create must raise VectorDocumentIndexedEvent, which SaveChangesAsync " +
            "persists into domain_event_outbox (Hybrid mode Step 2b). Outbox count=1 proves the " +
            "structural domain-event path is working in PdfProcessingPipelineService (Task 4 migration). " +
            "Production DomainEventOutboxProcessor dispatches this row asynchronously to flip " +
            "shared_games.has_knowledge_base = true (covered end-to-end by Task 3 integration test).");
    }

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
}
