using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
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
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
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
/// #4442 su Postgres e MinIO veri. Il caricamento a pezzi elaborava il PDF con una propria copia della
/// pipeline, che apriva la chiave S3 come file locale: con lo storage S3 ogni caricamento a pezzi
/// finiva <c>Failed</c>. Ora il completamento salva l'oggetto, il record e il job in coda (questi due
/// nella stessa transazione), e la pipeline della coda legge l'oggetto dallo storage fino a
/// <c>Ready</c>.
/// </summary>
/// <remarks>
/// MinIO è un servizio L1: se il container non parte il test fallisce, non salta
/// (<see cref="L1Services.FailBecauseUnavailable"/>).
/// </remarks>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("Feature", "S3Storage")]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "4442")]
public sealed class CompleteChunkedUploadS3IntegrationTests : IAsyncLifetime
{
    private static readonly Guid TestUserId = new("A0000000-0000-0000-0000-000000004442");
    private static readonly Guid TestSharedGameId = new("B0000000-0000-0000-0000-000000004442");
    private static readonly byte[] PdfBytes = BuildPdfBytes();

    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private IContainer? _minio;
    private IAmazonS3? _s3Client;
    private S3BlobStorageService? _storage;
    private string? _minioFailure;
    private ServiceProvider? _provider;
    private bool _failJobInsert;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public CompleteChunkedUploadS3IntegrationTests(SharedTestcontainersFixture fixture) => _fixture = fixture;

    public async ValueTask InitializeAsync()
    {
        await StartMinioAsync();

        _databaseName = $"test_chunked_s3_{Guid.NewGuid():N}";
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
        if (_storage is not null)
        {
            services.AddSingleton<IBlobStorageService>(_storage);
        }

        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await TestMigrationHelper.MigrateWithRetryAsync(db, Ct);
        db.Set<UserEntity>().Add(new UserEntity
        {
            Id = TestUserId,
            Email = "chunked-s3-4442@meepleai.test",
            PasswordHash = "x",
            DisplayName = "Chunked S3 #4442",
        });
        db.Set<SharedGameEntity>().Add(new SharedGameEntity { Id = TestSharedGameId, Title = "Chunked S3 #4442 Game" });
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

        _s3Client?.Dispose();
        if (_minio is not null)
        {
            await _minio.DisposeAsync();
        }
    }

    [Fact]
    public async Task Complete_OnS3_StoresTheObject_AndCommitsRecordAndJobTogether()
    {
        RequireMinio();

        var documentId = await CompleteAChunkedUploadAsync();

        using var scope = _provider!.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var pdf = await db.PdfDocuments.AsNoTracking().SingleAsync(p => p.Id == documentId, Ct);
        pdf.ProcessingState.Should().Be(nameof(PdfProcessingState.Pending));
        pdf.SharedGameId.Should().Be(TestSharedGameId);
        Path.IsPathRooted(pdf.FilePath).Should().BeFalse("con S3 il FilePath è la chiave dell'oggetto, non un percorso su disco");

        var jobs = await db.ProcessingJobs.AsNoTracking().Where(j => j.PdfDocumentId == documentId).ToListAsync(Ct);
        jobs.Should().ContainSingle();
        jobs[0].Status.Should().Be(nameof(JobStatus.Queued));
        jobs[0].Priority.Should().Be(CompleteChunkedUploadCommandHandler.InteractiveUploadPriority);
        jobs[0].UserId.Should().Be(TestUserId);

        await using var stored = await _storage!.RetrieveAsync(documentId.ToString("N"), BlobCategory.Pdf, TestSharedGameId.ToString(), Ct);
        stored.Should().NotBeNull("l'oggetto è nello storage S3");
        (await ReadAllAsync(stored!)).Should().Equal(PdfBytes);
    }

    [Fact]
    public async Task Complete_WithAFullQueue_StillEnqueues()
    {
        RequireMinio();
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
        RequireMinio();
        _failJobInsert = true;

        var result = await SendCompleteAsync();

        result.Success.Should().BeFalse();
        using var scope = _provider!.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        (await db.PdfDocuments.CountAsync(Ct)).Should().Be(0, "record e job stanno nella stessa transazione");
        var objects = await _s3Client!.ListObjectsV2Async(new ListObjectsV2Request
        {
            BucketName = TestcontainersConfiguration.MinioTestBucket,
            Prefix = $"pdfs/{TestSharedGameId}/",
        }, Ct);
        (objects.S3Objects ?? []).Should().BeEmpty("un oggetto senza record sarebbe orfano");
    }

    [Fact]
    public async Task QueuePipeline_ProcessesTheChunkedUpload_ReadingTheObjectFromS3()
    {
        RequireMinio();
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
            _storage!,
            TimeProvider.System,
            NullLogger<PdfProcessingPipelineService>.Instance,
            language.Object,
            Mock.Of<IChunkTranslationService>(),
            indexing.Object,
            advancedChunking: chunking.Object);

        var outcome = await pipeline.ProcessAsync(documentId, pdf.FilePath, TestUserId, Ct);

        outcome.Should().Be(PdfPipelineOutcome.Processed);
        extractedFrom.Should().Equal(PdfBytes, "la pipeline della coda legge il file dallo storage S3");
        using var assertScope = _provider.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var processed = await assertDb.PdfDocuments.AsNoTracking().SingleAsync(p => p.Id == documentId, Ct);
        processed.ProcessingState.Should().Be(nameof(PdfProcessingState.Ready));
        (await assertDb.TextChunks.CountAsync(tc => tc.PdfDocumentId == documentId, Ct)).Should().BeGreaterThan(0);
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

    private async Task StartMinioAsync()
    {
        try
        {
            var endpoint = Environment.GetEnvironmentVariable("TEST_S3_ENDPOINT");
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                _minio = new ContainerBuilder()
                    .WithImage(TestcontainersConfiguration.MinioImage)
                    .WithCreateParameterModifier(p => p.User = TestcontainersConfiguration.MinioContainerUser)
                    .WithPortBinding(TestcontainersConfiguration.MinioApiPort, true)
                    .WithEnvironment("MINIO_ROOT_USER", TestcontainersConfiguration.MinioRootUser)
                    .WithEnvironment("MINIO_ROOT_PASSWORD", TestcontainersConfiguration.MinioRootPassword)
                    .WithCommand("server", "/data")
                    .WithWaitStrategy(Wait.ForUnixContainer()
                        .UntilHttpRequestIsSucceeded(r => r
                            .ForPath("/minio/health/live")
                            .ForPort(TestcontainersConfiguration.MinioApiPort)
                            .ForStatusCode(System.Net.HttpStatusCode.OK)))
                    .WithCleanUp(true)
                    .Build();
                await _minio.StartAsync(Ct);
                endpoint = $"http://localhost:{_minio.GetMappedPublicPort(TestcontainersConfiguration.MinioApiPort)}";
            }

            var accessKey = Environment.GetEnvironmentVariable("TEST_S3_ACCESS_KEY") ?? TestcontainersConfiguration.MinioRootUser;
            var secretKey = Environment.GetEnvironmentVariable("TEST_S3_SECRET_KEY") ?? TestcontainersConfiguration.MinioRootPassword;
            _s3Client = new AmazonS3Client(new BasicAWSCredentials(accessKey, secretKey), new AmazonS3Config
            {
                ServiceURL = endpoint,
                ForcePathStyle = true,
                AuthenticationRegion = "us-east-1",
            });
            try
            {
                await _s3Client.PutBucketAsync(new PutBucketRequest { BucketName = TestcontainersConfiguration.MinioTestBucket }, Ct);
            }
            catch (AmazonS3Exception ex) when (ex.ErrorCode is "BucketAlreadyOwnedByYou" or "BucketAlreadyExists")
            {
                // Già presente.
            }

            _storage = new S3BlobStorageService(_s3Client, new S3StorageOptions
            {
                Endpoint = endpoint,
                AccessKey = accessKey,
                SecretKey = secretKey,
                BucketName = TestcontainersConfiguration.MinioTestBucket,
                Region = "us-east-1",
                PresignedUrlExpirySeconds = 3600,
                EnableEncryption = false,
                ForcePathStyle = true,
            }, NullLogger<S3BlobStorageService>.Instance);
        }
        catch (Exception ex)
        {
            _minioFailure = ex.Message;
        }
    }

    private void RequireMinio()
    {
        if (_storage is null)
        {
            L1Services.FailBecauseUnavailable("minio", $"il container MinIO non e partito oppure TEST_S3_ENDPOINT non risponde: {_minioFailure}");
        }
    }

    private static async Task<byte[]> ReadAllAsync(Stream stream)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
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
