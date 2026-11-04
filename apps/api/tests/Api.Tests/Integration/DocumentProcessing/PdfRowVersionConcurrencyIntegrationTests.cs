using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.Services.Pdf;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// Integration tests for PdfDocumentEntity RowVersion optimistic concurrency.
/// Issue #1802. Uses Barrier-synchronized parallel tasks for real race conditions
/// against PostgreSQL Testcontainers.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "1802")]
public sealed class PdfRowVersionConcurrencyIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    private static readonly Guid TestUserId = new("A0000000-0000-0000-0000-000000001802");
    private static readonly Guid TestSharedGameId = new("B0000000-0000-0000-0000-000000001802");

    public PdfRowVersionConcurrencyIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_rowversion_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);

        // Register real AgentDefinitionRepository (required by DeleteKbDocumentCommandHandler
        // which needs to update consuming agents' KbCardIds).
        services.AddScoped<IAgentDefinitionRepository, AgentDefinitionRepository>();

        // #3633: senza questa registrazione il test fallisce con «Unable to resolve service for
        // type 'IProcessingJobRepository'». In produzione è registrata da
        // DocumentProcessingServiceExtensions (#4731 queue commands); questa fixture costruisce il
        // DI a mano e non carica quel bounded context, quindi la dipendenza va aggiunta qui.
        services.AddScoped<
            Api.BoundedContexts.DocumentProcessing.Domain.Repositories.IProcessingJobRepository,
            Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence.ProcessingJobRepository>();
        services.AddScoped<
            Api.BoundedContexts.DocumentProcessing.Domain.Repositories.IPdfDocumentRepository,
            Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence.PdfDocumentRepository>();

        // Blob storage — best-effort in DeleteKbDocumentCommandHandler; mock so no physical I/O.
        var blobMock = new Mock<IBlobStorageService>();
        blobMock.Setup(b => b.DeleteAsync(
                It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        services.AddSingleton<IBlobStorageService>(blobMock.Object);

        // AI response cache invalidation — best-effort in handler; mock.
        var cacheMock = new Mock<IAiResponseCacheService>();
        cacheMock.Setup(c => c.InvalidateGameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        services.AddSingleton<IAiResponseCacheService>(cacheMock.Object);

        // Vector store — mock so pgvector_embeddings deletion doesn't fail in Testcontainers.
        var vectorStoreMock = new Mock<IVectorStoreAdapter>();
        vectorStoreMock.Setup(v => v.DeleteByVectorDocumentIdAsync(
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        services.AddScoped<IVectorStoreAdapter>(_ => vectorStoreMock.Object);

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        await TestMigrationHelper.MigrateWithRetryAsync(_dbContext, TestCancellationToken);
        await SeedBaseAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext is not null)
            await _dbContext.DisposeAsync();

        if (_serviceProvider is IAsyncDisposable d)
            await d.DisposeAsync();
        else
            (_serviceProvider as IDisposable)?.Dispose();

        if (!string.IsNullOrEmpty(_databaseName))
        {
            try
            {
                await _fixture.DropIsolatedDatabaseAsync(_databaseName);
            }
            catch
            {
                // best-effort cleanup — test isolation already achieved
            }
        }
    }

    private async Task SeedBaseAsync()
    {
        _dbContext!.Set<UserEntity>().Add(new UserEntity
        {
            Id = TestUserId,
            Email = "rowversion-test@meepleai.test",
            PasswordHash = "x",
            DisplayName = "RowVersion Test",
        });
        _dbContext.Set<SharedGameEntity>().Add(new SharedGameEntity
        {
            Id = TestSharedGameId,
            Title = "RowVersion Test Game",
        });
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    private async Task<PdfDocumentEntity> SeedReadyPdfAsync(string? indexerVersion = null)
    {
        var pdf = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            FileName = "rowversion.pdf",
            FilePath = "/tmp/rowversion.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedByUserId = TestUserId,
            SharedGameId = TestSharedGameId,
            ProcessingState = nameof(PdfProcessingState.Ready),
            IndexerVersion = indexerVersion ?? IndexerVersionRegistry.Current.Version,
        };
        _dbContext!.PdfDocuments.Add(pdf);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
        return pdf;
    }

    // ──────────────────────────────────────────────────────────────────────
    // Scenario 1: Parallel two reindex — only one succeeds
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Parallel_TwoReindex_OnlyOneSucceeds()
    {
        var pdf = await SeedReadyPdfAsync();

        using var barrier = new Barrier(participantCount: 2);

        Task<Exception?> RunReindex() => Task.Run(async () =>
        {
            using var scope = _serviceProvider!.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            barrier.SignalAndWait(TimeSpan.FromSeconds(5));
            try
            {
                await mediator.Send(
                    new ReindexDocumentCommand(pdf.Id, IndexerVersionRegistry.Current.Version),
                    TestCancellationToken);
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        });

        var taskA = RunReindex();
        var taskB = RunReindex();

        var exceptions = await Task.WhenAll(taskA, taskB);

        var successes = exceptions.Count(ex => ex is null);
        var conflicts = exceptions.Count(ex => ex is ConflictException);
        successes.Should().Be(1, "exactly one reindex must win the race");
        conflicts.Should().Be(1, "exactly one reindex must lose with ConflictException");

        var reloaded = await _dbContext!.PdfDocuments.AsNoTracking()
            .FirstAsync(p => p.Id == pdf.Id, TestCancellationToken);
        reloaded.ProcessingState.Should().Be(nameof(PdfProcessingState.Pending));

        // #3651: il token è ora `Xmin` (colonna di sistema Postgres) e non più `byte[] RowVersion`
        // su una `bytea`, che restava NULL da quando #2305 ha rimosso il trigger che la popolava.
        // L'assert cambia di conseguenza: xmin è un `uint` non nullable, quindi la proprietà che
        // conta è che sia CAMBIATO dopo l'update — che è ciò che rende rilevabile il conflitto.
        reloaded.Xmin.Should().NotBe(0u, "xmin è valorizzato dal server a ogni UPDATE");
        reloaded.Xmin.Should().NotBe(pdf.Xmin, "l'UPDATE deve aver avanzato il token di concorrenza");
    }

    // ──────────────────────────────────────────────────────────────────────
    // Scenario 2: Reindex races with Delete — nessuno stato orfano
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reindex_RacesWithDelete_LeavesNoOrphanState()
    {
        var pdf = await SeedReadyPdfAsync();

        using var barrier = new Barrier(participantCount: 2);

        Task<(string Op, Exception? Exception)> RunReindex() => Task.Run(async () =>
        {
            using var scope = _serviceProvider!.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            barrier.SignalAndWait(TimeSpan.FromSeconds(5));
            try
            {
                await mediator.Send(
                    new ReindexDocumentCommand(pdf.Id, IndexerVersionRegistry.Current.Version),
                    TestCancellationToken);
                return ("reindex", (Exception?)null);
            }
            catch (Exception ex) { return ("reindex", (Exception?)ex); }
        });

        Task<(string Op, Exception? Exception)> RunDelete() => Task.Run(async () =>
        {
            using var scope = _serviceProvider!.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            barrier.SignalAndWait(TimeSpan.FromSeconds(5));
            try
            {
                await mediator.Send(new DeleteKbDocumentCommand(pdf.Id), TestCancellationToken);
                return ("delete", (Exception?)null);
            }
            catch (Exception ex) { return ("delete", (Exception?)ex); }
        });

        var results = await Task.WhenAll(RunReindex(), RunDelete());

        // #3633: due interleaving sono entrambi legittimi, e questo test asseriva solo il primo.
        //
        //   (a) la barriera li allinea davvero → uno dei due legge un `xmin` che l'altro ha già
        //       avanzato, e prende ConflictException. Un successo, un conflitto.
        //   (b) il reindex committa PRIMA che il delete legga → il delete trova il token fresco e
        //       cancella senza conflitto. Due successi.
        //
        // (b) non è concorrenza mancata: DeleteKbDocumentCommandHandler non ha (per scelta) una
        // guardia di stato, e cancellare un documento appena accodato per il reindex è
        // un'operazione sensata. Asserire `successCount == 1` pretendeva un esito deterministico da
        // uno scenario che deterministico non è: il test era flaky per costruzione, e il rosso
        // misurato in #3633 era l'interleaving (b). Ciò che regge in ENTRAMBI gli ordinamenti è
        // qui sotto — stessa forma di Scenario 3, che l'asimmetria l'aveva già riconosciuta.

        //   (c) #3866 — il DELETE committa prima che il reindex LEGGA. E' l'immagine speculare di
        //       (b), e il reindex trova una riga che non c'e' piu': NotFoundException, cioe' un 404.
        //       E' la risposta corretta — reindicizzare un documento cancellato deve dare 404, non
        //       500 — ma il commento sopra ne elencava due su tre e la guardia qui sotto ammetteva
        //       solo ConflictException. Non e' emerso finora perche' DeleteKbDocumentCommandHandler
        //       leggeva senza tracciare (#3866): la cancellazione poteva morire su un conflitto di
        //       identita' invece di completare, e questo ordinamento restava raro.
        results.Should().OnlyContain(
            r => r.Exception == null
              || r.Exception is ConflictException
              || (r.Op == "reindex" && r.Exception is NotFoundException),
            "i soli fallimenti ammessi sono il conflitto di concorrenza e il 404 del reindex su un documento gia' cancellato");

        var conflictCount = results.Count(r => r.Exception is ConflictException);
        conflictCount.Should().BeLessThanOrEqualTo(1,
            "al più una delle due operazioni può conflittare, mai entrambe");

        // (c) non e' un esito qualsiasi: se il reindex ha visto 404, il delete DEVE essere passato.
        // Senza questo accoppiamento la guardia allargata ammetterebbe anche un 404 spurio.
        if (results.Any(r => r.Op == "reindex" && r.Exception is NotFoundException))
        {
            results.Single(r => r.Op == "delete").Exception
                .Should().BeNull("il reindex ha trovato 404 solo perche' il delete aveva gia' cancellato");
        }

        var stillExists = await _dbContext!.PdfDocuments.AsNoTracking()
            .AnyAsync(p => p.Id == pdf.Id, TestCancellationToken);

        if (stillExists)
        {
            // Solo l'interleaving (a) con il reindex vincitore lascia il documento in piedi.
            results.Single(r => r.Op == "delete").Exception
                .Should().BeOfType<ConflictException>(
                    "se il documento esiste ancora, il delete deve aver conflittato");

            var reloaded = await _dbContext.PdfDocuments.AsNoTracking()
                .FirstAsync(p => p.Id == pdf.Id, TestCancellationToken);
            reloaded.ProcessingState.Should().Be(nameof(PdfProcessingState.Pending),
                "il reindex vincitore riaccoda il documento");
        }
        else
        {
            // Il delete è passato — che abbia vinto la gara (a) o sia arrivato dopo il reindex
            // (b). L'invariante che conta è la stessa nei due casi, ed è quella che protegge i
            // dati: la cancellazione non lascia chunk appesi a un documento che non c'è più.
            var orphanChunks = await _dbContext.TextChunks.AsNoTracking()
                .CountAsync(tc => tc.PdfDocumentId == pdf.Id, TestCancellationToken);
            orphanChunks.Should().Be(0, "il cascade delete non deve lasciare TextChunk orfani");
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // Scenario 3: Reindex races with background pipeline mutation
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reindex_RacesWithBackgroundPipeline_AdminGets409()
    {
        var pdf = await SeedReadyPdfAsync();

        using var barrier = new Barrier(participantCount: 2);

        Task<(string Op, Exception? Exception)> RunAdminReindex() => Task.Run(async () =>
        {
            using var scope = _serviceProvider!.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            // Pre-load the entity to confirm it exists before racing (AsNoTracking — no scope pollution).
            // Note: NO `using` — db is the scoped DbContext that mediator.Send will also resolve.
            // `using` would Dispose() it prematurely at end-of-Task scope, leaving the
            // mediator with a disposed context. The IServiceScope itself owns disposal.
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            _ = await db.PdfDocuments.AsNoTracking()
                .FirstAsync(p => p.Id == pdf.Id, TestCancellationToken);

            barrier.SignalAndWait(TimeSpan.FromSeconds(5));
            try
            {
                await mediator.Send(
                    new ReindexDocumentCommand(pdf.Id, IndexerVersionRegistry.Current.Version),
                    TestCancellationToken);
                return ("admin", (Exception?)null);
            }
            catch (Exception ex) { return ("admin", (Exception?)ex); }
        });

        Task<(string Op, Exception? Exception)> RunPipelineTick() => Task.Run(async () =>
        {
            using var scope = _serviceProvider!.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

            barrier.SignalAndWait(TimeSpan.FromSeconds(5));
            try
            {
                // Simulate a Category B pipeline mutation directly (no real pipeline service spin-up).
                var entity = await db.PdfDocuments
                    .FirstAsync(p => p.Id == pdf.Id, TestCancellationToken);
                entity.ProcessingState = nameof(PdfProcessingState.Chunking);
                await db.SaveChangesAsync(TestCancellationToken);
                return ("pipeline", (Exception?)null);
            }
            catch (Exception ex) { return ("pipeline", (Exception?)ex); }
        });

        var results = await Task.WhenAll(RunAdminReindex(), RunPipelineTick());

        // Race outcome depends on timing; at most one operation should conflict.
        var failureCount = results.Count(r => r.Exception is not null);
        failureCount.Should().BeLessThanOrEqualTo(1,
            "at most one operation should conflict (or zero if barrier failed to align both in-flight)");

        // The document MUST end in a consistent, recognized state — not partial.
        var final = await _dbContext!.PdfDocuments.AsNoTracking()
            .FirstAsync(p => p.Id == pdf.Id, TestCancellationToken);
        final.ProcessingState.Should().BeOneOf(
            nameof(PdfProcessingState.Pending),    // admin reindex won
            nameof(PdfProcessingState.Chunking));  // pipeline tick won
    }

    // ──────────────────────────────────────────────────────────────────────
    // Scenario 4: dopo un conflitto, la guardia di stato governa il retry
    // ──────────────────────────────────────────────────────────────────────

    // #3633: qui c'era un unico test, `Sequential_RetryAfterConflict_Succeeds`, che dopo la gara
    // riprovava il reindex un secondo dopo e ne pretendeva il successo. Non può riuscire, e non
    // per un difetto: il vincitore lascia il documento in `Pending`, che
    // ReindexDocumentCommandHandler considera in-flight (InFlightStates = tutti gli stati tranne
    // Ready e Failed), quindi la guardia respinge il retry. La guardia è più giovane del test ed
    // è la parte corretta delle due: dopo aver perso una gara di reindex, riprovare è inutile
    // perché il reindex è già in coda.
    //
    // Il test copriva però anche un caso reale — il retry che riesce quando può — e quello non
    // va perso. Da qui i due test: uno pinna la guardia, l'altro il retry legittimo.

    [Fact]
    public async Task Reindex_RetryWhileStillPending_IsRejectedByGuard()
    {
        var pdf = await SeedReadyPdfAsync();
        await ProvokeReindexConflictAsync(pdf.Id);

        using var scope = _serviceProvider!.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var act = () => mediator.Send(
            new ReindexDocumentCommand(pdf.Id, IndexerVersionRegistry.Current.Version),
            TestCancellationToken);

        await act.Should().ThrowAsync<ConflictException>(
                "la guardia di stato rifiuta un reindex su un documento in-flight")
            .WithMessage("*state=" + nameof(PdfProcessingState.Pending) + "*");

        var final = await _dbContext!.PdfDocuments.AsNoTracking()
            .FirstAsync(p => p.Id == pdf.Id, TestCancellationToken);
        final.ProcessingState.Should().Be(nameof(PdfProcessingState.Pending),
            "il retry respinto non deve alterare lo stato del documento");
    }

    [Fact]
    public async Task Reindex_RetryAfterDocumentReturnsToReady_Succeeds()
    {
        var pdf = await SeedReadyPdfAsync();
        await ProvokeReindexConflictAsync(pdf.Id);

        // La pipeline porta a termine il lavoro accodato. Sostituisce il `Task.Delay(1s)` che
        // questo test faceva prima: nessuna pipeline gira in questi test, quindi nessuna attesa
        // avrebbe mai potuto soddisfare la precondizione — il ritardo fisso indovinava un istante
        // che non sarebbe mai arrivato.
        await SimulatePipelineCompletionAsync(pdf.Id);

        using var scope = _serviceProvider!.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        await mediator.Send(
            new ReindexDocumentCommand(pdf.Id, IndexerVersionRegistry.Current.Version),
            TestCancellationToken);

        var final = await _dbContext!.PdfDocuments.AsNoTracking()
            .FirstAsync(p => p.Id == pdf.Id, TestCancellationToken);
        final.ProcessingState.Should().Be(nameof(PdfProcessingState.Pending),
            "il reindex riaccoda il documento");
    }

    // ──────────────────────────────────────────────────────────────────────
    // Helper
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Provoca un conflitto di reindex con due comandi paralleli allineati da una barriera, e
    /// verifica che sia avvenuto davvero prima di restituire il controllo: è la precondizione dei
    /// due scenari di retry, e un test che riparte da una precondizione non verificata misura
    /// altro. Al ritorno il documento è accodato in <c>Pending</c>.
    /// </summary>
    private async Task ProvokeReindexConflictAsync(Guid pdfId)
    {
        using var barrier = new Barrier(participantCount: 2);

        Task<Exception?> RunReindex() => Task.Run(async () =>
        {
            using var scope = _serviceProvider!.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            barrier.SignalAndWait(TimeSpan.FromSeconds(5));
            try
            {
                await mediator.Send(
                    new ReindexDocumentCommand(pdfId, IndexerVersionRegistry.Current.Version),
                    TestCancellationToken);
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        });

        var exceptions = await Task.WhenAll(RunReindex(), RunReindex());

        exceptions.Count(ex => ex is null).Should()
            .Be(1, "esattamente un reindex deve vincere la gara");
        exceptions.Count(ex => ex is ConflictException).Should()
            .Be(1, "esattamente un reindex deve perdere con ConflictException");
    }

    /// <summary>
    /// Simula una pipeline che porta a termine il lavoro accodato da un reindex.
    /// </summary>
    /// <remarks>
    /// #3633: un reindex accodato lascia DUE marcatori di lavoro in corso, e due guardie
    /// indipendenti li leggono. Toglierne uno solo non basta, ed è il motivo per cui la prima
    /// versione di questo helper — che portava soltanto il documento a <c>Ready</c> — faceva
    /// fallire il retry con «already has an active job in the queue»:
    /// <list type="number">
    ///   <item><c>ProcessingState</c> in-flight → guardia in ReindexDocumentCommandHandler</item>
    ///   <item>un ProcessingJob <c>Queued</c>/<c>Processing</c> → guardia in EnqueuePdfCommandHandler</item>
    /// </list>
    /// Una pipeline che termina davvero li rimuove entrambi: chiude il job e riporta il documento
    /// a <c>Ready</c>. È quello che facciamo qui, nello stesso ordine.
    /// <para>
    /// <c>ExecuteUpdateAsync</c> è deliberato: emette un UPDATE senza token di concorrenza, quindi
    /// non conflitta con l'<c>xmin</c> che i reindex hanno già avanzato e non dipende da ciò che il
    /// DbContext condiviso ha in cache.
    /// </para>
    /// </remarks>
    private async Task SimulatePipelineCompletionAsync(Guid pdfId)
    {
        await _dbContext!.ProcessingJobs
            .Where(j => j.PdfDocumentId == pdfId
                && (j.Status == nameof(JobStatus.Queued) || j.Status == nameof(JobStatus.Processing)))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(j => j.Status, nameof(JobStatus.Completed)),
                TestCancellationToken);

        var rows = await _dbContext.PdfDocuments
            .Where(p => p.Id == pdfId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    p => p.ProcessingState, nameof(PdfProcessingState.Ready)),
                TestCancellationToken);

        rows.Should().Be(1,
            "la precondizione deve aver aggiornato esattamente il documento sotto test");

        var stillBlocked = await _dbContext.ProcessingJobs.AsNoTracking()
            .AnyAsync(j => j.PdfDocumentId == pdfId
                && (j.Status == nameof(JobStatus.Queued) || j.Status == nameof(JobStatus.Processing)),
                TestCancellationToken);
        stillBlocked.Should().BeFalse(
            "nessun job attivo deve restare, o la guardia della coda respingerebbe il reindex");
    }
}
