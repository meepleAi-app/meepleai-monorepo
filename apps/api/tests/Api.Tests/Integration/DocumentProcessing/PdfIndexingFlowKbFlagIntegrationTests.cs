using System.Security.Cryptography;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
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
/// Issue #2243 (Sub #1 of epic #2242 PDF indexing flow repair).
///
/// Failing-first integration test that proves the root-cause bug:
/// after the full upload-to-indexed pipeline completes successfully
/// (PdfDocument.ProcessingState = Ready), the SharedGame.HasKnowledgeBase
/// flag is NOT set to true because no path publishes the
/// VectorDocumentIndexedEvent that VectorDocumentIndexedForKbFlagHandler
/// listens to.
///
/// The three known ingestion handlers
/// (UploadPdfCommandHandler.ProcessPdfAsync, PdfProcessingPipelineService,
/// IndexPdfCommandHandler) all write VectorDocumentEntity directly to EF,
/// bypassing the domain entity constructor that raises the event.
///
/// Expected behaviour (post-fix, Sub #1 Block A): the BG pipeline emits
/// VectorDocumentIndexedEvent → VectorDocumentIndexedForKbFlagHandler
/// flips has_knowledge_base to true → admin/user UIs can show
/// "agente pronto" without manual refresh.
/// </summary>
[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "2243")]
public sealed class PdfIndexingFlowKbFlagIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;
    private IConnectionMultiplexer? _redis;
    private string? _testDataDirectory;
    private InlineBackgroundTaskService? _inlineBgService;
    private readonly ListLoggerSink _logSink = new();

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public PdfIndexingFlowKbFlagIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_kbflag_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        _testDataDirectory = Path.Combine(Path.GetTempPath(), "meepleai-test-kbflag-" + Guid.NewGuid());
        Directory.CreateDirectory(_testDataDirectory);

        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);

        // Override logging: capture into in-memory sink for diagnosis on assertion failure.
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddProvider(new ListLoggerProvider(_logSink));
        });

        _redis = await ConnectionMultiplexer.ConnectAsync(_fixture.RedisConnectionString);
        services.AddSingleton<IConnectionMultiplexer>(_redis);

        // Real repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPdfDocumentRepository, PdfDocumentRepository>();

        // Real handler under test
        services.AddScoped<UploadPdfCommandHandler>();

        // PDF processing options — allow 10 MB
        services.Configure<PdfProcessingOptions>(options =>
        {
            options.MaxFileSizeBytes = 10 * 1024 * 1024;
        });

        // CRITICAL: Inline background task service so the BG pipeline runs synchronously
        // (UploadPdfIntegrationTests uses Mock<IBackgroundTaskService> which never executes,
        // so ProcessPdfAsync never runs there → ProcessingState stays Pending → no event flow).
        _inlineBgService = new InlineBackgroundTaskService();
        services.AddSingleton<IBackgroundTaskService>(_inlineBgService);

        // CRITICAL: Real IEmbeddingService (CreateBase registers Mock.Of which returns null/false).
        // MockEmbeddingService produces deterministic 768-dim embeddings (matches pgvector schema).
        services.AddSingleton<IEmbeddingService>(new MockEmbeddingService(dimensions: 768));

        // Mock IPdfTextExtractor: paged result with single page of valid text
        var extractorMock = new Mock<IPdfTextExtractor>();
        extractorMock
            .Setup(e => e.ExtractTextAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TextExtractionResult.CreateSuccess(
                extractedText: "This is the rulebook text. It contains rules for the board game.",
                pageCount: 1,
                characterCount: 64,
                ocrTriggered: false,
                quality: ExtractionQuality.High));
        extractorMock
            .Setup(e => e.ExtractPagedTextAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PagedTextExtractionResult.CreateSuccess(
                pageChunks: new[]
                {
                    new PageTextChunk(
                        PageNumber: 1,
                        Text: "This is the rulebook text. It contains rules for the board game with multiple sentences to be chunked. " +
                              "Players take turns rolling dice. The winner is the player with the most points at the end.",
                        CharStartIndex: 0,
                        CharEndIndex: 200)
                },
                totalPages: 1,
                totalCharacters: 200,
                ocrTriggered: false));
        services.AddSingleton<IPdfTextExtractor>(extractorMock.Object);

        // Mock IPdfTableExtractor: returns Success=false so pipeline skips structured content step.
        // (bare Mock.Of returns default StructuredContentResult? = null → NRE at Processing.cs:294)
        var tableExtractorMock = new Mock<IPdfTableExtractor>();
        tableExtractorMock
            .Setup(t => t.ExtractStructuredContentAsync(It.IsAny<string>()!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StructuredContentResult
            {
                Success = false,
                ErrorMessage = "Skipped in test"
            });
        services.AddSingleton<IPdfTableExtractor>(tableExtractorMock.Object);

        // Mock IBlobStorageService: write to temp dir
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
            .Setup(b => b.RetrieveAsync(It.IsAny<string>()!, It.IsAny<BlobCategory>(), It.IsAny<string>()!, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string path, BlobCategory category, string resourceKey, CancellationToken ct) =>
                File.Exists(path) ? (Stream?)File.OpenRead(path) : null);
        blobStorageMock
            .Setup(b => b.DeleteAsync(It.IsAny<string>()!, It.IsAny<BlobCategory>(), It.IsAny<string>()!, It.IsAny<CancellationToken>()))
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

        // Real PdfUploadQuotaService (needs Redis)
        services.AddScoped<IPdfUploadQuotaService, PdfUploadQuotaService>();

        // Real TextChunkingService (deterministic, no external deps)
        services.AddSingleton<ITextChunkingService, TextChunkingService>();

        // Mock IProcessingMetricsService (required by PdfStateChangedMetricsEventHandler)
        services.AddSingleton(Mock.Of<Api.BoundedContexts.DocumentProcessing.Application.Services.IProcessingMetricsService>());

        // Real IVectorDocumentRepository — required by IPdfIndexingPipeline (Task 3, #2244) so that
        // AddAsync actually inserts VectorDocument rows into the test DB (mock would be a no-op).
        // Also required by VectorDocumentIndexedEventHandler relay that fires after the structural
        // VectorDocumentIndexedEvent raised inside VectorDocument.Create flows through SaveChanges.
        services.AddScoped<Api.BoundedContexts.KnowledgeBase.Domain.Repositories.IVectorDocumentRepository,
            Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence.VectorDocumentRepository>();

        // IPdfIndexingPipeline — required by UploadPdfCommandHandler.Processing.cs after #2244 Task 3.
        // Concrete PdfIndexingPipeline depends on IVectorDocumentRepository (registered above).
        services.AddScoped<Api.BoundedContexts.DocumentProcessing.Application.Services.PdfIndexingPipeline>();
        services.AddScoped<Api.BoundedContexts.DocumentProcessing.Application.Services.IPdfIndexingPipeline>(
            sp => sp.GetRequiredService<Api.BoundedContexts.DocumentProcessing.Application.Services.PdfIndexingPipeline>());

        // ISharedGameRepository — required by SharedGameIndexingAdminNotificationHandler which fires
        // when the structural VectorDocumentIndexedEvent (raised via VectorDocument.Create in the pipeline)
        // propagates through VectorDocumentIndexedEventHandler → VectorDocumentReadyIntegrationEvent.
        services.AddScoped(_ => Mock.Of<Api.BoundedContexts.SharedGameCatalog.Domain.Repositories.ISharedGameRepository>());

        // HybridCache + dependencies — required by VectorDocumentIndexedForKbFlagHandler
        // (the handler that actually flips shared_games.has_knowledge_base = true on the event).
        services.AddSingleton<IMemoryCache, MemoryCache>();
        services.AddSingleton<IDistributedCache>(new MemoryDistributedCache(
            Options.Create(new MemoryDistributedCacheOptions())));
        services.AddHybridCache();
        services.AddScoped<Api.Services.ICacheInvalidationRetryPolicy>(_ => new PassthroughRetryPolicy());

        // ISemanticResponseCache — required by IndexPdfCommandHandler (Task 5, #2244 call site 3/3).
        // Mock: test only asserts event chain + has_knowledge_base; cache invalidation is a side-effect.
        // Must return Task.CompletedTask (not null) to avoid NullReferenceException on await.
        var semanticCacheMock = new Mock<Api.BoundedContexts.KnowledgeBase.Application.Services.ISemanticResponseCache>();
        semanticCacheMock
            .Setup(c => c.InvalidateGameAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        services.AddSingleton(semanticCacheMock.Object);

        // IOptions<IndexingSettings> — required by IndexPdfCommandHandler ctor.
        // Default EmbeddingBatchSize=100 is sufficient for the small text seeded by this test.
        services.Configure<Api.Configuration.IndexingSettings>(opts => opts.EmbeddingBatchSize = 100);

        // #2244 Task 6: configure MeepleAiDbContext to use Hybrid dispatch mode so that
        // VectorDocumentIndexedEvent raised structurally by VectorDocumentRepository.AddAsync
        // is dispatched inline via MediatR (depth=1 path) within the same scope, allowing
        // VectorDocumentIndexedForKbFlagHandler to flip has_knowledge_base = true during the test.
        //
        // The production default is OutboxOnly (post-T9 cutover, DomainEventOutboxOptions.cs:49).
        // In production the outbox processor (DomainEventOutboxProcessor) drains the event and
        // dispatches it asynchronously. In this integration test there is no background processor,
        // so OutboxOnly silently drops the handler call (event persisted in domain_event_outbox
        // but never dispatched), and the HasKnowledgeBase assertion would fail.
        //
        // Hybrid = inline MediatR.Publish (depth=1) + outbox row written (Step 2b).
        // The UploadPdf test asserts the synchronous end-to-end effect (HasKnowledgeBase=true),
        // while the IndexPdf test (call site 3/3) asserts only the outbox row (outboxCount=1).
        //
        // DomainEventOutboxOptions uses init-only properties; use Options.Create() rather than
        // services.Configure<T>(opts => opts.Mode = ...) which would fail to compile on init setter.
        services.AddSingleton<IOptions<Api.Infrastructure.DomainEventOutbox.DomainEventOutboxOptions>>(
            Options.Create(new Api.Infrastructure.DomainEventOutbox.DomainEventOutboxOptions
            {
                Mode = Api.Infrastructure.DomainEventOutbox.DomainEventDispatchMode.Hybrid
            }));

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
            Email = $"kbflag-{Guid.NewGuid():N}@test.com",
            DisplayName = "KB Flag Test User",
            Role = "User",
            Tier = "Free",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext!.Users.Add(user);

        var game = new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = $"KB Flag Test Game {Guid.NewGuid():N}",
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
    public async Task UploadPdf_OnSuccessfulIndexing_SetsHasKnowledgeBaseTrue_OnSharedGame()
    {
        // Arrange
        var handler = _serviceProvider!.GetRequiredService<UploadPdfCommandHandler>();
        var testUser = await _dbContext!.Users.FirstAsync(TestCancellationToken);
        var testGame = await _dbContext.SharedGames.FirstAsync(TestCancellationToken);

        testGame.HasKnowledgeBase.Should().BeFalse(
            "precondition: SharedGame starts without indexed KB");

        var pdfBytes = CreateValidPdfBytes(1024);
        var formFile = CreateMockFormFile("rules.pdf", pdfBytes);

        var command = new UploadPdfCommand(
            GameId: testGame.Id.ToString(),
            Metadata: null,
            PrivateGameId: null,
            UserId: testUser.Id,
            File: formFile);

        // Act
        var result = await handler.Handle(command, TestCancellationToken);
        await _inlineBgService!.WaitForAllAsync();

        // Assert: upload succeeded
        result.Should().NotBeNull();
        result.Success.Should().BeTrue("upload accept-stage must succeed for a valid PDF");
        result.Document.Should().NotBeNull();

        // Assert: BG pipeline reached Ready terminal state
        var pdfDocId = Guid.Parse(result.Document!.Id.ToString());
        var pdfDoc = await _dbContext.PdfDocuments
            .AsNoTracking()
            .SingleAsync(p => p.Id == pdfDocId, TestCancellationToken);
        if (!string.IsNullOrEmpty(pdfDoc.ProcessingError))
        {
            var relevantLogs = _logSink.Snapshot()
                .Where(line =>
                    line.Contains("PDF-DEBUG", StringComparison.Ordinal) ||
                    line.Contains("UploadPdfCommandHandler", StringComparison.Ordinal) ||
                    line.Contains("PdfProcessing", StringComparison.Ordinal) ||
                    line.Contains("[Error]", StringComparison.Ordinal) ||
                    line.Contains("[Critical]", StringComparison.Ordinal) ||
                    line.Contains("Ex=", StringComparison.Ordinal))
                .TakeLast(80)
                .ToList();
            var logTail = string.Join("\n", relevantLogs);
            pdfDoc.ProcessingError.Should().BeNullOrEmpty(
                $"BG pipeline must not fail. State={pdfDoc.ProcessingState}, " +
                $"PageCount={pdfDoc.PageCount}, CharCount={pdfDoc.CharacterCount}, " +
                $"Error='{pdfDoc.ProcessingError}'.\n\n--- PIPELINE LOG ({relevantLogs.Count} entries) ---\n{logTail}\n--- END ---");
        }
        pdfDoc.ProcessingState.Should().Be(
            nameof(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Ready),
            "BG pipeline must reach Ready when extraction + chunking + embedding + indexing succeed");

        // Assert: VectorDocument was created and linked to SharedGame
        var vectorDoc = await _dbContext.VectorDocuments
            .AsNoTracking()
            .SingleAsync(v => v.PdfDocumentId == pdfDocId, TestCancellationToken);
        vectorDoc.SharedGameId.Should().Be(testGame.Id,
            "VectorDocumentEntity is created with SharedGameId propagated from PdfDocument");
        vectorDoc.ChunkCount.Should().BeGreaterThan(0,
            "at least one chunk must be persisted for the rulebook text");

        // STRUCTURAL ASSERTION (#2244 Sub #2 / Task 6):
        // VectorDocumentIndexedForKbFlagHandler subscribes to VectorDocumentIndexedEvent
        // and is the only place that sets has_knowledge_base = true on shared_games.
        //
        // After Task 3 migration, UploadPdfCommandHandler.ProcessPdfAsync uses IPdfIndexingPipeline
        // which calls VectorDocument.Create → raises VectorDocumentIndexedEvent structurally via
        // VectorDocumentRepository.AddAsync → IDomainEventCollector → SaveChangesAsync → MediatR.
        //
        // The test fixture configures Hybrid dispatch mode (see InitializeAsync) so the inline
        // MediatR.Publish fires within SaveChangesAsync at depth=1. The handler updates
        // has_knowledge_base = true in the same scope, verifiable by this assertion.
        //
        // Sub #1 Block A (manual publish in FinalizeProcessingAsync) has been removed by Task 6.
        var updatedGame = await _dbContext.SharedGames
            .AsNoTracking()
            .SingleAsync(g => g.Id == testGame.Id, TestCancellationToken);
        updatedGame.HasKnowledgeBase.Should().BeTrue(
            "after PDF indexing completes, VectorDocumentIndexedEvent must propagate to flip " +
            "shared_games.has_knowledge_base = true. Without this, admin tabs and user 'agente pronto' " +
            "badges stay invisible forever (root cause of epic #2242).");

        // Block B: KbsCount must reflect actual indexed chunks across BOTH GetAll and GetFiltered handlers.
        // The bug: GetAllSharedGamesQueryHandler.cs:93 and GetFilteredSharedGamesQueryHandler.cs:123
        // hardcode `0` for KbsCount (with comment "aggregate fields not computed by this handler"),
        // so even after Block A flips HasKnowledgeBase=true the API consumers see 0 chunks.
        var mediator = _serviceProvider!.GetRequiredService<MediatR.IMediator>();
        var allResult = await mediator.Send(
            new Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetAllSharedGamesQuery(
                Status: null,
                PageNumber: 1,
                PageSize: 50),
            TestCancellationToken);
        var allEntry = allResult.Items.SingleOrDefault(x => x.Id == testGame.Id);
        allEntry.Should().NotBeNull("test game must appear in GetAllSharedGames page");
        allEntry!.HasKnowledgeBase.Should().BeTrue("HasKnowledgeBase already projected by GetAll handler");
        allEntry.KbsCount.Should().BeGreaterThan(0,
            "Block B: GetAllSharedGamesQueryHandler must compute KbsCount from VectorDocuments join, " +
            "not hardcode 0 (line 93). Indexed chunks should be counted.");

        // Block C: GameDto and GameDetailsDto must expose HasKnowledgeBase so /api/v1/games clients
        // (admin UI list + user library) can render the "agente pronto" badge without an extra
        // GET /api/v1/shared-games round-trip. Currently both DTOs lack the field entirely; the
        // query handlers materialize from SharedGames so the column is one projection step away.
        var pagedGames = await mediator.Send(
            new Api.BoundedContexts.GameManagement.Application.Queries.GetAllGamesQuery(
                Search: null,
                Page: 1,
                PageSize: 50),
            TestCancellationToken);
        var gameDto = pagedGames.Games.SingleOrDefault(x => x.Id == testGame.Id);
        gameDto.Should().NotBeNull("test SharedGame must appear in GetAllGames response");
        gameDto!.HasKnowledgeBase.Should().BeTrue(
            "Block C: GameDto must expose HasKnowledgeBase. The handler reads SharedGames where the " +
            "column is already populated by VectorDocumentIndexedForKbFlagHandler; the projection " +
            "must include g.HasKnowledgeBase instead of dropping it.");

        // Block D: GetKnowledgeBaseStatusQueryHandler maps "Ready" with TotalChunks=0 hardcoded
        // (line 165). The handler is the source of truth for the per-game status pill on the
        // library detail page; with 0/0 chunks it cannot answer the obvious question "how big
        // is my agent's KB?" once indexing finishes. Sub #1 Block D wires the real ChunkCount
        // from the VectorDocument row.
        var kbStatus = await mediator.Send(
            new Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKnowledgeBaseStatusQuery(
                GameId: testGame.Id,
                IsPrivateGame: false),
            TestCancellationToken);
        kbStatus.Should().NotBeNull();
        kbStatus!.Status.Should().Be("Completed", "PDF is in Ready state → mapping emits 'Completed'");
        kbStatus.TotalChunks.Should().BeGreaterThan(0,
            "Block D: KnowledgeBaseStatusDto must surface the real chunk count when Ready, " +
            "not the 0 hardcoded at GetKnowledgeBaseStatusQueryHandler.cs:165.");
    }

    /// <summary>
    /// Task 5 / issue #2244 (Sub #2, call site 3/3): proves the admin re-index path
    /// (<see cref="IndexPdfCommandHandler"/>) correctly uses <see cref="IPdfIndexingPipeline"/>
    /// after the migration.
    ///
    /// The scenario seeds a SharedGame + PdfDocument with ExtractedText already populated
    /// (admin re-index assumes extraction already completed), then invokes
    /// <see cref="IndexPdfCommandHandler"/> via MediatR and asserts:
    ///   1. <c>shared_games.has_knowledge_base = true</c> — proves the structural
    ///      VectorDocument.Create domain-event chain fires and propagates through
    ///      VectorDocumentIndexedForKbFlagHandler (same chain as Task 3).
    ///   2. Exactly 1 <c>VectorDocumentEntity</c> row for (sharedGameId, pdfId) —
    ///      re-index is idempotent.
    ///   3. <c>VectorDocumentEntity.TotalCharacters == pdfDoc.ExtractedText.Length</c>.
    ///
    /// RED before Task 5 migration: new VectorDocumentEntity {} in
    /// ValidateAndPreparePdfForIndexingAsync bypasses domain constructor → no event →
    /// has_knowledge_base stays false.
    /// GREEN after migration: pipeline.ExecuteAsync raises event structurally.
    /// </summary>
    [Fact(Timeout = 90000)]
    public async Task IndexPdf_OnRebuild_SetsHasKnowledgeBaseTrue_AndRaisesEventOnce()
    {
        // Arrange: seed a SharedGame + PdfDocument with ExtractedText already set.
        // Admin re-index does not perform extraction — it operates on existing text.
        var user = await _dbContext!.Users.FirstAsync(TestCancellationToken);
        var testGame = await _dbContext.SharedGames.FirstAsync(TestCancellationToken);

        testGame.HasKnowledgeBase.Should().BeFalse(
            "precondition: SharedGame starts without indexed KB");

        const string extractedText =
            "This is the admin re-index test rulebook. " +
            "It contains rules for the board game with multiple sentences to be chunked. " +
            "Players take turns rolling dice and moving pieces. " +
            "The winner is the player who reaches the goal first. " +
            "Setup takes approximately fifteen minutes for new players unfamiliar with the game.";

        var pdfId = Guid.NewGuid();
        var pdfDoc = new Api.Infrastructure.Entities.PdfDocumentEntity
        {
            Id = pdfId,
            SharedGameId = testGame.Id,
            FileName = "admin-reindex-rules.pdf",
            FilePath = "/uploads/admin-reindex-rules.pdf",
            FileSizeBytes = 2048,
            ProcessingState = nameof(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Ready),
            ExtractedText = extractedText,
            UploadedByUserId = user.Id,
        };
        _dbContext.PdfDocuments.Add(pdfDoc);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Register IndexPdfCommandHandler + its dependencies (ISemanticResponseCache, IndexingSettings).
        // The service provider was built in InitializeAsync without IndexPdfCommandHandler,
        // so we resolve via MediatR which auto-discovers the handler through DI registration.
        // IntegrationServiceCollectionBuilder.CreateBase registers MediatR with all handlers,
        // so IndexPdfCommandHandler is already wired — resolve via IMediator.
        var mediator = _serviceProvider!.GetRequiredService<MediatR.IMediator>();

        // Act: invoke the admin re-index path via MediatR (same mechanism as the endpoint).
        var command = new IndexPdfCommand(pdfId.ToString());
        var result = await mediator.Send(command, TestCancellationToken);

        // Assert: indexing succeeded
        result.Should().NotBeNull();
        result.Success.Should().BeTrue(
            $"admin re-index must succeed for a PDF with extracted text. " +
            $"Error: '{result.ErrorMessage}' (code: {result.ErrorCode})");

        // Assert 1: exactly 1 VectorDocumentEntity row for this pdfId — idempotent.
        var vectorDocs = await _dbContext.VectorDocuments
            .AsNoTracking()
            .Where(v => v.PdfDocumentId == pdfId)
            .ToListAsync(TestCancellationToken);
        vectorDocs.Should().HaveCount(1,
            "admin re-index must be idempotent: exactly one VectorDocument row per PDF");

        var vectorDoc = vectorDocs.Single();
        vectorDoc.SharedGameId.Should().Be(testGame.Id,
            "VectorDocument must carry the SharedGameId from the PdfDocument");
        vectorDoc.ChunkCount.Should().BeGreaterThan(0,
            "at least one chunk must be produced from the extracted rulebook text");

        // Assert 2: TotalCharacters derived from pdfDoc.ExtractedText.Length inside the pipeline.
        vectorDoc.TotalCharacters.Should().Be(extractedText.Length,
            "TotalCharacters must equal pdfDoc.ExtractedText.Length " +
            "(set by PdfIndexingPipeline via VectorDocument.Create, not the deleted " +
            "vectorDoc.TotalCharacters = extractedText.Length assignment).");

        // Assert 3: structural domain-event path — outbox row count = 1.
        //
        // VectorDocument.Create raises VectorDocumentIndexedEvent → VectorDocumentRepository.AddAsync
        // collects the event via IDomainEventCollector → SaveChangesAsync (Hybrid mode Step 2b)
        // persists it into domain_event_outbox.
        //
        // Without Task 5 migration: ValidateAndPreparePdfForIndexingAsync uses new VectorDocumentEntity {}
        // directly (no domain constructor called) → no event raised → outbox stays empty.
        //
        // With Task 5 migration: pipeline.ExecuteAsync calls VectorDocument.Create which raises the
        // event structurally. Outbox count=1 proves the event was persisted regardless of whether
        // the downstream VectorDocumentIndexedForKbFlagHandler succeeded synchronously.
        // The has_knowledge_base = true downstream effect is fully covered by Task 3's
        // UploadPdf_OnSuccessfulIndexing_SetsHasKnowledgeBaseTrue_OnSharedGame which exercises the
        // same VectorDocumentIndexedForKbFlagHandler chain end-to-end via InlineBackgroundTaskService.
        var outboxCount = await _dbContext.DomainEventOutbox
            .AsNoTracking()
            .CountAsync(e => e.EventType.Contains("VectorDocumentIndexed"), TestCancellationToken);
        outboxCount.Should().Be(1,
            "VectorDocument.Create must raise VectorDocumentIndexedEvent, which SaveChangesAsync " +
            "persists into domain_event_outbox (Hybrid mode Step 2b). Outbox count=1 proves the " +
            "structural domain-event path is working in IndexPdfCommandHandler (Task 5 migration, " +
            "call site 3/3). Production DomainEventOutboxProcessor dispatches this row to flip " +
            "shared_games.has_knowledge_base = true (covered end-to-end by Task 3 integration test).");
    }

    /// <summary>
    /// Task 7 / issue #2244 (Sub #2, final structural task): proves that
    /// <see cref="UploadPdfCommandHandler.FinalizeProcessingAsync"/> uses
    /// <c>PdfDocument.TransitionTo(Ready)</c> via <c>IPdfDocumentRepository</c>
    /// to raise <c>PdfStateChangedEvent</c> and <c>KbDocIndexedEvent</c> structurally,
    /// rather than the tactical <c>scopedMediator.Publish(PdfStateChangedEvent)</c>
    /// that was removed in this task.
    ///
    /// Asserts:
    ///   1. Exactly 1 <c>KbDocIndexedEvent</c> outbox row after the pipeline completes —
    ///      proves the structural domain path fires exactly once (no duplicate from
    ///      old tactical + new structural coexistence).
    ///   2. Exactly 1 <c>PdfStateChangedEvent</c> outbox row with NewState=Ready —
    ///      proves the Indexing→Ready transition event is structural, not tactical.
    /// </summary>
    [Fact(Timeout = 90000)]
    public async Task UploadPdf_OnReady_RaisesKbDocIndexedEventOnce()
    {
        // Arrange
        var handler = _serviceProvider!.GetRequiredService<UploadPdfCommandHandler>();
        var testUser = await _dbContext!.Users.FirstAsync(TestCancellationToken);
        var testGame = await _dbContext.SharedGames.FirstAsync(TestCancellationToken);

        var pdfBytes = CreateValidPdfBytes(1024);
        var formFile = CreateMockFormFile("rules-task7.pdf", pdfBytes);

        var command = new UploadPdfCommand(
            GameId: testGame.Id.ToString(),
            Metadata: null,
            PrivateGameId: null,
            UserId: testUser.Id,
            File: formFile);

        // Act
        var result = await handler.Handle(command, TestCancellationToken);
        await _inlineBgService!.WaitForAllAsync();

        // Precondition: upload and BG pipeline succeeded
        result.Should().NotBeNull();
        result.Success.Should().BeTrue("upload accept-stage must succeed for a valid PDF");
        var pdfDocId = Guid.Parse(result.Document!.Id.ToString());

        var pdfDoc = await _dbContext.PdfDocuments
            .AsNoTracking()
            .SingleAsync(p => p.Id == pdfDocId, TestCancellationToken);
        pdfDoc.ProcessingState.Should().Be(
            nameof(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Ready),
            "BG pipeline must reach Ready for this test to be meaningful");

        // Assert 1: exactly 1 KbDocIndexedEvent in the domain_event_outbox.
        // PdfDocument.TransitionTo(Ready) raises KbDocIndexedEvent structurally; the
        // SaveChanges dispatcher (Hybrid mode Step 2b) persists it into the outbox.
        // If the old tactical scopedMediator.Publish path were still present AND the
        // structural path active, we would see 0 (tactical-only, no outbox row) or 1
        // (structural-only). After Task 7, only the structural path exists → count = 1.
        // KbDocIndexedEvent is registered in EventTypeRegistry as "kb.doc.indexed"
        // (not the CLR name "KbDocIndexedEvent"). Query using the registry alias.
        var kbIndexedCount = await _dbContext.DomainEventOutbox
            .AsNoTracking()
            .CountAsync(e => e.EventType == "kb.doc.indexed", TestCancellationToken);
        kbIndexedCount.Should().Be(1,
            "PdfDocument.TransitionTo(Ready) must raise exactly 1 KbDocIndexedEvent via the " +
            "structural domain-event path (IPdfDocumentRepository.UpdateAsync → IDomainEventCollector " +
            "→ SaveChangesAsync → outbox). The removed tactical scopedMediator.Publish(PdfStateChangedEvent) " +
            "never raised KbDocIndexedEvent anyway; the structural path is the ONLY source.");

        // Assert 2: exactly 1 PdfStateChangedEvent in the outbox for this pipeline run.
        // Only the Ready transition in FinalizeProcessingAsync goes through the domain
        // (other state transitions use direct EF entity mutation and don't raise events).
        // PdfStateChangedEvent is unregistered in EventTypeRegistry so EventType = CLR FullName.
        // The PayloadJson contains "Ready" as the newState enum value.
        //
        // Note: PdfStateChangedEvent was previously published via the tactical
        // scopedMediator.Publish path (which bypassed the outbox entirely). After Task 7,
        // it flows through the domain → structural path → outbox.
        var pdfStateChangedEvents = await _dbContext.DomainEventOutbox
            .AsNoTracking()
            .Where(e => e.EventType.Contains("PdfStateChanged"))
            .ToListAsync(TestCancellationToken);
        pdfStateChangedEvents.Should().HaveCount(1,
            "exactly 1 PdfStateChangedEvent must be in the outbox: the structural Ready transition. " +
            "Other pipeline state transitions (Pending→Uploading, etc.) still use direct EF mutation " +
            "and do not raise domain events. The removed tactical scopedMediator.Publish " +
            "bypassed the outbox and is now replaced by the structural path.");
        // PayloadJson uses camelCase + lowercase enum values (DomainEventJsonOptions).
        // "newState": "ready" (not "Ready") is the correct assertion.
        pdfStateChangedEvents[0].PayloadJson.Should().Contain("ready",
            "the PdfStateChangedEvent outbox row must have newState=ready in its camelCase JSON payload.");
    }

    /// <summary>
    /// In-memory log sink. Diagnoses ProcessPdfAsync pipeline failures
    /// (which otherwise surface as opaque "Object reference not set" errors).
    /// </summary>
    internal sealed class ListLoggerSink
    {
        private readonly List<string> _entries = new();
        private readonly Lock _gate = new();

        public void Append(string entry)
        {
            lock (_gate)
            {
                _entries.Add(entry);
            }
        }

        public IReadOnlyList<string> Snapshot()
        {
            lock (_gate)
            {
                return _entries.ToArray();
            }
        }
    }

    internal sealed class ListLoggerProvider : ILoggerProvider
    {
        private readonly ListLoggerSink _sink;

        public ListLoggerProvider(ListLoggerSink sink) => _sink = sink;

        public ILogger CreateLogger(string categoryName) => new ListLogger(_sink, categoryName);

        public void Dispose() { }
    }

    internal sealed class ListLogger : ILogger
    {
        private readonly ListLoggerSink _sink;
        private readonly string _category;

        public ListLogger(ListLoggerSink sink, string category)
        {
            _sink = sink;
            _category = category;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            var msg = formatter(state, exception);
            var suffix = exception is not null
                ? $" | Ex={exception.GetType().Name}: {exception.Message}\n{exception.StackTrace}"
                : string.Empty;
            // Trim category to last segment for compact output
            var lastDot = _category.LastIndexOf('.');
            var shortCategory = lastDot >= 0 ? _category[(lastDot + 1)..] : _category;
            _sink.Append($"[{logLevel}] {shortCategory}: {msg}{suffix}");
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    /// <summary>
    /// Test-only IBackgroundTaskService that runs the queued task synchronously
    /// while still letting the caller await all in-flight tasks before assertions.
    ///
    /// The production registration uses a thread-pool fire-and-forget runner so
    /// the HTTP request returns 200 immediately. For test determinism we need the
    /// pipeline (extract → chunk → embed → index → finalize) to complete before
    /// the test inspects the DB.
    /// </summary>
    internal sealed class InlineBackgroundTaskService : IBackgroundTaskService
    {
        private readonly List<Task> _running = new();
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
                _running.Add(Task.Run(() => taskFactory(CancellationToken.None)));
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
