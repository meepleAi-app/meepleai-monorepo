using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Chunking;
using Api.BoundedContexts.KnowledgeBase.Domain.Chunking;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.DocumentProcessing;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Services;
using Api.Services.Pdf;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// #4442 su Postgres vero, una volta per ciascuno storage. Il caricamento a pezzi elaborava il PDF con
/// una propria copia della pipeline, che apriva la chiave S3 come file locale: con lo storage S3 ogni
/// caricamento a pezzi finiva <c>Failed</c>. Ora il completamento salva l'oggetto, il record e il job in
/// coda (questi due nella stessa transazione), e la pipeline della coda legge l'oggetto dallo storage
/// fino a <c>Ready</c>.
/// </summary>
/// <remarks>
/// Gli stessi test girano su S3 (<see cref="CompleteChunkedUploadS3IntegrationTests"/>) e sullo storage
/// locale (<see cref="CompleteChunkedUploadLocalStorageIntegrationTests"/>): la correzione non deve
/// rompere lo storage su cui la copia rimossa funzionava. Una sottoclasse fornisce lo storage, la forma
/// attesa di <c>FilePath</c> e l'elenco degli oggetti salvati.
/// </remarks>
public abstract class CompleteChunkedUploadIntegrationTestsBase : IAsyncLifetime
{
    protected static readonly Guid TestUserId = new("A0000000-0000-0000-0000-000000004442");
    protected static readonly Guid TestSharedGameId = new("B0000000-0000-0000-0000-000000004442");
    private static readonly byte[] PdfBytes = BuildPdfBytes();

    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private ServiceProvider? _provider;
    private bool _failJobInsert;

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    protected CompleteChunkedUploadIntegrationTestsBase(SharedTestcontainersFixture fixture) => _fixture = fixture;

    /// <summary>Lo storage sotto test; <c>null</c> se non è partito (vedi <see cref="RequireStorage"/>).</summary>
    private protected IBlobStorageService? Storage { get; set; }

    /// <summary>Breve, entra nel nome del database di test (Postgres tronca oltre i 63 byte).</summary>
    protected abstract string StorageName { get; }

    protected abstract Task StartStorageAsync();

    protected abstract ValueTask DisposeStorageAsync();

    /// <summary>Fa fallire il test se lo storage non è partito: per un servizio L1 non si salta.</summary>
    protected abstract void RequireStorage();

    /// <summary>La forma che <c>FilePath</c> deve avere con questo storage.</summary>
    protected abstract void AssertFilePathShape(string filePath);

    /// <summary>Gli oggetti PDF salvati per il gioco di test.</summary>
    protected abstract Task<IReadOnlyList<string>> ListStoredPdfsAsync();

    public async ValueTask InitializeAsync()
    {
        await StartStorageAsync();

        _databaseName = $"test_chunked_{StorageName}_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);
        services.AddSingleton<IHttpContextAccessor>(new Mock<IHttpContextAccessor>().Object);
        services.AddScoped<IChunkedUploadSessionRepository, ChunkedUploadSessionRepository>();
        services.AddScoped<IPdfDocumentRepository, PdfDocumentRepository>();
        services.AddScoped<ProcessingJobRepository>();
        services.AddScoped<IProcessingJobRepository>(sp => _failJobInsert
            ? FailingJobInserts()
            : sp.GetRequiredService<ProcessingJobRepository>());
        services.AddSingleton<IBackgroundTaskService>(new NoOpBackgroundTaskService());
        var dedup = new Mock<IPdfDeduplicationService>();
        dedup.Setup(d => d.EvaluateAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PdfDedupResult(PdfDedupDecision.NewUpload, null, "h"));
        services.AddScoped(_ => dedup.Object);
        if (Storage is not null)
        {
            services.AddSingleton<IBlobStorageService>(Storage);
        }

        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await TestMigrationHelper.MigrateWithRetryAsync(db, Ct);
        db.Set<UserEntity>().Add(new UserEntity
        {
            Id = TestUserId,
            Email = "chunked-4442@meepleai.test",
            PasswordHash = "x",
            DisplayName = "Chunked upload #4442",
        });
        db.Set<SharedGameEntity>().Add(new SharedGameEntity { Id = TestSharedGameId, Title = "Chunked upload #4442 Game" });
        await db.SaveChangesAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_provider is not null)
        {
            await _provider.DisposeAsync();
        }

        if (!string.IsNullOrEmpty(_databaseName))
        {
            try
            {
                await _fixture.DropIsolatedDatabaseAsync(_databaseName);
            }
            catch
            {
                // Isolamento già ottenuto.
            }
        }

        await DisposeStorageAsync();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Complete_StoresTheObject_AndCommitsRecordAndJobTogether()
    {
        RequireStorage();

        var documentId = await CompleteAChunkedUploadAsync();

        using var scope = _provider!.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var pdf = await db.PdfDocuments.AsNoTracking().SingleAsync(p => p.Id == documentId, Ct);
        pdf.ProcessingState.Should().Be(nameof(PdfProcessingState.Pending));
        pdf.SharedGameId.Should().Be(TestSharedGameId);
        AssertFilePathShape(pdf.FilePath);

        var jobs = await db.ProcessingJobs.AsNoTracking().Where(j => j.PdfDocumentId == documentId).ToListAsync(Ct);
        jobs.Should().ContainSingle();
        jobs[0].Status.Should().Be(nameof(JobStatus.Queued));
        jobs[0].Priority.Should().Be(CompleteChunkedUploadCommandHandler.InteractiveUploadPriority);
        jobs[0].UserId.Should().Be(TestUserId);

        await using var stored = await Storage!.RetrieveAsync(documentId.ToString("N"), BlobCategory.Pdf, TestSharedGameId.ToString(), Ct);
        stored.Should().NotBeNull("l'oggetto è nello storage");
        (await ReadAllAsync(stored!)).Should().Equal(PdfBytes);
    }

    [Fact]
    public async Task Complete_WithAFullQueue_StillEnqueues()
    {
        RequireStorage();
        await FillTheQueueAsync();

        var documentId = await CompleteAChunkedUploadAsync();

        using var scope = _provider!.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        (await db.ProcessingJobs.CountAsync(j => j.PdfDocumentId == documentId && j.Status == nameof(JobStatus.Queued), Ct))
            .Should().Be(1, "un upload interattivo di un admin scavalca il tetto della coda (decisione 1a)");
    }

    [Fact]
    public async Task Complete_WhenTheJobCannotBeSaved_LeavesNoRecord_AndNoOrphanObject()
    {
        RequireStorage();
        _failJobInsert = true;

        var result = await SendCompleteAsync();

        result.Success.Should().BeFalse();
        using var scope = _provider!.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        (await db.PdfDocuments.CountAsync(Ct)).Should().Be(0, "record e job stanno nella stessa transazione");
        (await ListStoredPdfsAsync()).Should().BeEmpty("un oggetto senza record sarebbe orfano");
    }

    [Fact]
    public async Task QueuePipeline_ProcessesTheChunkedUpload_ReadingTheObjectFromStorage()
    {
        RequireStorage();
        var documentId = await CompleteAChunkedUploadAsync();

        using var scope = _provider!.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var pdf = await db.PdfDocuments.AsNoTracking().SingleAsync(p => p.Id == documentId, Ct);

        byte[]? extractedFrom = null;
        var extractor = new Mock<IPdfTextExtractor>();
        extractor.Setup(e => e.ExtractPagedTextAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns<Stream, bool, CancellationToken>(async (stream, _, _) =>
            {
                extractedFrom = await ReadAllAsync(stream);
                return PagedTextExtractionResult.CreateSuccess(
                    pageChunks: [new PageTextChunk(1, "Setup. Place the tiles.", 0, 23)],
                    totalPages: 1,
                    totalCharacters: 23,
                    ocrTriggered: false,
                    structuredElements: [new ExtractedElement("Setup", 1, "Title"), new ExtractedElement("Place the tiles.", 1, "NarrativeText")]);
            });
        var tables = new Mock<IPdfTableExtractor>();
        tables.Setup(t => t.ExtractStructuredContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(StructuredContentResult.CreateFailure("Not applicable"));
        var parent = HierarchicalChunk.CreateParent("Setup section content",
            new ChunkMetadata { Page = 1, Heading = "Setup", ElementType = "Title", DocumentId = documentId });
        var child = HierarchicalChunk.CreateChild("Place the tiles.", level: 2,
            new ChunkMetadata { Page = 1, Heading = "Setup", ElementType = "NarrativeText", DocumentId = documentId }, parent.Id);
        var chunking = new Mock<IAdvancedChunkingService>();
        chunking.Setup(s => s.ChunkDocumentAsync(It.IsAny<ExtractedDocument>(), It.IsAny<ChunkingConfiguration?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HierarchicalChunk> { parent, child });
        var embeddings = new Mock<IEmbeddingService>();
        embeddings.Setup(e => e.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<string> texts, CancellationToken _) => new EmbeddingResult
            {
                Success = true,
                Embeddings = texts.Select(_ => Enumerable.Repeat(0.01f, 768).ToArray()).ToList(),
            });
        embeddings.Setup(e => e.GetModelName()).Returns("test-embedding-model");
        var language = new Mock<ILanguageDetector>();
        language.Setup(l => l.Detect(It.IsAny<string>())).Returns(new LanguageDetectionResult("en", true, 0.95));
        var indexing = new Mock<IPdfIndexingPipeline>();

        var pipeline = new PdfProcessingPipelineService(
            db,
            new RelationalPdfClaimService(db),
            extractor.Object,
            tables.Object,
            Mock.Of<ITextChunkingService>(),
            embeddings.Object,
            Storage!,
            TimeProvider.System,
            NullLogger<PdfProcessingPipelineService>.Instance,
            language.Object,
            Mock.Of<IChunkTranslationService>(),
            indexing.Object,
            advancedChunking: chunking.Object);

        var outcome = await pipeline.ProcessAsync(documentId, pdf.FilePath, TestUserId, Ct);

        outcome.Should().Be(PdfPipelineOutcome.Processed);
        extractedFrom.Should().Equal(PdfBytes, "la pipeline della coda legge il file dallo storage");
        using var assertScope = _provider.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var processed = await assertDb.PdfDocuments.AsNoTracking().SingleAsync(p => p.Id == documentId, Ct);
        processed.ProcessingState.Should().Be(nameof(PdfProcessingState.Ready));
        (await assertDb.TextChunks.CountAsync(tc => tc.PdfDocumentId == documentId, Ct)).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task QueuePipeline_WhenTheObjectIsMissing_EndsFailed_WithStageAndCategory()
    {
        RequireStorage();
        var documentId = await CompleteAChunkedUploadAsync();
        await Storage!.DeleteAsync(documentId.ToString("N"), BlobCategory.Pdf, TestSharedGameId.ToString(), Ct);

        using var scope = _provider!.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var pdf = await db.PdfDocuments.AsNoTracking().SingleAsync(p => p.Id == documentId, Ct);
        var language = new Mock<ILanguageDetector>();
        language.Setup(l => l.Detect(It.IsAny<string>())).Returns(new LanguageDetectionResult("en", true, 0.95));
        var pipeline = new PdfProcessingPipelineService(
            db,
            new RelationalPdfClaimService(db),
            Mock.Of<IPdfTextExtractor>(),
            Mock.Of<IPdfTableExtractor>(),
            Mock.Of<ITextChunkingService>(),
            Mock.Of<IEmbeddingService>(),
            Storage,
            TimeProvider.System,
            NullLogger<PdfProcessingPipelineService>.Instance,
            language.Object,
            Mock.Of<IChunkTranslationService>(),
            Mock.Of<IPdfIndexingPipeline>());

        var outcome = await pipeline.ProcessAsync(documentId, pdf.FilePath, TestUserId, Ct);

        outcome.Should().Be(PdfPipelineOutcome.Failed);
        using var assertScope = _provider.CreateScope();
        var failed = await assertScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>()
            .PdfDocuments.AsNoTracking().SingleAsync(p => p.Id == documentId, Ct);
        failed.ProcessingState.Should().Be(nameof(PdfProcessingState.Failed));
        failed.FailedAtState.Should().Be(nameof(PdfProcessingState.Extracting), "la fase in cui il documento si è fermato");
        failed.ErrorCategory.Should().Be(nameof(ErrorCategory.StorageObjectMissing));
        // #4660: la chiave cercata sta nel log, non nella colonna che pdf-status serve agli utenti.
        failed.ProcessingError.Should().Be(PdfFailureText.For(ErrorCategory.StorageObjectMissing));
        failed.ProcessingError.Should().NotContain(documentId.ToString("N"));
    }

    protected static async Task<byte[]> ReadAllAsync(Stream stream)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }

    private async Task<Guid> CompleteAChunkedUploadAsync()
    {
        var result = await SendCompleteAsync();
        result.Success.Should().BeTrue(result.ErrorMessage);
        return result.DocumentId!.Value;
    }

    private async Task<CompleteChunkedUploadResult> SendCompleteAsync()
    {
        var sessionId = Guid.NewGuid();
        var tempDir = Path.Combine(Path.GetTempPath(), "meepleai_uploads", sessionId.ToString());
        Directory.CreateDirectory(tempDir);
        var session = new ChunkedUploadSession(sessionId, TestSharedGameId, TestUserId, "rules.pdf",
            totalFileSize: PdfBytes.Length, tempDirectory: tempDir);
        await File.WriteAllBytesAsync(session.GetChunkFilePath(0), PdfBytes, Ct);
        session.MarkChunkReceived(0);

        using (var seedScope = _provider!.CreateScope())
        {
            var sessions = seedScope.ServiceProvider.GetRequiredService<IChunkedUploadSessionRepository>();
            await sessions.AddAsync(session, Ct);
            await seedScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>().SaveChangesAsync(Ct);
        }

        using var scope = _provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>()
            .Send(new CompleteChunkedUploadCommand(sessionId, TestUserId), Ct);
    }

    private async Task FillTheQueueAsync()
    {
        using var scope = _provider!.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        for (var i = 0; i < ProcessingJob.MaxQueueSize; i++)
        {
            var pdfId = Guid.NewGuid();
            db.PdfDocuments.Add(new PdfDocumentEntity
            {
                Id = pdfId,
                SharedGameId = TestSharedGameId,
                FileName = $"filler-{i}.pdf",
                FilePath = $"pdfs/{TestSharedGameId}/{pdfId:N}_filler-{i}.pdf",
                FileSizeBytes = 1,
                ContentType = "application/pdf",
                UploadedByUserId = TestUserId,
                UploadedAt = DateTime.UtcNow,
            });
            db.ProcessingJobs.Add(new ProcessingJobEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = pdfId,
                UserId = TestUserId,
                Status = nameof(JobStatus.Queued),
                Priority = 0,
                CreatedAt = DateTimeOffset.UtcNow,
                MaxRetries = 3,
            });
        }

        await db.SaveChangesAsync(Ct);
    }

    private static byte[] BuildPdfBytes()
    {
        var body = new byte[32 * 1024];
        "%PDF-1.4\n"u8.CopyTo(body);
        for (var i = 9; i < body.Length; i++)
        {
            body[i] = (byte)('a' + (i % 26));
        }

        return body;
    }

    private sealed class NoOpBackgroundTaskService : IBackgroundTaskService
    {
        public void Execute(Func<Task> task)
        {
        }

        public void ExecuteWithCancellation(string taskId, Func<CancellationToken, Task> taskFactory)
        {
        }

        public bool CancelTask(string taskId) => false;
    }

    /// <summary>
    /// Un guasto del database sull'inserimento del job, e solo su quello: il resto dell'accodamento
    /// (nessun job attivo, coda vuota) risponde come il repository vero.
    /// </summary>
    private static IProcessingJobRepository FailingJobInserts()
    {
        var jobs = new Mock<IProcessingJobRepository>();
        jobs.Setup(r => r.ExistsByPdfDocumentIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        jobs.Setup(r => r.CountByStatusAsync(It.IsAny<JobStatus>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
        jobs.Setup(r => r.AddAsync(It.IsAny<ProcessingJob>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("simulated failure inserting the processing job"));
        return jobs.Object;
    }
}
