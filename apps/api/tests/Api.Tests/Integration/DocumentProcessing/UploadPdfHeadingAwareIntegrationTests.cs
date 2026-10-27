using System.Security.Cryptography;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Configuration;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
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
/// Issue #3281 (Task 3): wires <see cref="HeadingAwareChunker.BuildAsync"/> into
/// <see cref="UploadPdfCommandHandler"/>'s background processing pipeline
/// (<c>ChunkExtractedTextAsync</c>). Proves that when an <see cref="IAdvancedChunkingService"/>
/// is resolvable from the background-task scope, the fresh Upload ingest path (regular +
/// private-game upload) produces and persists heading-bearing <c>text_chunks</c> rows with
/// parent/child hierarchy, instead of the flat <c>ITextChunkingService.PrepareForEmbedding</c>
/// production.
///
/// Modeled on <c>PdfIndexingFlowKbFlagIntegrationTests</c>'s harness: real Postgres via
/// <see cref="SharedTestcontainersFixture"/>, <c>InlineBackgroundTaskService</c> so the BG
/// pipeline (extract → chunk → embed → index → finalize) runs synchronously before assertions.
/// </summary>
[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3281")]
public sealed class UploadPdfHeadingAwareIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;
    private IConnectionMultiplexer? _redis;
    private string? _testDataDirectory;
    private InlineBackgroundTaskService? _inlineBgService;

    // #4390: gira dentro la classificazione dei ruoli, che il caricamento chiama dopo aver creato il
    // documento vettoriale e prima di salvare passaggi ed embedding.
    private Func<Task>? _whileThePassagesAreBeingWritten;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public UploadPdfHeadingAwareIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_headingaware_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        _testDataDirectory = Path.Combine(Path.GetTempPath(), "meepleai-test-headingaware-" + Guid.NewGuid());
        Directory.CreateDirectory(_testDataDirectory);

        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);

        _redis = await ConnectionMultiplexer.ConnectAsync(_fixture.RedisConnectionString);
        services.AddSingleton<IConnectionMultiplexer>(_redis);

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPdfDocumentRepository, PdfDocumentRepository>();
        // #4443 R1: the upload background task claims the document before working on it.
        services.AddScoped<Api.BoundedContexts.DocumentProcessing.Application.Services.IPdfClaimService, Api.BoundedContexts.DocumentProcessing.Infrastructure.Services.RelationalPdfClaimService>();

        services.AddScoped<UploadPdfCommandHandler>();

        services.Configure<PdfProcessingOptions>(options =>
        {
            options.MaxFileSizeBytes = 10 * 1024 * 1024;
        });

        // Inline background task service so the BG pipeline runs synchronously before assertions
        // (mirrors PdfIndexingFlowKbFlagIntegrationTests — Mock<IBackgroundTaskService> never executes).
        _inlineBgService = new InlineBackgroundTaskService();
        services.AddSingleton<IBackgroundTaskService>(_inlineBgService);

        services.AddSingleton<IEmbeddingService>(new MockEmbeddingService(dimensions: 768));

        // Extractor returns a paged result WITH non-empty StructuredElements (a Title + body
        // element) so ChunkExtractedTextAsync feeds them into HeadingAwareChunker.BuildAsync.
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

        // CRITICAL for this test: mocked IAdvancedChunkingService registered in the container so
        // scope.ServiceProvider.GetService<IAdvancedChunkingService>() (Processing.cs) resolves it
        // (non-null) and ChunkExtractedTextAsync takes the heading-aware branch instead of the
        // flat ITextChunkingService.PrepareForEmbedding fallback.
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

        // #4384: un ruolo diverso da None per ogni passaggio, così role_tags è osservabile sulle righe
        // di pgvector_embeddings (senza classificatore resterebbe 0 su entrambe le tabelle).
        var roleClassifierMock = new Mock<Api.BoundedContexts.KnowledgeBase.Application.Services.IRoleClassifierService>();
        roleClassifierMock
            .Setup(c => c.ClassifyAsync(It.IsAny<IReadOnlyList<ChunkInput>>(), It.IsAny<CancellationToken>()))
            .Returns(async (IReadOnlyList<ChunkInput> inputs, CancellationToken _) =>
            {
                if (_whileThePassagesAreBeingWritten is { } hook)
                {
                    await hook();
                }

                return (IReadOnlyList<GameBookRole>)inputs
                    .Select((_, i) => i % 2 == 0 ? GameBookRole.Setup : GameBookRole.RulesReference).ToList();
            });
        services.AddSingleton(roleClassifierMock.Object);

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

        // #4390: il repository vero, così VectorDocumentIndexedEventHandler trova il documento vettoriale e
        // la compensazione (VectorDocumentReadyStateHandler) parte. Con un Mock.Of il gestore registrava
        // «VectorDocument not found» e si fermava: nessun test di caricamento qui esercitava la compensazione.
        services.AddScoped<Api.BoundedContexts.KnowledgeBase.Domain.Repositories.IVectorDocumentRepository,
            Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence.VectorDocumentRepository>();
        // Da lì l'evento d'integrazione raggiunge anche SharedGameIndexingAdminNotificationHandler, che
        // chiede il repository dei giochi: senza, la pubblicazione fallisce e il caricamento finisce
        // Failed con «Invalid operation».
        services.AddScoped<Api.BoundedContexts.SharedGameCatalog.Domain.Repositories.ISharedGameRepository,
            Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories.SharedGameRepository>();

        services.AddScoped<Api.BoundedContexts.KnowledgeBase.Application.Services.IPdfIndexingPipeline,
            Api.BoundedContexts.KnowledgeBase.Application.Services.PdfIndexingPipeline>();
        // PdfIndexingPipeline invalida la cache semantica dopo l'indicizzazione. Senza questa riga
        // l'elaborazione cadeva su «Unable to resolve ISemanticResponseCache» e finiva Failed con
        // ProcessingError='Invalid operation': il test qui sotto era rosso anche su main-dev (#4384).
        services.AddSingleton(Mock.Of<ISemanticResponseCache>());

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
            Email = $"headingaware-{Guid.NewGuid():N}@test.com",
            DisplayName = "Heading Aware Test User",
            Role = "User",
            Tier = "Free",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext!.Users.Add(user);

        var game = new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = $"Heading Aware Test Game {Guid.NewGuid():N}",
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
    public async Task ChunkExtractedText_WithAdvancedChunking_PersistsHeadingBearingChunks()
    {
        // Arrange
        var handler = _serviceProvider!.GetRequiredService<UploadPdfCommandHandler>();
        var testUser = await _dbContext!.Users.FirstAsync(TestCancellationToken);
        var testGame = await _dbContext.SharedGames.FirstAsync(TestCancellationToken);

        var pdfBytes = CreateValidPdfBytes(1024);
        var formFile = CreateMockFormFile("heading-aware.pdf", pdfBytes);

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
        var pdfDoc = await _dbContext.PdfDocuments
            .AsNoTracking()
            .SingleAsync(p => p.Id == pdfDocId, TestCancellationToken);
        pdfDoc.ProcessingState.Should().Be(
            nameof(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Ready),
            $"BG pipeline must reach Ready when the heading-aware chunk production succeeds. " +
            $"ProcessingError='{pdfDoc.ProcessingError}'.");

        // Assert: persisted text_chunks carry the heading-aware hierarchy produced by the
        // mocked IAdvancedChunkingService (parent Level0 "Setup" + child Level2 with ParentChunkId).
        var chunks = await _dbContext.TextChunks
            .AsNoTracking()
            .Where(tc => tc.PdfDocumentId == pdfDocId)
            .ToListAsync(TestCancellationToken);

        chunks.Should().HaveCount(2, "the mocked IAdvancedChunkingService returns exactly one parent + one child");

        var parentChunk = chunks.SingleOrDefault(c => c.ParentChunkId == null);
        parentChunk.Should().NotBeNull("the parent (section) chunk has no ParentChunkId");
        parentChunk!.Heading.Should().Be("Setup");
        // The parent/section chunk is Level 0 (HeadingAwareChunker / HierarchicalChunk.CreateParent).
        // This round-trips correctly through real Npgsql because TextChunkEntityConfiguration.cs
        // now declares Level with .ValueGeneratedNever() (#3281) — without it, HasDefaultValue(1)
        // would coerce the explicit 0 to the store default 1 on INSERT. This assertion is the
        // regression guard for that fix: it fails against real Postgres if ValueGeneratedNever() is removed.
        parentChunk.Level.Should().Be((short)0,
            "parent/section chunks are Level 0 and must persist as 0 (ValueGeneratedNever fix)");

        var childChunk = chunks.SingleOrDefault(c => c.ParentChunkId != null);
        childChunk.Should().NotBeNull("the child chunk must reference the parent via ParentChunkId");
        childChunk!.Heading.Should().Be("Setup");
        // Level=2 != default(short) so it is NOT subject to the default-value substitution above,
        // and round-trips correctly — this is the meaningful signal that Level pass-through works.
        childChunk.Level.Should().Be((short)2);
        childChunk.ParentChunkId.Should().Be(parentChunk.Id,
            "the child's ParentChunkId must point at the persisted parent row's Id");
    }

    /// <summary>
    /// #4384: il caricamento da <c>/ingest/pdf</c> scriveva le righe di <c>pgvector_embeddings</c>
    /// senza <c>source_chunk_id</c> né <c>role_tags</c>. Ogni riga deve puntare al passaggio con lo
    /// stesso <c>ChunkIndex</c> e portarne il ruolo, come fa la pipeline di Quartz.
    /// </summary>
    [Fact(Timeout = 90000)]
    public async Task Upload_LinksEveryPgvectorRowToTheTextChunkAtTheSameIndex()
    {
        // Arrange
        var handler = _serviceProvider!.GetRequiredService<UploadPdfCommandHandler>();
        var testUser = await _dbContext!.Users.FirstAsync(TestCancellationToken);
        var testGame = await _dbContext.SharedGames.FirstAsync(TestCancellationToken);

        var command = new Api.BoundedContexts.DocumentProcessing.Application.Commands.UploadPdfCommand(
            GameId: testGame.Id.ToString(),
            Metadata: null,
            PrivateGameId: null,
            UserId: testUser.Id,
            File: CreateMockFormFile("linked.pdf", CreateValidPdfBytes(1024)));

        // Act
        var result = await handler.Handle(command, TestCancellationToken);
        await _inlineBgService!.WaitForAllAsync();

        // Assert
        result.Success.Should().BeTrue();
        var pdfDocId = Guid.Parse(result.Document!.Id.ToString());
        var pdfDoc = await _dbContext.PdfDocuments.AsNoTracking().SingleAsync(p => p.Id == pdfDocId, TestCancellationToken);
        pdfDoc.ProcessingState.Should().Be(
            nameof(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Ready),
            $"ProcessingError='{pdfDoc.ProcessingError}'");

        var chunksByIndex = await _dbContext.TextChunks
            .AsNoTracking()
            .Where(tc => tc.PdfDocumentId == pdfDocId)
            .ToDictionaryAsync(tc => tc.ChunkIndex, TestCancellationToken);
        var vectorDocumentId = await _dbContext.VectorDocuments
            .AsNoTracking()
            .Where(v => v.PdfDocumentId == pdfDocId)
            .Select(v => v.Id)
            .SingleAsync(TestCancellationToken);
        var rows = await _dbContext.PgVectorEmbeddings
            .AsNoTracking()
            .Where(e => e.VectorDocumentId == vectorDocumentId)
            .ToListAsync(TestCancellationToken);

        rows.Should().HaveCount(chunksByIndex.Count).And.NotBeEmpty();
        foreach (var row in rows)
        {
            var chunk = chunksByIndex[row.ChunkIndex];
            row.SourceChunkId.Should().Be(chunk.Id, "la riga {0} deve puntare al passaggio con lo stesso indice", row.ChunkIndex);
            row.RoleTags.Should().Be((int)chunk.RoleTags).And.NotBe(0, "il classificatore ha dato un ruolo a ogni passaggio");
        }
    }

    /// <summary>
    /// #4390 / #4609: il caricamento crea il documento vettoriale (<c>IPdfIndexingPipeline.BeginIndexingAsync</c>,
    /// in <c>processing</c>) prima di salvare passaggi ed embedding; <c>IndexAsync</c>, che lo completa e
    /// pubblica l'evento da cui parte la compensazione, gira per ultimo. Chi aspetta <c>Ready</c> non deve
    /// trovarlo finché l'indice è vuoto: in quella finestra una domanda riceve <c>NO_RESULTS</c>. Lo stato si
    /// legge da dentro la finestra, con un'altra connessione, mentre la pipeline è ferma nella classificazione
    /// dei ruoli. Prima di #4609 l'evento cadeva dentro la finestra; ora ne è fuori, e la protezione dal
    /// gestore che porta a <c>Ready</c> un PDF in <c>Indexing</c> sta nei test del gestore e nel controllo
    /// finale qui sotto (la pipeline arriva a <c>Ready</c> da sé, senza «Cannot transition from Ready state»).
    /// </summary>
    [Fact(Timeout = 90000)]
    [Trait("Issue", "4390")]
    public async Task Upload_IsNotReady_WhileItsPassagesAndEmbeddingsAreStillBeingWritten()
    {
        // Arrange
        var handler = _serviceProvider!.GetRequiredService<UploadPdfCommandHandler>();
        var testUser = await _dbContext!.Users.FirstAsync(TestCancellationToken);
        var testGame = await _dbContext.SharedGames.FirstAsync(TestCancellationToken);

        string? stateInTheWindow = null;
        int vectorDocumentsInTheWindow = -1;
        int embeddingsInTheWindow = -1;
        Exception? windowReadError = null;
        _whileThePassagesAreBeingWritten = async () =>
        {
            try
            {
                await using var scope = _serviceProvider!.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
                // Database isolato per test: il PDF è uno solo.
                stateInTheWindow = await db.PdfDocuments.AsNoTracking()
                    .Select(p => p.ProcessingState).SingleAsync(TestCancellationToken);
                vectorDocumentsInTheWindow = await db.VectorDocuments.AsNoTracking().CountAsync(TestCancellationToken);
                embeddingsInTheWindow = await db.PgVectorEmbeddings.AsNoTracking().CountAsync(TestCancellationToken);
            }
            catch (Exception ex)
            {
                // Il classificatore inghiotte le eccezioni: l'errore si riporta all'asserzione.
                windowReadError = ex;
            }
        };

        var command = new Api.BoundedContexts.DocumentProcessing.Application.Commands.UploadPdfCommand(
            GameId: testGame.Id.ToString(),
            Metadata: null,
            PrivateGameId: null,
            UserId: testUser.Id,
            File: CreateMockFormFile("ready-window.pdf", CreateValidPdfBytes(1024)));

        // Act
        var result = await handler.Handle(command, TestCancellationToken);
        // Un Ready prematuro fa fallire la finalizzazione («Cannot transition from Ready state»): l'errore
        // si asserisce dopo lo stato, così il rosso dice cosa è stato letto nella finestra.
        var backgroundError = await Record.ExceptionAsync(() => _inlineBgService!.WaitForAllAsync());

        // Assert: la finestra è stata raggiunta, dopo il documento vettoriale e prima di ogni embedding.
        result.Success.Should().BeTrue();
        windowReadError.Should().BeNull();
        vectorDocumentsInTheWindow.Should().Be(1, "il documento vettoriale (in processing) esiste prima dei passaggi");
        embeddingsInTheWindow.Should().Be(0, "la finestra precede la scrittura degli embedding");

        stateInTheWindow.Should().Be(
            nameof(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Indexing),
            "un PDF senza embedding non è pronto, anche se il suo documento vettoriale esiste");
        backgroundError.Should().BeNull();

        // ...e la pipeline lo porta comunque a Ready da sé, a indice scritto.
        var pdfDoc = await _dbContext.PdfDocuments.AsNoTracking().SingleAsync(TestCancellationToken);
        pdfDoc.ProcessingState.Should().Be(
            nameof(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Ready),
            $"ProcessingError='{pdfDoc.ProcessingError}'");
        (await _dbContext.PgVectorEmbeddings.AsNoTracking().CountAsync(TestCancellationToken)).Should().BePositive();
    }


    /// <summary>
    /// Test-only IBackgroundTaskService that runs the queued task synchronously
    /// while still letting the caller await all in-flight tasks before assertions.
    /// Mirrors PdfIndexingFlowKbFlagIntegrationTests.InlineBackgroundTaskService.
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
