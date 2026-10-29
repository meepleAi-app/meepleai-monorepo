using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Jobs;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Configuration;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.SharedKernel.Services;
using System.Security.Cryptography;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Quartz;
using Xunit;
using AuthRole = Api.SharedKernel.Domain.ValueObjects.Role;

namespace Api.Tests.Integration;

/// <summary>
/// Mid-Phase Cancellation Tests for PDF Upload (Issue #1819 - #1736 Enhanced).
/// Uses SharedTestcontainersFixture for optimized performance and Docker hijack prevention (Issue #2031).
///
/// <para><b>Purpose:</b> Test cancellation occurring MID-WAY through pipeline stages, not just at start/end</para>
/// <para><b>Production Scenarios:</b> Network interruptions, user timeouts, browser crashes during active processing</para>
/// <para><b>Acceptance Criteria:</b> Issue #1819 (#1736) - Database consistency verification across ALL stages</para>
///
/// Test Coverage:
/// 1. Mid-database operation cancellation (parte sincrona dell'upload)
/// 2. Mid-blob write cancellation (parte sincrona dell'upload)
/// 3. Mid-text extraction cancellation (saltato: DIFETTO #1684 follow-up)
/// 4. Mid-embedding batch cancellation (pipeline in background, <c>ProcessPdfAsync</c>)
/// 5. Mid-vector store cancellation: all'ingresso di <c>BeginIndexingAsync</c> e durante il salvataggio delle righe
///    vettoriali (pipeline in background)
/// 6. Cancellazione durante una transizione di stato, e dopo il commit di <c>Ready</c> (pipeline in background)
/// 7. Un annullamento dell'utente non viene ritentato; un timeout sì (#4610)
/// 8. Controllo: la stessa pipeline, senza cancellazione, arriva a <c>Ready</c>
/// 9. Cancellazione a istanti casuali e a istanti fissi (parte sincrona dell'upload, stress)
/// </summary>
/// <remarks>
/// <para>In CI gira nello shard <c>Core</c>, come gli altri <c>Category=Integration</c> (#4522).
/// Prima di #4522 era escluso per nome da <c>ci.yml</c> e <c>dev-async.yml</c> (#3102), e non girava
/// in nessun workflow.</para>
/// <para><b>Postgres:</b> ogni istanza crea un database suo (<c>test_uploadcancel_&lt;guid&gt;</c>) e lo
/// cancella in <c>DisposeAsync</c>. Con <c>TEST_POSTGRES_CONNSTRING</c> impostata, come in CI, la
/// fixture usa solo il server: il database condiviso <c>meepleai_test</c> non viene toccato.</para>
/// <para><b>Redis:</b> nessuno. Il gestore e i servizi che registra qui non lo usano (le dipendenze
/// che lo userebbero, quota e tier, sono doppi). Prima la classe si collegava al Redis della fixture
/// solo per farci <c>FLUSHDB</c> a ogni test: in CI quel Redis è il service container condiviso da
/// tutto lo shard, e il flush cancellava lo stato degli altri test in parallelo (#4522).</para>
/// <para><b>Quali test arrivano alle fasi (#4531):</b> i test che usano il banco di <c>InitializeAsync</c>
/// hanno <c>IBackgroundTaskService</c> come Mock senza setup, quindi esercitano solo la parte sincrona
/// dell'upload. Quelli che nominano embedding e indicizzazione usano <c>BuildProcessingHarness</c>: la lambda
/// in background gira davvero (<see cref="CancellableBackgroundTaskService"/>), la pipeline scrive su
/// Postgres, e l'embedding e l'estrazione del testo sono doppi che segnalano di essere «dentro la chiamata»
/// (<see cref="HoldPoint"/>). Nessun servizio esterno e nessun LLM.</para>
/// </remarks>
[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
[Trait("Issue", "2031")]
public sealed class UploadPdfMidPhaseCancellationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;
    private string? _testDataDirectory;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public UploadPdfMidPhaseCancellationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        // Issue #2031: Postgres dalla SharedTestcontainersFixture, per evitare il Docker hijack. Niente Redis (#4522).
        // PR2 fix: per-instance unique database name. The previous fixed name "test_uploadcancel"
        // caused `55006: database is being accessed by other users` when xUnit parallel test methods
        // raced on DROP DATABASE inside CreateIsolatedDatabaseAsync.
        _databaseName = $"test_uploadcancel_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        _testDataDirectory = Path.Combine(Path.GetTempPath(), "meepleai-midphase-test-" + Guid.NewGuid());
        Directory.CreateDirectory(_testDataDirectory);

        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);

        // Register repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPdfDocumentRepository, PdfDocumentRepository>();
        // #4443 R1: the upload background task claims the document before working on it.
        services.AddScoped<Api.BoundedContexts.DocumentProcessing.Application.Services.IPdfClaimService, Api.BoundedContexts.DocumentProcessing.Infrastructure.Services.RelationalPdfClaimService>();

        services.AddScoped<UploadPdfCommandHandler>();

        services.Configure<PdfProcessingOptions>(options =>
        {
            options.MaxFileSizeBytes = 10485760;
        });

        // Register mock services
        RegisterDefaultMockServices(services);

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        await _dbContext.Database.MigrateAsync(TestCancellationToken);
        await SeedTestDataAsync();
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_testDataDirectory != null && Directory.Exists(_testDataDirectory))
            {
                try
                {
                    await Task.Delay(TestConstants.Timing.SmallDelay);
                    Directory.Delete(_testDataDirectory, true);
                }
                catch (IOException ex)
                {
                    Console.WriteLine($"Warning: Failed to clean temp directory: {ex.Message}");
                }
            }

            if (_dbContext != null) await _dbContext.DisposeAsync();

            if (_serviceProvider is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync();
            else
                (_serviceProvider as IDisposable)?.Dispose();
        }
        finally
        {
            // #4522: nel finally, perché in CI il server Postgres è condiviso dallo shard e un
            // database rimasto lì resta fino alla fine del job. DropIsolatedDatabaseAsync non lancia.
            if (!string.IsNullOrEmpty(_databaseName))
            {
                await _fixture.DropIsolatedDatabaseAsync(_databaseName);
            }
        }
    }

    private void RegisterDefaultMockServices(IServiceCollection services)
    {
        // Register repositories (required for handler)
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPdfDocumentRepository, PdfDocumentRepository>();
        // #4443 R1: the upload background task claims the document before working on it.
        services.AddScoped<Api.BoundedContexts.DocumentProcessing.Application.Services.IPdfClaimService, Api.BoundedContexts.DocumentProcessing.Infrastructure.Services.RelationalPdfClaimService>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        // Register domain event infrastructure (required for handler)
        services.AddScoped<IDomainEventCollector, DomainEventCollector>();

        // Only register default mocks if not already registered by test
        if (!services.Any(s => s.ServiceType == typeof(IBackgroundTaskService)))
        {
            var backgroundTaskMock = new Mock<IBackgroundTaskService>();
            services.AddSingleton<IBackgroundTaskService>(backgroundTaskMock.Object);
        }

        // PR2 follow-up to #1684: UploadPdfCommandHandler requires ITierEnforcementService.
        // CreateBase() registers it; this self-contained test (MidTextExtraction) builds its own
        // ServiceCollection without CreateBase, so add it here under the same idempotent guard.
        // Mock.Of<T>() returns Task.FromResult(default) for Task<T> members → CanPerformAsync=false
        // → handler enters the TierLimitExceeded branch and dereferences GetUsageAsync's null result.
        // Configure CanPerformAsync to return true so the cancellation flow is exercised.
        if (!services.Any(s => s.ServiceType == typeof(ITierEnforcementService)))
        {
            var tierMock = new Mock<ITierEnforcementService>();
            tierMock.Setup(t => t.CanPerformAsync(It.IsAny<Guid>(), It.IsAny<TierAction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            // UploadPdfCommandHandler also reads tierLimits.MaxPdfSizeBytes to gate the upload.
            // Mock.Of would return null for the Task<TierLimits>'s Result → NRE at line 108.
            tierMock.Setup(t => t.GetLimitsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects.TierLimits.Unlimited);
            services.AddScoped<ITierEnforcementService>(_ => tierMock.Object);
        }

        if (!services.Any(s => s.ServiceType == typeof(IAiResponseCacheService)))
        {
            var cacheMock = new Mock<IAiResponseCacheService>();
            cacheMock.Setup(c => c.InvalidateGameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            services.AddSingleton<IAiResponseCacheService>(cacheMock.Object);
        }

        if (!services.Any(s => s.ServiceType == typeof(IBlobStorageService)))
        {
            var blobStorageMock = new Mock<IBlobStorageService>();
            blobStorageMock.Setup(b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Stream stream, string fileName, BlobCategory category, string resourceKey, CancellationToken ct) =>
                {
                    // Sanitize filename for filesystem safety (tests may use malicious filenames)
                    var safeFileName = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars()));
                    var filePath = Path.Combine(_testDataDirectory!, $"{resourceKey}_{safeFileName}");
                    using var fileStream = File.Create(filePath);
                    stream.CopyTo(fileStream);
                    return new BlobStorageResult(true, Guid.NewGuid().ToString(), filePath, stream.Length, null);
                });
            services.AddSingleton<IBlobStorageService>(blobStorageMock.Object);
        }

        // Always register PDF extractors (not overrideable in current tests)
        if (!services.Any(s => s.ServiceType == typeof(IPdfTextExtractor)))
        {
            var extractorMock = new Mock<IPdfTextExtractor>();
            extractorMock.Setup(e => e.ExtractTextAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TextExtractionResult(
                    Success: true,
                    ExtractedText: "Mock extracted text from PDF",
                    PageCount: 1,
                    CharacterCount: 100,
                    OcrTriggered: false,
                    Quality: ExtractionQuality.High,
                    ErrorMessage: null));
            services.AddSingleton<IPdfTextExtractor>(extractorMock.Object);
        }

        if (!services.Any(s => s.ServiceType == typeof(IPdfTableExtractor)))
        {
            var tableExtractorMock = new Mock<IPdfTableExtractor>();
            services.AddSingleton<IPdfTableExtractor>(tableExtractorMock.Object);
        }

        if (!services.Any(s => s.ServiceType == typeof(IPdfUploadQuotaService)))
        {
            var quotaMock = new Mock<IPdfUploadQuotaService>();
            quotaMock.Setup(q => q.CheckQuotaAsync(It.IsAny<Guid>(), It.IsAny<UserTier>(), It.IsAny<AuthRole>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(PdfUploadQuotaResult.Success(0, int.MaxValue, 0, int.MaxValue, DateTime.MaxValue, DateTime.MaxValue));
            // FIX: Add missing ReserveQuotaAsync mock setup (Issue #2307 - PDF test failures)
            quotaMock.Setup(q => q.ReserveQuotaAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(QuotaReservationResult.Success(DateTime.UtcNow.AddHours(1)));
            quotaMock.Setup(q => q.ReleaseQuotaAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            services.AddSingleton<IPdfUploadQuotaService>(quotaMock.Object);
        }
    }

    private async Task SeedTestDataAsync()
    {
        var testUser = await PdfUploadTestHelpers.SeedTestUserAsync(_dbContext!);
        var testGame = await PdfUploadTestHelpers.SeedTestGameAsync(_dbContext!);
    }
    /// <summary>
    /// Tests cancellation that occurs MID-WAY through database query execution.
    ///
    /// <para><b>Test Category:</b> Cancellation - Mid-Phase Timing</para>
    /// <para><b>Production Scenario:</b> User closes browser exactly during database INSERT operation</para>
    /// <para><b>Expected Behavior:</b> Transaction rollback, no partial data, OperationCanceledException</para>
    /// <para><b>Acceptance Criteria:</b> Issue #1819 (#1736) - Database consistency after mid-phase cancellation</para>
    /// </summary>
    [Fact(Timeout = 90000)]
    public async Task UploadPdf_WhenCancelledMidDatabaseOperation_RollsBackCompletely()
    {
        // Arrange
        var handler = _serviceProvider!.GetRequiredService<UploadPdfCommandHandler>();
        var testUser = await _dbContext!.Users.FirstAsync(TestCancellationToken);
        var testGame = await _dbContext.SharedGames.FirstAsync(TestCancellationToken);

        var pdfBytes = PdfUploadTestHelpers.CreateValidPdfBytes(1024 * 10);
        var formFile = PdfUploadTestHelpers.CreateMockFormFile("mid_db_cancel.pdf", pdfBytes);

        var command = new UploadPdfCommand(
            GameId: testGame.Id.ToString(),
            Metadata: null,
            null,  // PrivateGameId
            UserId: testUser.Id,
            File: formFile);

        using var cts = PdfUploadTestHelpers.CreateDelayedCancellation(10); // Very short delay

        // Act - May complete or be cancelled depending on timing
        try
        {
            var result = await handler.Handle(command, cts.Token);
            // If completes, that's OK - just verify cleanup happens if failure
        }
        catch (OperationCanceledException)
        {
            // Expected if cancelled during DB operation
        }

        // Assert - Verify consistency regardless of whether operation completed or was cancelled
        await PdfUploadTestHelpers.VerifyDatabaseConsistencyAsync(_dbContext, testUser.Id, testGame.Id);
    }

    /// <summary>
    /// Tests cancellation during blob storage write operation (mid-transfer).
    ///
    /// <para><b>Production Scenario:</b> Network interruption during file upload to storage</para>
    /// <para><b>Expected Behavior:</b> Cleanup partial data, no orphaned files</para>
    /// </summary>
    [Fact(Timeout = 90000)]
    public async Task UploadPdf_WhenCancelledMidBlobWrite_CleansUpPartialData()
    {
        // Arrange - Override blob storage with mid-write cancellation mock using SharedTestcontainersFixture

        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);

        // #4522: il test cancella quando il gestore è DENTRO la scrittura del blob, non dopo un ritardo
        // fisso. Col ritardo di 100 ms di prima era rosso 2 run su 3 in locale: a freddo la prima query
        // del gestore (la ricerca per hash del contenuto) dura di più, la cancellazione cadeva lì, e il
        // test vedeva l'OperationCanceledException della query invece del TaskCanceledException della
        // scrittura. Misurava un'altra fase.
        var blobWriteStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var midWriteBlob = new Mock<IBlobStorageService>();
        midWriteBlob.Setup(b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (Stream stream, string fileName, BlobCategory category, string resourceKey, CancellationToken ct) =>
            {
                blobWriteStarted.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, ct); // la scrittura finisce solo con la cancellazione: TaskCanceledException
                return new BlobStorageResult(false, null, null, 0, "Never reached");
            });
        services.AddSingleton<IBlobStorageService>(midWriteBlob.Object);

        RegisterDefaultMockServices(services);
        services.Configure<PdfProcessingOptions>(options => options.MaxFileSizeBytes = 10485760);
        services.AddScoped<UploadPdfCommandHandler>();

        var midWriteProvider = services.BuildServiceProvider();
        var testDbContext = midWriteProvider.GetRequiredService<MeepleAiDbContext>();
        await testDbContext.Database.MigrateAsync(TestCancellationToken);
        await PdfUploadTestHelpers.CleanDatabaseAsync(testDbContext);

        var testUser = await PdfUploadTestHelpers.SeedTestUserAsync(testDbContext);
        var testGame = await PdfUploadTestHelpers.SeedTestGameAsync(testDbContext);

        var handler = midWriteProvider.GetRequiredService<UploadPdfCommandHandler>();

        var pdfBytes = PdfUploadTestHelpers.CreateValidPdfBytes(1024 * 100);
        var formFile = PdfUploadTestHelpers.CreateMockFormFile("mid_blob_write.pdf", pdfBytes);

        var command = new UploadPdfCommand(
            GameId: testGame.Id.ToString(),
            Metadata: null,
            null,  // PrivateGameId
            UserId: testUser.Id,
            File: formFile);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);

        // Act - cancellazione a scrittura in corso
        var handleTask = handler.Handle(command, cts.Token);
        try
        {
            var first = await Task.WhenAny(blobWriteStarted.Task, handleTask)
                .WaitAsync(TimeSpan.FromSeconds(60), TestCancellationToken);
            if (first == handleTask)
            {
                // Il gestore è uscito prima della scrittura: il test non può cancellarla lì. Il
                // fallimento deve dire perché, non solo che non ci è arrivato.
                string outcome;
                try
                {
                    var result = await handleTask;
                    outcome = $"returned Success={result.Success}, Message=\"{result.Message}\"";
                }
                catch (Exception ex)
                {
                    outcome = $"threw {ex}";
                }

                Assert.Fail($"The handler must reach the blob write before the test cancels it, but it ended first: {outcome}");
            }
        }
        finally
        {
            // È la cancellazione del test, e va fatta anche se l'attesa scade o l'asserzione sopra
            // fallisce: altrimenti il gestore resta appeso sul Delay infinito del doppio.
            await cts.CancelAsync();
        }

        // Assert
        var act = async () => await handleTask;
        await act.Should().ThrowAsync<TaskCanceledException>();

        // Verify cleanup
        await PdfUploadTestHelpers.VerifyNoPdfDocumentsAsync(testDbContext);
        PdfUploadTestHelpers.VerifyNoOrphanedFiles(_testDataDirectory!, "*mid_blob_write*");
    }

    /// <summary>
    /// Tests cancellation during text extraction processing (mid-extraction).
    ///
    /// <para><b>Production Scenario:</b> Long PDF extraction cancelled by user timeout</para>
    /// <para><b>Expected Behavior:</b> Release resources, no memory leaks</para>
    /// </summary>
    [Fact(Skip = "DIFETTO: #1684 follow-up: test asserts handler returns a result on cancellation, but the production code (correctly) propagates OperationCanceledException through SaveChangesAsync. Once the upstream DI mocks were fixed (#1684), cancellation now reaches the handler and surfaces TaskCanceledException — exposing this brittle assertion. Tracked as follow-up: rewrite the test to expect cancellation OR assert resource-release without requiring a non-null return.", Timeout = 90000)]
    public async Task UploadPdf_WhenCancelledMidTextExtraction_ReleasesResources()
    {
        // Arrange - Mock extractor that delays during processing
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseInMemoryDatabase($"MidExtractionTest-{Guid.NewGuid()}");
        });

        var midExtractor = new Mock<IPdfTextExtractor>();
        midExtractor.Setup(e => e.ExtractTextAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(async (Stream stream, bool ocr, CancellationToken ct) =>
            {
                await Task.Delay(TestConstants.Timing.LargeDelay, ct); // Will throw if cancelled mid-processing
                throw new OperationCanceledException("Should not reach here");
            });
        services.AddSingleton<IPdfTextExtractor>(midExtractor.Object);

        RegisterDefaultMockServices(services);
        services.Configure<PdfProcessingOptions>(options => options.MaxFileSizeBytes = 10485760);
        services.AddMediatR(config => config.RegisterServicesFromAssembly(typeof(UploadPdfCommandHandler).Assembly));
        services.AddScoped<UploadPdfCommandHandler>();

        var midExtractionProvider = services.BuildServiceProvider();
        var testDbContext = midExtractionProvider.GetRequiredService<MeepleAiDbContext>();
        // FIX: Use EnsureCreatedAsync for InMemoryDatabase (MigrateAsync only works with relational providers)
        await testDbContext.Database.EnsureCreatedAsync(TestCancellationToken);

        var testUser = await PdfUploadTestHelpers.SeedTestUserAsync(testDbContext);
        var testGame = await PdfUploadTestHelpers.SeedTestGameAsync(testDbContext);

        var handler = midExtractionProvider.GetRequiredService<UploadPdfCommandHandler>();

        var pdfBytes = PdfUploadTestHelpers.CreateValidPdfBytes(1024 * 50);
        var formFile = PdfUploadTestHelpers.CreateMockFormFile("mid_extraction.pdf", pdfBytes);

        var command = new UploadPdfCommand(
            GameId: testGame.Id.ToString(),
            Metadata: null,
            null,  // PrivateGameId
            UserId: testUser.Id,
            File: formFile);

        using var cts = PdfUploadTestHelpers.CreateDelayedCancellation(100); // Cancel mid-extraction

        // Act
        var result = await handler.Handle(command, cts.Token);

        // Assert - Upload completes, background processing handles cancellation
        result.Should().NotBeNull();
    }

    /// <summary>
    /// Cancellazione mentre il servizio di embedding lavora a un lotto (<c>ProcessPdfAsync</c>, passo 3).
    ///
    /// <para><b>Scenario reale:</b> l'utente annulla l'elaborazione (<c>CancelPdfProcessingCommand</c> →
    /// <c>IBackgroundTaskService.CancelTask</c>) mentre la richiesta del secondo lotto è in volo.</para>
    /// <para><b>Atteso:</b> la chiamata finisce con la cancellazione senza aspettare il servizio; il documento
    /// passa a <c>Failed</c> («Processing cancelled by user», categoria <c>Cancelled</c>, con <c>Embedding</c>
    /// come punto di rottura); la prenotazione di quota è rilasciata e non confermata; del lavoro a metà non resta
    /// niente nel database (né passaggi, né documento vettoriale, né righe vettoriali); nessun errore nei log.</para>
    /// <para><b>Prova di essere arrivati alla fase:</b> il doppio ha ricevuto due richieste (il primo lotto
    /// completo, il secondo fermato) e l'indicizzazione non è mai partita. Prima di #4531 il test usava un
    /// <c>IBackgroundTaskService</c> senza setup: la lambda non girava mai e il servizio di embedding non veniva
    /// nemmeno chiamato.</para>
    /// </summary>
    [Fact(Timeout = 120_000)]
    public async Task UploadPdf_WhenCancelledMidEmbeddingBatch_StopsGracefully()
    {
        // Arrange - il secondo lotto resta in volo finché non lo cancella il test
        var embeddingHold = new HoldPoint(holdOnCall: 2);
        var indexingHold = HoldPoint.Never();
        await using var harness = BuildProcessingHarness(embeddingHold, indexingHold);
        var pdfId = await UploadAsPlainUserAsync(harness, "mid_embedding.pdf");

        // Act - cancella quando il secondo lotto è DENTRO la chiamata. Non lo lascia andare: in produzione
        // è la cancellazione a chiudere la richiesta al provider, non una risposta.
        await CancelWhenInsideAsync(harness, embeddingHold, "the second embedding batch", pdfId,
            "the embedding call has to end with the cancellation");

        // Assert - la fase c'è stata, e il documento è in uno stato finale e pulito
        embeddingHold.Calls.Should().Be(2, "the test must have cancelled inside the second batch, not before or after");
        indexingHold.Calls.Should().Be(0, "a cancelled embedding step must not go on to the indexing step");

        var outcome = await ReadOutcomeAsync(harness, pdfId);
        AssertCancelled(harness, outcome, failedAt: "Embedding");
        outcome.TextChunks.Should().Be(0, "nothing is persisted before the indexing step");
        outcome.VectorDocuments.Should().Be(0);
        outcome.Embeddings.Should().Be(0, "no partial embeddings of the batches already computed");
        AssertQuotaReleased(harness, pdfId);
    }

    /// <summary>
    /// Cancellazione all'ingresso dell'indicizzazione vettoriale (<c>ProcessPdfAsync</c>, passo 4), prima che
    /// <c>IPdfIndexingPipeline.BeginIndexingAsync</c> scriva qualunque cosa. Si chiamava
    /// <c>MidVectorStore_MaintainsConsistency</c>: il nome prometteva il centro della scrittura, e il centro è
    /// nel test <c>WhileSavingVectorRows</c>.
    ///
    /// <para><b>Scenario reale:</b> l'utente annulla quando tutti gli embedding sono già stati calcolati e
    /// l'indicizzazione sta per partire.</para>
    /// <para><b>Atteso:</b> documento <c>Failed</c> con <c>Indexing</c> come punto di rottura, quota rilasciata,
    /// nessun passaggio, documento vettoriale o riga vettoriale rimasti.</para>
    /// <para><b>Prova di essere arrivati alla fase:</b> il doppio dell'indicizzazione ha ricevuto la chiamata, e
    /// gli embedding sono stati richiesti più volte (tutti i lotti) prima di lei.</para>
    /// </summary>
    [Fact(Timeout = 120_000)]
    public async Task UploadPdf_WhenCancelledAtBeginIndexingEntry_WritesNothing()
    {
        // Arrange - gli embedding passano tutti; l'indicizzazione resta ferma all'ingresso
        var embeddingHold = HoldPoint.Never();
        var indexingHold = new HoldPoint(holdOnCall: 1);
        await using var harness = BuildProcessingHarness(embeddingHold, indexingHold);
        var pdfId = await UploadAsPlainUserAsync(harness, "index_entry.pdf");

        // Act
        await CancelWhenInsideAsync(harness, indexingHold, "the vector indexing", pdfId,
            "the indexing call has to end with the cancellation");

        // Assert
        embeddingHold.Calls.Should().BeGreaterThan(1, "every embedding batch has to be computed before the indexing step");
        indexingHold.Calls.Should().Be(1);

        var outcome = await ReadOutcomeAsync(harness, pdfId);
        AssertCancelled(harness, outcome, failedAt: "Indexing");
        outcome.TextChunks.Should().Be(0);
        outcome.VectorDocuments.Should().Be(0, "no vector document announcing an index that was never written");
        outcome.Embeddings.Should().Be(0);
        AssertQuotaReleased(harness, pdfId);
    }

    /// <summary>
    /// Cancellazione mentre <c>PdfChunkPersistence</c> salva le righe vettoriali: è «a metà» della fase di
    /// indicizzazione davvero, con i passaggi già scritti e il documento vettoriale già creato.
    ///
    /// <para><b>Atteso:</b> documento <c>Failed</c> con <c>Indexing</c> come punto di rottura, quota rilasciata,
    /// e nessuna riga vettoriale: né quelle del salvataggio interrotto, che restano nel change tracker del
    /// contesto, né un loro salvataggio in un secondo momento da parte del gestore della cancellazione.</para>
    /// <para><b>Prova di essere arrivati alla fase:</b> l'interceptor ha fermato il primo salvataggio che
    /// contiene righe vettoriali, e a quel punto i passaggi sono già nel database (il test lo legge prima di
    /// cancellare).</para>
    /// </summary>
    [Fact(Timeout = 120_000)]
    public async Task UploadPdf_WhenCancelledWhileSavingVectorRows_LeavesNoPartialEmbeddings()
    {
        // Arrange - il salvataggio delle righe vettoriali resta fermo prima di partire
        var vectorRowsSaveHold = new HoldPoint(holdOnCall: 1);
        await using var harness = BuildProcessingHarness(
            HoldPoint.Never(), HoldPoint.Never(), HoldingSaveChangesInterceptor.ForVectorRows(vectorRowsSaveHold));

        // Act
        var (pdfId, inFlight) = await CancelWhileSavingVectorRowsAsync(harness, vectorRowsSaveHold, "mid_vector_rows.pdf");

        // Assert
        inFlight.TextChunks.Should().BeGreaterThan(0, "the text chunks are saved before the vector rows, so the test is cancelling in the middle of the step");
        inFlight.Embeddings.Should().Be(0);

        var outcome = await ReadOutcomeAsync(harness, pdfId);
        AssertCancelled(harness, outcome, failedAt: "Indexing");
        outcome.Embeddings.Should().Be(0, "the vector rows of a cancelled save must not be saved later by the cancellation handler");
        AssertQuotaReleased(harness, pdfId);
    }

    /// <summary>
    /// Lo stesso scenario del test sopra, con la domanda che quello non fa: che cosa resta del lavoro già
    /// committato prima dell'interruzione?
    ///
    /// <para>Prima di #4609 <c>IPdfIndexingPipeline.IndexAsync</c> scriveva il documento vettoriale già
    /// <c>completed</c> e pubblicava <c>VectorDocumentIndexedEvent</c> (che alza
    /// <c>shared_games.has_knowledge_base</c>) PRIMA che <c>PdfChunkPersistence</c> salvasse passaggi e righe
    /// vettoriali. Una cancellazione, o qualunque errore, fra i due passi lasciava un PDF <c>Failed</c> con un
    /// documento vettoriale «completato», passaggi senza righe vettoriali e un gioco che dichiarava di avere
    /// una base di conoscenza vuota. Ora la riga nasce <c>processing</c> (<c>BeginIndexingAsync</c>) e
    /// <c>IndexAsync</c> la completa per ultimo.</para>
    /// <para>Asserisce l'invariante e non il meccanismo: nessun documento vettoriale <c>completed</c> senza
    /// righe, e il gioco senza base di conoscenza. Il suo controllo è <c>UploadPdf_WhenNotCancelled_RunsEveryPhaseToReady</c>,
    /// che asserisce l'opposto sul percorso riuscito (completo e flag alzato): senza, i due «falso» non
    /// proverebbero niente.</para>
    /// </summary>
    [Fact(Timeout = 120_000)]
    public async Task UploadPdf_WhenCancelledWhileSavingVectorRows_LeavesNoCompletedVectorDocument()
    {
        // Arrange
        var vectorRowsSaveHold = new HoldPoint(holdOnCall: 1);
        await using var harness = BuildProcessingHarness(
            HoldPoint.Never(), HoldPoint.Never(), HoldingSaveChangesInterceptor.ForVectorRows(vectorRowsSaveHold));

        // Act
        var (pdfId, _) = await CancelWhileSavingVectorRowsAsync(harness, vectorRowsSaveHold, "mid_vector_rows_leftovers.pdf");

        // Assert
        var outcome = await ReadOutcomeAsync(harness, pdfId);
        outcome.State.Should().Be("Failed");
        var completedWithoutRows = string.Equals(outcome.VectorDocumentStatus, "completed", StringComparison.Ordinal)
            && outcome.Embeddings == 0;
        completedWithoutRows.Should().BeFalse($"no vector document may claim an index that has no vector rows ({outcome.Describe(harness)})");
        outcome.HasKnowledgeBase.Should().BeFalse("the game must not claim a knowledge base that was never written");
    }

    /// <summary>
    /// Lo stesso invariante del test sopra, con un errore al posto della cancellazione: il salvataggio delle
    /// righe vettoriali fallisce (<c>DbUpdateException</c>, che <c>PdfChunkPersistence</c> rilancia come
    /// <c>InvalidOperationException</c>). Dopo l'errore non deve restare un documento vettoriale <c>completed</c>
    /// senza righe, né un gioco con <c>has_knowledge_base</c> alzato da un indice che non esiste.
    /// </summary>
    [Fact(Timeout = 120_000)]
    public async Task UploadPdf_WhenSavingTheVectorRowsFails_LeavesNoCompletedVectorDocument()
    {
        // Arrange - il primo salvataggio che contiene righe vettoriali fallisce
        await using var harness = BuildProcessingHarness(
            HoldPoint.Never(), HoldPoint.Never(), FailingSaveChangesInterceptor.ForVectorRows(new DbUpdateException("vector rows rejected by the test")));

        // Act
        var pdfId = await UploadAsPlainUserAsync(harness, "vector_rows_fail.pdf");
        await WaitForBackgroundAsync(harness, "a failed vector save has to finish");

        // Assert
        var outcome = await ReadOutcomeAsync(harness, pdfId);
        outcome.State.Should().Be("Failed", outcome.Describe(harness));
        outcome.Embeddings.Should().Be(0, "the interceptor refused the save of the vector rows");
        var completedWithoutRows = string.Equals(outcome.VectorDocumentStatus, "completed", StringComparison.Ordinal)
            && outcome.Embeddings == 0;
        completedWithoutRows.Should().BeFalse($"no vector document may claim an index that has no vector rows ({outcome.Describe(harness)})");
        outcome.HasKnowledgeBase.Should().BeFalse("the game must not claim a knowledge base that was never written");
    }

    /// <summary>
    /// Seguito di #4609: dopo l'errore il PDF è `Failed`, e il suo documento vettoriale (nato `processing` da
    /// <c>BeginIndexingAsync</c>) non deve restare `processing` per sempre: lo stato dell'indicizzazione di un
    /// gioco lo leggerebbe come «in elaborazione» e il polling non finirebbe mai. Diventa `failed`, come
    /// quando fallisce il comando di re-indicizzazione.
    /// </summary>
    [Fact(Timeout = 120_000)]
    public async Task UploadPdf_WhenSavingTheVectorRowsFails_MarksTheVectorDocumentFailed()
    {
        // Arrange - il primo salvataggio che contiene righe vettoriali fallisce
        await using var harness = BuildProcessingHarness(
            HoldPoint.Never(), HoldPoint.Never(), FailingSaveChangesInterceptor.ForVectorRows(new DbUpdateException("vector rows rejected by the test")));

        // Act
        var pdfId = await UploadAsPlainUserAsync(harness, "vector_rows_fail_marks.pdf");
        await WaitForBackgroundAsync(harness, "a failed vector save has to finish");

        // Assert
        var outcome = await ReadOutcomeAsync(harness, pdfId);
        outcome.State.Should().Be("Failed", outcome.Describe(harness));
        outcome.VectorDocuments.Should().Be(1, "the document was created before the save that failed");
        outcome.VectorDocumentStatus.Should().Be("failed", $"a failed PDF cannot leave its vector document in flight ({outcome.Describe(harness)})");
    }

    /// <summary>
    /// Lo stesso per la cancellazione dell'utente: il PDF è `Failed` (categoria `Cancelled`), il suo documento
    /// vettoriale non resta `processing`.
    /// </summary>
    [Fact(Timeout = 120_000)]
    public async Task UploadPdf_WhenCancelledWhileSavingVectorRows_MarksTheVectorDocumentFailed()
    {
        // Arrange
        var vectorRowsSaveHold = new HoldPoint(holdOnCall: 1);
        await using var harness = BuildProcessingHarness(
            HoldPoint.Never(), HoldPoint.Never(), HoldingSaveChangesInterceptor.ForVectorRows(vectorRowsSaveHold));

        // Act
        var (pdfId, _) = await CancelWhileSavingVectorRowsAsync(harness, vectorRowsSaveHold, "mid_vector_rows_marks.pdf");

        // Assert
        var outcome = await ReadOutcomeAsync(harness, pdfId);
        outcome.State.Should().Be("Failed");
        outcome.VectorDocuments.Should().Be(1);
        outcome.VectorDocumentStatus.Should().Be("failed", $"a cancelled PDF cannot leave its vector document in flight ({outcome.Describe(harness)})");
    }

    /// <summary>
    /// Cancellazione mentre si salva una transizione di stato (<c>Embedding → Indexing</c>).
    ///
    /// <para>La transizione accoda <c>PdfStateChangedEvent</c> nel <c>IDomainEventCollector</c> PRIMA del
    /// salvataggio, e lo svuota solo a salvataggio riuscito. Se il salvataggio è interrotto, <c>ChangeTracker.Clear()</c>
    /// non basta: l'evento resta in coda e il salvataggio della pulizia lo scrive nell'outbox, annunciando una
    /// transizione che nel database non è mai avvenuta.</para>
    /// <para><b>Atteso:</b> il punto di rottura è <c>Embedding</c> (è lo stato che ha il database) e l'outbox non
    /// contiene alcun cambio di stato verso <c>Indexing</c> per questo PDF.</para>
    /// </summary>
    [Fact(Timeout = 120_000)]
    public async Task UploadPdf_WhenCancelledDuringAStateTransition_DoesNotPublishATransitionThatNeverHappened()
    {
        // Arrange - il salvataggio che porta il PDF a Indexing resta fermo prima di partire
        var transitionHold = new HoldPoint(holdOnCall: 1);
        await using var harness = BuildProcessingHarness(
            HoldPoint.Never(), HoldPoint.Never(), HoldingSaveChangesInterceptor.ForPdfStateTransition("Indexing", transitionHold));
        var pdfId = await UploadAsPlainUserAsync(harness, "mid_transition.pdf");

        // Act
        await CancelWhenInsideAsync(harness, transitionHold, "the save of the Embedding → Indexing transition", pdfId,
            "the transition save has to end with the cancellation");

        // Assert
        var outcome = await ReadOutcomeAsync(harness, pdfId);
        AssertCancelled(harness, outcome, failedAt: "Embedding");

        var transitions = await ReadStateChangesInOutboxAsync(harness, pdfId);
        transitions.Should().NotContain(t => string.Equals(t.NewState, "indexing", StringComparison.OrdinalIgnoreCase),
            "the outbox must not announce a transition the database never made");
        transitions.Should().Contain(t => string.Equals(t.NewState, "failed", StringComparison.OrdinalIgnoreCase),
            "the cleanup's own transition is announced, so the check above is not reading an empty outbox");
    }

    /// <summary>
    /// Cancellazione dopo il commit di <c>Ready</c>, nella coda di <c>FinalizeProcessingAsync</c> (qui: mentre
    /// invalida la cache dell'AI).
    ///
    /// <para>A quel punto l'elaborazione è finita: il documento è <c>Ready</c> e indicizzato. Il gestore della
    /// cancellazione non deve riscrivere il progresso come <c>Failed</c>, né liberare la quota, né tentare di
    /// portare a <c>Failed</c> un aggregato <c>Ready</c> (<c>InvalidOperationException</c>).</para>
    /// </summary>
    [Fact(Timeout = 120_000)]
    public async Task UploadPdf_WhenCancelledAfterTheReadyCommit_LeavesTheDocumentReady()
    {
        // Arrange - la seconda invalidazione della cache, quella del task in background, resta ferma
        var cacheInvalidationHold = new HoldPoint(holdOnCall: 1);
        await using var harness = BuildProcessingHarness(
            HoldPoint.Never(), HoldPoint.Never(), cacheInvalidationHold: cacheInvalidationHold);
        var pdfId = await UploadAsPlainUserAsync(harness, "after_ready.pdf");

        // Act
        await CancelWhenInsideAsync(harness, cacheInvalidationHold, "the cache invalidation that closes the processing", pdfId,
            "the cache invalidation has to end with the cancellation");

        // Assert
        var outcome = await ReadOutcomeAsync(harness, pdfId);
        outcome.State.Should().Be("Ready", $"a cancellation after the Ready commit must not undo it ({outcome.Describe(harness)})");
        outcome.ProgressStep.Should().Be(Api.Models.ProcessingStep.Completed);
        harness.Background.Errors.Should().BeEmpty("the cleanup must leave a Ready document alone, not throw on it");
        harness.Quota.Verify(q => q.ReleaseQuotaAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Un PDF annullato dall'utente non viene ritentato da <c>RetryFailedPdfsJob</c> (#4610): l'utente ha chiesto
    /// di fermarlo. Prima il job lo rielaborava fino a tre volte, a spese degli embedding.
    /// </summary>
    [Fact(Timeout = 120_000)]
    public async Task UploadPdf_WhenCancelledByTheUser_IsNotRetriedByTheRetryJob()
    {
        // Arrange
        var indexingHold = new HoldPoint(holdOnCall: 1);
        await using var harness = BuildProcessingHarness(HoldPoint.Never(), indexingHold);
        var pdfId = await UploadAsPlainUserAsync(harness, "cancelled_no_retry.pdf");
        await CancelWhenInsideAsync(harness, indexingHold, "the vector indexing", pdfId,
            "the indexing call has to end with the cancellation");

        // Act
        var retried = await RunRetryJobAsync(harness);

        // Assert
        var outcome = await ReadOutcomeAsync(harness, pdfId);
        outcome.State.Should().Be("Failed");
        outcome.ErrorCategory.Should().Be("Cancelled");
        retried.Should().NotContain(pdfId, "the user asked to stop this document");
    }

    /// <summary>
    /// Un timeout che esce come <see cref="OperationCanceledException"/> (<c>TaskCanceledException</c> di
    /// <c>HttpClient</c>) quando il token del task NON è cancellato non è un annullamento dell'utente: il PDF
    /// finisce <c>Failed</c> con una categoria transitoria e il job lo ritenta.
    /// </summary>
    [Fact(Timeout = 120_000)]
    public async Task UploadPdf_WhenTheExtractorTimesOut_FailsAsTransientNotAsCancelled()
    {
        // Arrange - HttpClient scaduto: TaskCanceledException, ma nessuno ha cancellato il token del task
        var timeout = new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing.");
        await using var harness = BuildProcessingHarness(HoldPoint.Never(), HoldPoint.Never(), extractionFailure: timeout);

        // Act
        var pdfId = await UploadAsPlainUserAsync(harness, "extractor_timeout.pdf");
        await WaitForBackgroundAsync(harness, "a failed upload has to finish");
        var retried = await RunRetryJobAsync(harness);

        // Assert
        var outcome = await ReadOutcomeAsync(harness, pdfId);
        outcome.State.Should().Be("Failed", outcome.Describe(harness));
        // #4660: the text is fixed by the category (Unknown, not Cancelled: that is what tells a timeout from the
        // user's cancellation); the timeout's own message stays in the log.
        outcome.Error.Should().Be(PdfFailureText.For(ErrorCategory.Unknown), "a timeout is reported as an unknown failure, not as the user's cancellation");
        outcome.Error.Should().NotContain(timeout.Message);
        outcome.ProgressError.Should().Be(PdfFailureText.For(ErrorCategory.Unknown), "the progress stream is served to the user too");
        harness.Logs.Entries.Should().Contain(l => l.Contains(timeout.Message, StringComparison.Ordinal), "the detail is in the log");
        outcome.ErrorCategory.Should().Be("Unknown");
        retried.Should().Contain(pdfId, "a transient failure stays retriable");
        harness.Quota.Verify(q => q.ReleaseQuotaAsync(It.IsAny<Guid>(), pdfId.ToString(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// #4660: l'estrattore risponde con un fallimento che nomina un servizio interno. La colonna e il progresso,
    /// che pdf-status e lo stream servono all'utente, portano il testo fisso della categoria (<c>Parsing</c>); il
    /// dettaglio sta nel log.
    /// </summary>
    [Fact(Timeout = 120_000)]
    public async Task UploadPdf_WhenTheExtractorReportsAFailure_StoresTheFixedText_AndKeepsTheDetailInTheLog()
    {
        const string detail = "unstructured-service at http://10.0.0.5:8000 answered 502 for /var/data/pdfs/rules.pdf";
        await using var harness = BuildProcessingHarness(HoldPoint.Never(), HoldPoint.Never(), extractionResultError: detail);

        var pdfId = await UploadAsPlainUserAsync(harness, "extractor_reports_failure.pdf");
        await WaitForBackgroundAsync(harness, "a failed upload has to finish");

        var outcome = await ReadOutcomeAsync(harness, pdfId);
        outcome.State.Should().Be("Failed", outcome.Describe(harness));
        outcome.ErrorCategory.Should().Be("Parsing");
        outcome.Error.Should().Be(PdfFailureText.For(ErrorCategory.Parsing));
        outcome.ProgressError.Should().Be(PdfFailureText.For(ErrorCategory.Parsing));
        harness.Logs.Entries.Should().Contain(l => l.Contains(detail, StringComparison.Ordinal), "the detail is in the log");
    }

    /// <summary>
    /// #4660: il servizio di embedding risponde con un fallimento che nomina un host. Stesso contratto, categoria
    /// <c>Service</c>.
    /// </summary>
    [Fact(Timeout = 120_000)]
    public async Task UploadPdf_WhenTheEmbeddingServiceReportsAFailure_StoresTheFixedText_AndKeepsTheDetailInTheLog()
    {
        const string detail = "connection to 10.0.0.7:8001 refused";
        await using var harness = BuildProcessingHarness(HoldPoint.Never(), HoldPoint.Never(), embeddingResultError: detail);

        var pdfId = await UploadAsPlainUserAsync(harness, "embedding_reports_failure.pdf");
        await WaitForBackgroundAsync(harness, "a failed upload has to finish");

        var outcome = await ReadOutcomeAsync(harness, pdfId);
        outcome.State.Should().Be("Failed", outcome.Describe(harness));
        outcome.ErrorCategory.Should().Be("Service");
        outcome.Error.Should().Be(PdfFailureText.For(ErrorCategory.Service));
        outcome.ProgressError.Should().Be(PdfFailureText.For(ErrorCategory.Service));
        harness.Logs.Entries.Should().Contain(l => l.Contains(detail, StringComparison.Ordinal), "the detail is in the log");
    }

    /// <summary>
    /// Controllo dei test di cancellazione sopra: senza cancellazione la stessa pipeline arriva a <c>Ready</c> e
    /// scrive passaggi, documento vettoriale e righe vettoriali.
    ///
    /// <para>Se un giorno la configurazione di questa classe smettesse di bastare alla pipeline (una
    /// dipendenza nuova, un gestore di eventi che non si costruisce), i test di cancellazione diventerebbero
    /// rossi, o peggio verdi, per un motivo che non è quello che dichiarano. Questo dice per primo che il
    /// banco funziona.</para>
    /// </summary>
    [Fact(Timeout = 120_000)]
    public async Task UploadPdf_WhenNotCancelled_RunsEveryPhaseToReady()
    {
        // Arrange
        var embeddingHold = HoldPoint.Never();
        var indexingHold = HoldPoint.Never();
        await using var harness = BuildProcessingHarness(embeddingHold, indexingHold);

        // Act
        var pdfId = await UploadAsPlainUserAsync(harness, "not_cancelled.pdf");
        await WaitForBackgroundAsync(harness, "an uncancelled upload has to finish");

        // Assert
        var outcome = await ReadOutcomeAsync(harness, pdfId);
        outcome.State.Should().Be("Ready", $"the pipeline ended with: {outcome.Describe(harness)}");
        embeddingHold.Calls.Should().BeGreaterThan(1, "the fixture has to be long enough for more than one embedding batch");
        indexingHold.Calls.Should().Be(1);
        outcome.TextChunks.Should().BeGreaterThan(0);
        outcome.VectorDocuments.Should().Be(1);
        outcome.VectorDocumentStatus.Should().Be("completed", "a finished index is announced as completed");
        outcome.HasKnowledgeBase.Should().BeTrue("the flag handler has to run in this harness, or the 'false' of the interrupted-upload tests would prove nothing");
        outcome.Embeddings.Should().Be(outcome.TextChunks, "one vector row per chunk");
        harness.Quota.Verify(q => q.ConfirmQuotaAsync(It.IsAny<Guid>(), pdfId.ToString(), It.IsAny<CancellationToken>()), Times.Once);
        harness.Quota.Verify(q => q.ReleaseQuotaAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Tests cancellation at a random instant (stress test).
    ///
    /// <para><b>Production Scenario:</b> Real-world unpredictable user cancellation timing</para>
    /// <para><b>Expected Behavior:</b> ALWAYS clean up resources, regardless of timing</para>
    /// <para><b>Cosa esercita davvero (#4531):</b> istanti, non fasi. Il ritardo (10-300 ms) cade nella parte
    /// sincrona dell'upload: validazione, ricerca per hash, scrittura del blob, record, prenotazione della quota.
    /// Qui <c>IBackgroundTaskService</c> è un Mock senza setup, quindi la lambda con estrazione, embedding e
    /// indicizzazione non gira mai. Quelle fasi sono nei test <c>MidEmbeddingBatch</c> e <c>MidVectorStore</c>.</para>
    /// </summary>
    [Fact(Timeout = 90000)]
    public async Task UploadPdf_WhenCancelledAtRandomInstant_AlwaysCleansUp()
    {
        // Arrange
        var handler = _serviceProvider!.GetRequiredService<UploadPdfCommandHandler>();
        var testUser = await _dbContext!.Users.FirstAsync(TestCancellationToken);
        var testGame = await _dbContext.SharedGames.FirstAsync(TestCancellationToken);

        var pdfBytes = PdfUploadTestHelpers.CreateValidPdfBytes(1024 * 50);
        var formFile = PdfUploadTestHelpers.CreateMockFormFile("random_cancel.pdf", pdfBytes);

        var command = new UploadPdfCommand(
            GameId: testGame.Id.ToString(),
            Metadata: null,
            null,  // PrivateGameId
            UserId: testUser.Id,
            File: formFile);

        // Use random cancellation helper
        try
        {
            await PdfUploadTestHelpers.ExecuteWithRandomCancellation(
                ct => handler.Handle(command, ct),
                minDelayMs: 10,
                maxDelayMs: 300);
        }
        catch (OperationCanceledException)
        {
            // Expected - random cancellation occurred
        }

        // Verify cleanup regardless of when cancellation occurred
        var docCount = await _dbContext.PdfDocuments.CountAsync(TestContext.Current.CancellationToken);
        docCount.Should().BeLessThanOrEqualTo(1, "at most one document created before cancellation");

        // Note: Files may exist if upload completed before cancellation - this is expected behavior
        // The key test is database consistency, not file existence

        // Verify database consistency
        await PdfUploadTestHelpers.VerifyDatabaseConsistencyAsync(_dbContext, testUser.Id, testGame.Id);
    }

    /// <summary>
    /// Tests comprehensive database consistency after cancellation at several instants of the upload.
    ///
    /// <para><b>Test Category:</b> Cancellation - Consistency Verification</para>
    /// <para><b>Production Scenario:</b> Verify no data corruption after cancellation at ANY instant</para>
    /// <para><b>Expected Behavior:</b> FK integrity maintained, no orphaned records, referential consistency</para>
    /// <para><b>Acceptance Criteria:</b> Issue #1819 (#1736) - Database consistency verification (EXPLICIT)</para>
    /// </summary>
    /// <remarks>
    /// <para>This test satisfies the acceptance criterion "Database consistency verification" for the
    /// synchronous part of the upload only (#4531). The five delays (10-500 ms) are instants, not pipeline
    /// phases: <c>IBackgroundTaskService</c> is a Mock without setup, so the lambda with extraction,
    /// embedding and indexing never runs, so no delay can reach them. The phases of
    /// <c>ProcessPdfAsync</c> are covered by <c>MidEmbeddingBatch</c> and <c>MidVectorStore</c>.</para>
    ///
    /// Verifies:
    /// - User and Game entities remain untouched
    /// - No orphaned PdfDocument records
    /// - FK relationships valid
    /// - Transaction atomicity across all cancellation scenarios
    /// </remarks>
    [Fact(Timeout = 90_000)]
    public async Task UploadPdf_WhenCancelledAtVariousInstants_MaintainsDatabaseConsistency()
    {
        // Arrange - Test cancellation at 5 different instants
        var testUser = await _dbContext!.Users.FirstAsync(TestCancellationToken);
        var testGame = await _dbContext.SharedGames.FirstAsync(TestCancellationToken);

        var cancellationTimings = new[] { 10, 50, 100, 200, 500 };

        foreach (var delayMs in cancellationTimings)
        {
            using var scope = _serviceProvider!.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<UploadPdfCommandHandler>();

            var pdfBytes = PdfUploadTestHelpers.CreateValidPdfBytes(1024 * 20);
            var formFile = PdfUploadTestHelpers.CreateMockFormFile($"consistency_{delayMs}.pdf", pdfBytes);

            var command = new UploadPdfCommand(
                GameId: testGame.Id.ToString(),
                Metadata: null,
                null,  // PrivateGameId
                UserId: testUser.Id,
                File: formFile);

            using var cts = PdfUploadTestHelpers.CreateDelayedCancellation(delayMs);

            // Act
            try
            {
                await handler.Handle(command, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected for some timings
            }

            // Assert - Verify consistency after EACH scenario
            await PdfUploadTestHelpers.VerifyDatabaseConsistencyAsync(_dbContext, testUser.Id, testGame.Id);

            // Verify no FK violations
            var orphanedCount = await _dbContext.PdfDocuments
                .Where(d => !_dbContext.Users.Any(u => u.Id == d.UploadedByUserId) ||
                           (d.SharedGameId != null && !_dbContext.SharedGames.Any(g => g.Id == d.SharedGameId)))
                .CountAsync(TestContext.Current.CancellationToken);
            orphanedCount.Should().Be(0, $"no orphaned documents after cancellation at {delayMs}ms");

            // Clean up for next iteration
            var createdDocs = await _dbContext.PdfDocuments
                .Where(d => d.SharedGameId == testGame.Id)
                .ToListAsync(TestContext.Current.CancellationToken);
            _dbContext.PdfDocuments.RemoveRange(createdDocs);
            await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Final verification
        var userStillExists = await _dbContext.Users.AnyAsync(u => u.Id == testUser.Id, TestCancellationToken);
        var gameStillExists = await _dbContext.SharedGames.AnyAsync(g => g.Id == testGame.Id, TestCancellationToken);

        userStillExists.Should().BeTrue("user should survive all cancellation scenarios");
        gameStillExists.Should().BeTrue("game should survive all cancellation scenarios");
    }

    // -------------------------------------------------------------------------------------------
    // Banco per le fasi di elaborazione (#4531)
    //
    // I test di sopra che chiamano il gestore con il banco di InitializeAsync esercitano solo la parte
    // sincrona dell'upload: lì IBackgroundTaskService è un Mock senza setup e la lambda con
    // ProcessPdfAsync non gira mai. Questo banco la fa girare: estrazione, suddivisione, embedding,
    // indicizzazione e chiusura vanno su Postgres, e i servizi esterni sono doppi.
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// Costruisce il gestore con la pipeline di elaborazione vera dietro. Nessun servizio esterno e nessun
    /// LLM: l'embedding è un doppio che calcola vettori deterministici (<see cref="MockEmbeddingService"/>) e
    /// può fermarsi dentro la chiamata; l'estrazione del testo è un doppio; la scrittura vettoriale va
    /// su Postgres con <c>PdfIndexingPipeline</c> e <c>PdfChunkPersistence</c> veri.
    /// </summary>
    private ProcessingHarness BuildProcessingHarness(
        HoldPoint embeddingHold,
        HoldPoint indexingHold,
        SaveChangesInterceptor? saveInterceptor = null,
        Exception? extractionFailure = null,
        HoldPoint? cacheInvalidationHold = null,
        string? extractionResultError = null,
        string? embeddingResultError = null)
    {
        var background = new CancellableBackgroundTaskService();
        var logs = new ErrorLogCapture();
        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);
        services.AddLogging(builder => builder.AddProvider(logs));

        // Registrati prima di RegisterDefaultMockServices, che non sovrascrive ciò che trova già.
        services.AddSingleton<IBackgroundTaskService>(background);

        var quota = new Mock<IPdfUploadQuotaService>();
        quota.Setup(q => q.CheckQuotaAsync(It.IsAny<Guid>(), It.IsAny<UserTier>(), It.IsAny<AuthRole>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PdfUploadQuotaResult.Success(0, int.MaxValue, 0, int.MaxValue, DateTime.MaxValue, DateTime.MaxValue));
        quota.Setup(q => q.ReserveQuotaAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(QuotaReservationResult.Success(DateTime.UtcNow.AddHours(1)));
        services.AddSingleton(quota.Object);

        var vectors = new MockEmbeddingService(dimensions: 768);
        var embedding = new Mock<IEmbeddingService>();
        embedding.Setup(e => e.GetModelName()).Returns(vectors.GetModelName());
        embedding.Setup(e => e.GetEmbeddingDimensions()).Returns(vectors.GetEmbeddingDimensions());
        embedding
            .Setup(e => e.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (List<string> texts, string language, CancellationToken ct) =>
            {
                if (embeddingResultError is not null)
                {
                    return EmbeddingResult.CreateFailure(embeddingResultError);
                }

                try
                {
                    await embeddingHold.EnterAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Come EmbeddingService: l'eccezione del provider, cancellazione compresa, esce come
                    // risultato fallito (GenerateWithFallbackAsync la cattura).
                    return EmbeddingResult.CreateFailure("embedding generation failed: The operation was canceled.");
                }

                return await vectors.GenerateEmbeddingsAsync(texts, language, ct).ConfigureAwait(false);
            });
        services.AddSingleton(embedding.Object);

        var extractor = new Mock<IPdfTextExtractor>();
        var extraction = extractor
            .Setup(e => e.ExtractPagedTextAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()));
        if (extractionResultError is not null)
        {
            extraction.ReturnsAsync(PagedTextExtractionResult.CreateFailure(extractionResultError));
        }
        else if (extractionFailure is null)
        {
            extraction.ReturnsAsync(BuildRulebookExtraction(pages: 30));
        }
        else
        {
            extraction.ThrowsAsync(extractionFailure);
        }

        services.AddSingleton(extractor.Object);

        var tableExtractor = new Mock<IPdfTableExtractor>();
        tableExtractor
            .Setup(t => t.ExtractStructuredContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(StructuredContentResult.CreateFailure("Skipped in test"));
        services.AddSingleton(tableExtractor.Object);

        services.AddSingleton<ITextChunkingService, TextChunkingService>();

        // Ciò che la pipeline vera chiede e CreateBase non registra: la cache semantica, e le dipendenze dei
        // gestori di VectorDocumentIndexedEvent, che IndexAsync pubblica in linea (il secondo alza
        // shared_games.has_knowledge_base).
        services.AddSingleton(Mock.Of<ISemanticResponseCache>());
        services.AddScoped(_ => Mock.Of<Api.BoundedContexts.KnowledgeBase.Domain.Repositories.IVectorDocumentRepository>());
        services.AddScoped<ICacheInvalidationRetryPolicy>(_ => new PassthroughRetryPolicy());
        services.AddScoped<PdfIndexingPipeline>();
        if (saveInterceptor is not null)
        {
            services.ConfigureDbContext<MeepleAiDbContext>(options => options.AddInterceptors(saveInterceptor));
        }

        services.AddScoped<IPdfIndexingPipeline>(sp =>
            new HoldingPdfIndexingPipeline(sp.GetRequiredService<PdfIndexingPipeline>(), indexingHold));

        services.Configure<PdfProcessingOptions>(options => options.MaxFileSizeBytes = 10485760);
        services.AddScoped<UploadPdfCommandHandler>();

        if (cacheInvalidationHold is not null)
        {
            // L'upload invalida la cache dell'AI due volte: la richiesta, all'avvio, e il task in background,
            // alla chiusura dell'elaborazione, DOPO il commit di Ready. Si ferma solo la seconda.
            var cache = new Mock<IAiResponseCacheService>();
            cache.Setup(c => c.InvalidateGameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(async (string gameId, CancellationToken ct) =>
                {
                    if (CancellableBackgroundTaskService.InBackgroundTask.Value)
                    {
                        await cacheInvalidationHold.EnterAsync(ct).ConfigureAwait(false);
                    }
                });
            services.AddSingleton(cache.Object);
        }

        RegisterDefaultMockServices(services);

        return new ProcessingHarness(services.BuildServiceProvider(), background, quota, logs);
    }

    /// <summary>
    /// Un testo abbastanza lungo per più lotti di embedding (il gestore ne calcola 20 passaggi alla volta).
    /// </summary>
    private static PagedTextExtractionResult BuildRulebookExtraction(int pages)
    {
        var chunks = new List<PageTextChunk>();
        var offset = 0;
        for (var page = 1; page <= pages; page++)
        {
            var text = $"Pagina {page}. " + string.Concat(Enumerable.Repeat($"Il giocatore {page} pesca una carta e la gioca. ", 16));
            chunks.Add(new PageTextChunk(page, text, offset, offset + text.Length));
            offset += text.Length + 2; // il testo intero unisce le pagine con una riga vuota
        }

        return PagedTextExtractionResult.CreateSuccess(chunks, pages, offset, ocrTriggered: false);
    }

    /// <summary>
    /// Carica un PDF come utente semplice (<c>Role = "User"</c>): è il ruolo per cui il gestore avvia la
    /// lambda in background invece di accodare il lavoro. Restituisce l'id del documento.
    /// </summary>
    private async Task<Guid> UploadAsPlainUserAsync(ProcessingHarness harness, string fileName)
    {
        var user = await _dbContext!.Users.FirstAsync(TestCancellationToken);
        var game = await _dbContext.SharedGames.FirstAsync(TestCancellationToken);
        user.Role.Should().Be("User", "an admin or editor upload goes to the queue and the lambda under test never runs");

        var command = new UploadPdfCommand(
            GameId: game.Id.ToString(),
            Metadata: null,
            null,  // PrivateGameId
            UserId: user.Id,
            File: PdfUploadTestHelpers.CreateMockFormFile(fileName, PdfUploadTestHelpers.CreateValidPdfBytes(1024 * 10)));

        var handler = harness.RequestScope.ServiceProvider.GetRequiredService<UploadPdfCommandHandler>();
        var result = await handler.Handle(command, TestCancellationToken);

        result.Success.Should().BeTrue($"the upload itself has to succeed: {result.Message}");
        harness.Background.Started.Should().Be(1, "an upload by a plain user is processed by a background task");
        return result.Document!.Id;
    }

    /// <summary>
    /// Carica un PDF, aspetta che <c>PdfChunkPersistence</c> sia sul punto di salvare le righe vettoriali, annulla
    /// l'elaborazione come farebbe l'utente e aspetta la fine del task. Restituisce l'id del documento e lo
    /// stato che aveva un istante prima della cancellazione.
    /// </summary>
    private async Task<(Guid PdfId, PdfOutcome InFlight)> CancelWhileSavingVectorRowsAsync(
        ProcessingHarness harness,
        HoldPoint vectorRowsSaveHold,
        string fileName)
    {
        var pdfId = await UploadAsPlainUserAsync(harness, fileName);

        PdfOutcome? inFlight = null;
        await CancelWhenInsideAsync(harness, vectorRowsSaveHold, "the save of the vector rows", pdfId,
            "the save of the vector rows has to end with the cancellation",
            async () => inFlight = await ReadOutcomeAsync(harness, pdfId));

        return (pdfId, inFlight!);
    }

    /// <summary>
    /// Aspetta che il codice sotto prova sia dentro la chiamata fermata, annulla l'elaborazione come farebbe
    /// l'utente (<c>CancelTask</c> con l'id del documento) e aspetta la fine del task. La chiamata ferma viene
    /// lasciata andare in ogni caso, anche se l'attesa fallisce, perché il task non resti appeso.
    /// </summary>
    private async Task CancelWhenInsideAsync(
        ProcessingHarness harness,
        HoldPoint hold,
        string where,
        Guid pdfId,
        string becauseItHasToEnd,
        Func<Task>? beforeCancel = null)
    {
        await WaitUntilInsideAsync(harness, hold, where, pdfId);
        try
        {
            if (beforeCancel is not null)
            {
                await beforeCancel();
            }

            harness.Background.CancelTask(pdfId.ToString()).Should().BeTrue("the processing task is still running");
            await WaitForBackgroundAsync(harness, becauseItHasToEnd);
        }
        finally
        {
            hold.Release();
        }
    }

    /// <summary>
    /// Esegue <c>RetryFailedPdfsJob</c> sul database del banco, con un mediator-doppio al posto del vero, e
    /// restituisce gli id dei PDF per cui ha inviato <c>RetryPdfProcessingCommand</c>.
    /// </summary>
    private static async Task<IReadOnlyList<Guid>> RunRetryJobAsync(ProcessingHarness harness)
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<RetryPdfProcessingCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RetryPdfProcessingResult(true, "Pending", 1));

        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        context.SetupGet(c => c.FireTimeUtc).Returns(DateTimeOffset.UtcNow);
        context.SetupProperty(c => c.Result);

        using var scope = harness.Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var job = new RetryFailedPdfsJob(db, mediator.Object, NullLogger<RetryFailedPdfsJob>.Instance);
        await job.Execute(context.Object);

        return mediator.Invocations
            .Select(i => i.Arguments[0])
            .OfType<RetryPdfProcessingCommand>()
            .Select(c => c.PdfId)
            .ToList();
    }

    /// <summary>I cambi di stato di un PDF scritti nell'outbox degli eventi di dominio.</summary>
    private static async Task<IReadOnlyList<(string PreviousState, string NewState)>> ReadStateChangesInOutboxAsync(
        ProcessingHarness harness,
        Guid pdfId)
    {
        using var scope = harness.Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var payloads = await db.DomainEventOutbox
            .Where(o => o.EventType == "pdf.state.changed")
            .Select(o => o.PayloadJson)
            .ToListAsync(TestCancellationToken);

        var changes = new List<(string, string)>();
        foreach (var payload in payloads)
        {
            using var json = System.Text.Json.JsonDocument.Parse(payload);
            var root = json.RootElement;
            if (root.GetProperty("pdfDocumentId").GetGuid() == pdfId)
            {
                changes.Add((root.GetProperty("previousState").GetString()!, root.GetProperty("newState").GetString()!));
            }
        }

        return changes;
    }

    /// <summary>
    /// Aspetta che il codice sotto prova sia dentro la chiamata fermata. Se la pipeline finisce prima, o non ci
    /// arriva in tempo, il fallimento dice dove si è fermata: un test che non raggiunge la fase non deve
    /// restare verde.
    /// </summary>
    private async Task WaitUntilInsideAsync(ProcessingHarness harness, HoldPoint hold, string where, Guid pdfId)
    {
        var finished = harness.Background.WaitForAllAsync();
        var first = await Task.WhenAny(hold.Entered, finished, Task.Delay(TimeSpan.FromSeconds(60), TestCancellationToken));
        if (first == hold.Entered)
        {
            return;
        }

        var outcome = await ReadOutcomeAsync(harness, pdfId);
        Assert.Fail($"The pipeline must reach {where} before the test cancels it, but it did not: {outcome.Describe(harness)}");
    }

    private static async Task WaitForBackgroundAsync(ProcessingHarness harness, string because)
    {
        var finished = harness.Background.WaitForAllAsync();
        if (await Task.WhenAny(finished, Task.Delay(TimeSpan.FromSeconds(30), TestCancellationToken)) != finished)
        {
            Assert.Fail($"The background task did not finish within 30 s: {because}.");
        }

        await finished;
    }

    /// <summary>Lo stato del documento e le righe che ha lasciato, letti con un contesto nuovo.</summary>
    private static async Task<PdfOutcome> ReadOutcomeAsync(ProcessingHarness harness, Guid pdfId)
    {
        using var scope = harness.Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var pdf = await db.PdfDocuments.AsNoTracking().SingleAsync(p => p.Id == pdfId, TestCancellationToken);
        var textChunks = await db.TextChunks.CountAsync(t => t.PdfDocumentId == pdfId, TestCancellationToken);
        var vectorDocuments = await db.VectorDocuments.CountAsync(v => v.PdfDocumentId == pdfId, TestCancellationToken);
        var embeddings = await db.PgVectorEmbeddings
            .CountAsync(e => db.VectorDocuments.Any(v => v.Id == e.VectorDocumentId && v.PdfDocumentId == pdfId), TestCancellationToken);

        var vectorDocumentStatus = await db.VectorDocuments
            .Where(v => v.PdfDocumentId == pdfId)
            .Select(v => v.IndexingStatus)
            .FirstOrDefaultAsync(TestCancellationToken);
        var hasKnowledgeBase = await db.SharedGames
            .Where(g => g.Id == pdf.SharedGameId)
            .Select(g => g.HasKnowledgeBase)
            .SingleAsync(TestCancellationToken);

        var progress = pdf.ProcessingProgress;

        return new PdfOutcome(
            pdf.ProcessingState,
            pdf.ProcessingError,
            pdf.ErrorCategory,
            pdf.FailedAtState,
            progress?.CurrentStep,
            progress?.ErrorMessage,
            textChunks,
            vectorDocuments,
            vectorDocumentStatus,
            embeddings,
            hasKnowledgeBase);
    }

    private static void AssertCancelled(ProcessingHarness harness, PdfOutcome outcome, string failedAt)
    {
        outcome.State.Should().Be("Failed", $"a cancelled upload ends as Failed, not stuck in flight ({outcome.Describe(harness)})");
        outcome.Error.Should().Be("Processing cancelled by user");
        outcome.ErrorCategory.Should().Be("Cancelled", "a user's cancellation is neither retried nor notified as a failure (#4610)");
        outcome.FailedAtState.Should().Be(failedAt);

        // Ciò che vede la UI (GET /pdfs/{id}/progress) è il progresso, non la colonna di stato.
        outcome.ProgressStep.Should().Be(Api.Models.ProcessingStep.Failed);
        outcome.ProgressError.Should().Be("Processing cancelled by user");

        harness.Logs.Entries.Should().BeEmpty("a cancellation is not an error and must not be logged as one");
    }

    private static void AssertQuotaReleased(ProcessingHarness harness, Guid pdfId)
    {
        harness.Quota.Verify(q => q.ReleaseQuotaAsync(It.IsAny<Guid>(), pdfId.ToString(), It.IsAny<CancellationToken>()), Times.Once);
        harness.Quota.Verify(q => q.ConfirmQuotaAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed record PdfOutcome(
        string State,
        string? Error,
        string? ErrorCategory,
        string? FailedAtState,
        Api.Models.ProcessingStep? ProgressStep,
        string? ProgressError,
        int TextChunks,
        int VectorDocuments,
        string? VectorDocumentStatus,
        int Embeddings,
        bool HasKnowledgeBase)
    {
        public string Describe(ProcessingHarness harness) =>
            $"state={State}, category={ErrorCategory}, failedAt={FailedAtState}, progress={ProgressStep}, error=\"{Error}\", textChunks={TextChunks}, vectorDocuments={VectorDocuments} (status {VectorDocumentStatus}), embeddings={Embeddings}, hasKnowledgeBase={HasKnowledgeBase}, " +
            $"background exceptions=[{string.Join("; ", harness.Background.Errors.Select(e => $"{e.GetType().Name}: {e.Message}"))}], " +
            $"logged errors=[{string.Join(" || ", harness.Logs.Entries.Take(3).Select(l => l.Length > 1500 ? l[..1500] : l))}]";
    }

    private sealed class ProcessingHarness : IAsyncDisposable
    {
        public ProcessingHarness(
            ServiceProvider provider,
            CancellableBackgroundTaskService background,
            Mock<IPdfUploadQuotaService> quota,
            ErrorLogCapture logs)
        {
            Provider = provider;
            Background = background;
            Quota = quota;
            Logs = logs;
            RequestScope = provider.CreateScope();
        }

        public ServiceProvider Provider { get; }

        public CancellableBackgroundTaskService Background { get; }

        public Mock<IPdfUploadQuotaService> Quota { get; }

        /// <summary>I log di errore, per dire nel fallimento che cosa è andato storto.</summary>
        public ErrorLogCapture Logs { get; }

        /// <summary>Lo scope «della richiesta» del gestore: vive quanto il test, come la lambda in background.</summary>
        public IServiceScope RequestScope { get; }

        public async ValueTask DisposeAsync()
        {
            // Un test che fallisce non lascia la lambda appesa sul provider già smaltito.
            await Task.WhenAny(Background.WaitForAllAsync(), Task.Delay(TimeSpan.FromSeconds(10))).ConfigureAwait(false);
            RequestScope.Dispose();
            await Provider.DisposeAsync().ConfigureAwait(false);
        }
    }
}
