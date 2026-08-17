using System.Security.Cryptography;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
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
[Collection("Integration-GroupC")]
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

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPdfDocumentRepository, PdfDocumentRepository>();

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

        var tableExtractorMock = new Mock<IPdfTableExtractor>();
        tableExtractorMock
            .Setup(t => t.ExtractStructuredContentAsync(It.IsAny<string>()!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StructuredContentResult
            {
                Success = false,
                ErrorMessage = "Skipped in test"
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
    /// Test-only IBackgroundTaskService that runs the queued task synchronously
    /// while still letting the caller await all in-flight tasks before assertions.
    /// Mirrors UploadPdfHeadingAwareIntegrationTests.InlineBackgroundTaskService.
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
