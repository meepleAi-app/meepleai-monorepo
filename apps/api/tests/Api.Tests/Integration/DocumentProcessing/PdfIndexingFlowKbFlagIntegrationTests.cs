using System.Security.Cryptography;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Configuration;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.Infrastructure;
using Api.Infrastructure.DomainEventLog;
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

        // #3633: senza questa registrazione il test fallisce con «Unable to resolve service for
        // type 'IGameTitleResolver'». In produzione è registrata da
        // SharedGameCatalogServiceExtensions (#2339 batch translation enricher); questa fixture
        // costruisce il DI a mano e non carica quel bounded context.
        services.AddScoped<
            Api.BoundedContexts.SharedGameCatalog.Application.Services.IGameTitleResolver,
            Api.BoundedContexts.SharedGameCatalog.Application.Services.GameTitleResolver>();
        services.AddScoped<
            Api.BoundedContexts.SharedGameCatalog.Domain.Repositories.ISharedGameTranslationRepository,
            Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories.SharedGameTranslationRepository>();

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

        // Mock IVectorDocumentRepository (required by VectorDocumentIndexedEventHandler relay
        // that fires after Sub #1 Block A publishes VectorDocumentIndexedEvent)
        services.AddScoped(_ => Mock.Of<Api.BoundedContexts.KnowledgeBase.Domain.Repositories.IVectorDocumentRepository>());

        // Real IPdfIndexingPipeline (#2244 / epic #2242 Sub #2): the production write
        // path that UploadPdfCommandHandler.UpdateVectorDocumentAsync now resolves at
        // runtime via scope.ServiceProvider.GetRequiredService. Without this binding
        // the resolver would throw InvalidOperationException and the handler's outer
        // general catch would silently mark the PDF Failed — which is exactly the
        // silent-fail mode this test class was written to detect.
        services.AddScoped<Api.BoundedContexts.KnowledgeBase.Application.Services.IPdfIndexingPipeline,
            Api.BoundedContexts.KnowledgeBase.Application.Services.PdfIndexingPipeline>();

        // HybridCache + dependencies — required by VectorDocumentIndexedForKbFlagHandler
        // (the handler that actually flips shared_games.has_knowledge_base = true on the event).
        services.AddSingleton<IMemoryCache, MemoryCache>();
        services.AddSingleton<IDistributedCache>(new MemoryDistributedCache(
            Options.Create(new MemoryDistributedCacheOptions())));
        services.AddHybridCache();
        services.AddScoped<Api.Services.ICacheInvalidationRetryPolicy>(_ => new PassthroughRetryPolicy());

        // ADR-062: IHybridCacheService wrapper used by VectorDocumentIndexedForKbFlagHandler
        // for cross-replica L1 invalidation. Configuration mirrors production defaults; the
        // wrapper still works in single-replica test mode (broadcast publish becomes no-op
        // when only this Redis subscriber connects).
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

        // CRITICAL ASSERTION: this is the bug.
        // VectorDocumentIndexedForKbFlagHandler subscribes to VectorDocumentIndexedEvent
        // and is the only place that sets has_knowledge_base = true on shared_games.
        // The event is currently NEVER published from any of the 3 ingestion paths
        // (UploadPdfCommandHandler.ProcessPdfAsync, PdfProcessingPipelineService, IndexPdfCommandHandler)
        // because they write VectorDocumentEntity directly via EF instead of constructing
        // the VectorDocument domain entity whose constructor raises the event.
        //
        // Sub #1 Block A fix: publish the event manually in FinalizeProcessingAsync
        // after PdfStateChangedEvent. Sub #2 refactors the architecture to use a domain
        // factory so the event is raised structurally.
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

        // #2284 follow-up coverage gap: assert the pipeline raises exactly 6
        // PdfStateChangedEvent into the domain-event outbox (one per state transition:
        // Pending→Uploading, Uploading→Extracting, Extracting→Chunking,
        // Chunking→Embedding, Embedding→Indexing, Indexing→Ready) plus exactly 1
        // KbDocIndexedEvent on the Ready transition. Pre-refactor the count was 1 due
        // to the bridge-save shortcut (Uploading → Indexing via EF). The 6-event count
        // matches the PdfDocument_SevenStateProgression baseline contract.
        // Match outbox rows by checking the JSON payload contains the PdfDocumentId — the
        // outbox row schema does not store an AggregateId column, so the test filters
        // on the serialised event body. Acceptable in test scope because the only PDF in
        // play is the one this test seeded.
        // #3633: il filtro sul payload va fatto in memoria. `PayloadJson` è mappato a jsonb, e un
        // `.Contains(...)` dentro l'espressione LINQ viene tradotto in `LIKE`, che per Postgres non
        // esiste su quel tipo: la query moriva con `42883: operator does not exist: jsonb ~~ jsonb`
        // prima di qualunque assert. Il filtro su `EventType` (text) resta invece nel database.
        // #3633: gli alias arrivano da EventTypeRegistry, non scritti a mano. La colonna
        // `EventType` NON contiene il nome CLR: `MeepleAiDbContext.EnqueueOutboxRows` la
        // popola con `EventTypeRegistry.ResolveOrFullName(...)`, che per questi due eventi
        // restituisce gli alias stabili «pdf.state.changed» e «kb.doc.indexed» (#661).
        // I filtri precedenti cercavano «PdfStateChanged» e «KbDocIndexed» e non potevano
        // matchare nulla: il test contava 0 e 0, e falliva sul primo assert facendo sembrare
        // regredita la pipeline mentre a essere sbagliata era l'interrogazione.
        var pdfStateChangedAlias = EventTypeRegistry.AliasByType[typeof(PdfStateChangedEvent)];
        var kbDocIndexedAlias = EventTypeRegistry.AliasByType[typeof(KbDocIndexedEvent)];

        var pdfIdString = pdfDocId.ToString();
        var outboxRows = await _dbContext.DomainEventOutbox
            .AsNoTracking()
            .Select(e => new { e.EventType, e.PayloadJson })
            .ToListAsync(TestCancellationToken);

        var pdfStateChangedCount = outboxRows.Count(
            e => string.Equals(e.EventType, pdfStateChangedAlias, StringComparison.Ordinal)
                 && e.PayloadJson.Contains(pdfIdString, StringComparison.Ordinal));
        pdfStateChangedCount.Should().Be(6,
            "pipeline must raise PdfStateChangedEvent for EVERY state transition " +
            "(Pending→Uploading + Uploading→Extracting + Extracting→Chunking + " +
            "Chunking→Embedding + Embedding→Indexing + Indexing→Ready). " +
            "If this counts 1, the bridge-save shortcut from PR #2295 has regressed.");

        var kbDocIndexedCount = outboxRows.Count(
            e => string.Equals(e.EventType, kbDocIndexedAlias, StringComparison.Ordinal)
                 && e.PayloadJson.Contains(pdfIdString, StringComparison.Ordinal));
        kbDocIndexedCount.Should().Be(1,
            "KbDocIndexedEvent must fire exactly once per upload, raised by " +
            "PdfDocument.TransitionTo(Ready) (PdfDocument.cs:435-442).");
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
