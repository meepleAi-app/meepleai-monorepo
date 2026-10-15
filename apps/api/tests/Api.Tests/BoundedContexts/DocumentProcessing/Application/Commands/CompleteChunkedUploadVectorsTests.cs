using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.Services.Pdf;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// #4395: il caricamento a pezzi (<c>/ingest/pdf/chunked/complete</c>) calcolava gli embedding e non li
/// salvava, e non legava il PDF al gioco del catalogo. Il recupero trova i documenti di un gioco del
/// catalogo per <c>pdf_documents.shared_game_id</c> e il braccio vettoriale legge
/// <c>pgvector_embeddings</c>: un PDF caricato così era invisibile a entrambi.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "4395")]
public class CompleteChunkedUploadVectorsTests
{
    [Fact]
    public async Task TriggerPdfProcessing_SavesAPgvectorRowPerChunk_LinkedToItsChunk()
    {
        // Arrange
        var dbName = $"chunked_vectors_{Guid.NewGuid():N}";
        var pdfId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var vectorDocumentId = Guid.NewGuid();

        await using (var seedDb = CreateNoTrackingInMemoryDbContext(dbName))
        {
            seedDb.PdfDocuments.Add(new PdfDocumentEntity
            {
                Id = pdfId,
                SharedGameId = gameId,
                UploadedByUserId = Guid.NewGuid(),
                FileName = "rules.pdf",
                FilePath = "/test/rules.pdf",
                FileSizeBytes = 1024,
                ContentType = "application/pdf",
                UploadedAt = DateTime.UtcNow,
                ProcessingState = nameof(PdfProcessingState.Extracting),
                Language = "en",
            });
            // Il documento vettoriale lo crea IPdfIndexingPipeline, che qui è un doppio senza effetti.
            seedDb.VectorDocuments.Add(new VectorDocumentEntity
            {
                Id = vectorDocumentId,
                GameId = gameId,
                SharedGameId = gameId,
                PdfDocumentId = pdfId,
            });
            await seedDb.SaveChangesAsync();
        }

        var handler = CreateHandlerForProcessing(dbName, chunkTexts: ["Setup the board.", "Each player draws five cards.", "The game ends."]);
        var tmp = Path.GetTempFileName();
        await File.WriteAllTextAsync(tmp, "dummy pdf bytes");

        // Act
        try
        {
            await handler.TriggerPdfProcessingAsync(pdfId.ToString(), tmp, CancellationToken.None);
        }
        finally
        {
            File.Delete(tmp);
        }

        // Assert
        await using var assertDb = CreateNoTrackingInMemoryDbContext(dbName);
        var pdf = await assertDb.PdfDocuments.SingleAsync(p => p.Id == pdfId);
        pdf.ProcessingState.Should().Be(nameof(PdfProcessingState.Ready), $"ProcessingError='{pdf.ProcessingError}'");

        var chunksByIndex = await assertDb.TextChunks.Where(tc => tc.PdfDocumentId == pdfId).ToDictionaryAsync(tc => tc.ChunkIndex);
        chunksByIndex.Should().HaveCount(3);
        chunksByIndex.Values.Should().OnlyContain(tc => tc.SharedGameId == gameId,
            "il braccio lessicale filtra i passaggi di un gioco del catalogo per SharedGameId");

        var rows = await assertDb.PgVectorEmbeddings.Where(e => e.VectorDocumentId == vectorDocumentId).ToListAsync();
        rows.Should().HaveCount(3, "una riga vettoriale per ogni passaggio: prima gli embedding si calcolavano e si buttavano");
        foreach (var row in rows)
        {
            var chunk = chunksByIndex[row.ChunkIndex];
            row.SourceChunkId.Should().Be(chunk.Id);
            row.RoleTags.Should().Be((int)chunk.RoleTags).And.NotBe(0);
            row.TextContent.Should().Be(chunk.Content);
            row.GameId.Should().Be(gameId);
        }
    }

    [Fact]
    public async Task TriggerPdfProcessing_WithoutAModelName_EndsFailed_NotStuckInFlight()
    {
        // Arrange: no embedding model name. Before, the rows were saved with a NULL required column; the
        // failed rows stayed in the change tracker, the failure handler's own save failed on them, and the
        // PDF stayed in Extracting forever, with no error. Now the inputs are checked before any write, and
        // the failure handler starts from a clean change tracker in any case.
        var dbName = $"chunked_vectors_fail_{Guid.NewGuid():N}";
        var pdfId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        await using (var seedDb = CreateNoTrackingInMemoryDbContext(dbName))
        {
            seedDb.PdfDocuments.Add(new PdfDocumentEntity
            {
                Id = pdfId,
                SharedGameId = gameId,
                UploadedByUserId = Guid.NewGuid(),
                FileName = "rules.pdf",
                FilePath = "/test/rules.pdf",
                FileSizeBytes = 1024,
                ContentType = "application/pdf",
                UploadedAt = DateTime.UtcNow,
                ProcessingState = nameof(PdfProcessingState.Extracting),
            });
            seedDb.VectorDocuments.Add(new VectorDocumentEntity { PdfDocumentId = pdfId, GameId = gameId, SharedGameId = gameId });
            await seedDb.SaveChangesAsync();
        }

        var handler = CreateHandlerForProcessing(dbName, chunkTexts: ["Setup the board.", "The game ends."], modelName: null);
        var tmp = Path.GetTempFileName();
        await File.WriteAllTextAsync(tmp, "dummy pdf bytes");

        // Act
        try
        {
            await handler.TriggerPdfProcessingAsync(pdfId.ToString(), tmp, CancellationToken.None);
        }
        finally
        {
            File.Delete(tmp);
        }

        // Assert
        await using var assertDb = CreateNoTrackingInMemoryDbContext(dbName);
        var pdf = await assertDb.PdfDocuments.SingleAsync(p => p.Id == pdfId);
        pdf.ProcessingState.Should().Be(nameof(PdfProcessingState.Failed));
        pdf.ProcessingError.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task TriggerPdfProcessing_WhenSavingTheVectorRowsFails_EndsFailed_NotStuckInFlight()
    {
        // Arrange: the vector rows' save fails in the database. The failed rows stay in the change
        // tracker; before, the failure handler's own save submitted them again, failed again, and the PDF
        // stayed in Extracting forever with no error.
        var dbName = $"chunked_vectors_dbfail_{Guid.NewGuid():N}";
        var pdfId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        await using (var seedDb = CreateNoTrackingInMemoryDbContext(dbName))
        {
            seedDb.PdfDocuments.Add(new PdfDocumentEntity
            {
                Id = pdfId,
                SharedGameId = gameId,
                UploadedByUserId = Guid.NewGuid(),
                FileName = "rules.pdf",
                FilePath = "/test/rules.pdf",
                FileSizeBytes = 1024,
                ContentType = "application/pdf",
                UploadedAt = DateTime.UtcNow,
                ProcessingState = nameof(PdfProcessingState.Extracting),
            });
            seedDb.VectorDocuments.Add(new VectorDocumentEntity { PdfDocumentId = pdfId, GameId = gameId, SharedGameId = gameId });
            await seedDb.SaveChangesAsync();
        }

        var handler = CreateHandlerForProcessing(dbName, chunkTexts: ["Setup the board.", "The game ends."], failVectorSave: true);
        var tmp = Path.GetTempFileName();
        await File.WriteAllTextAsync(tmp, "dummy pdf bytes");

        // Act
        try
        {
            await handler.TriggerPdfProcessingAsync(pdfId.ToString(), tmp, CancellationToken.None);
        }
        finally
        {
            File.Delete(tmp);
        }

        // Assert
        await using var assertDb = CreateNoTrackingInMemoryDbContext(dbName);
        var pdf = await assertDb.PdfDocuments.SingleAsync(p => p.Id == pdfId);
        pdf.ProcessingState.Should().Be(nameof(PdfProcessingState.Failed));
        pdf.ProcessingError.Should().Contain("vector rows");
    }

    [Fact]
    public async Task TriggerPdfProcessing_WithAnInvalidVector_EndsFailed_WithoutRewritingAnything()
    {
        // Arrange: /ingest/pdf rejects NaN/Infinity vectors before saving; the chunked path saved none
        // at all, and once it started saving them it had no such check. The inputs are now checked
        // before the first write.
        var dbName = $"chunked_vectors_nan_{Guid.NewGuid():N}";
        var pdfId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        await using (var seedDb = CreateNoTrackingInMemoryDbContext(dbName))
        {
            seedDb.PdfDocuments.Add(new PdfDocumentEntity
            {
                Id = pdfId,
                SharedGameId = gameId,
                UploadedByUserId = Guid.NewGuid(),
                FileName = "rules.pdf",
                FilePath = "/test/rules.pdf",
                FileSizeBytes = 1024,
                ContentType = "application/pdf",
                UploadedAt = DateTime.UtcNow,
                ProcessingState = nameof(PdfProcessingState.Extracting),
            });
            seedDb.VectorDocuments.Add(new VectorDocumentEntity { PdfDocumentId = pdfId, GameId = gameId, SharedGameId = gameId });
            await seedDb.SaveChangesAsync();
        }

        var handler = CreateHandlerForProcessing(dbName, chunkTexts: ["Setup the board."], vector: [0.1f, float.NaN, 0.3f]);
        var tmp = Path.GetTempFileName();
        await File.WriteAllTextAsync(tmp, "dummy pdf bytes");

        // Act
        try
        {
            await handler.TriggerPdfProcessingAsync(pdfId.ToString(), tmp, CancellationToken.None);
        }
        finally
        {
            File.Delete(tmp);
        }

        // Assert
        await using var assertDb = CreateNoTrackingInMemoryDbContext(dbName);
        var pdf = await assertDb.PdfDocuments.SingleAsync(p => p.Id == pdfId);
        pdf.ProcessingState.Should().Be(nameof(PdfProcessingState.Failed));
        pdf.ProcessingError.Should().Contain("NaN");
        (await assertDb.PgVectorEmbeddings.CountAsync()).Should().Be(0);
        (await assertDb.TextChunks.CountAsync(tc => tc.PdfDocumentId == pdfId)).Should().Be(0, "the inputs are checked before any write");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Complete_CreatesTheRecordLinkedToItsGame(bool sharedGame)
    {
        // Arrange
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var tempDir = Path.Combine(Path.GetTempPath(), "meepleai_uploads", sessionId.ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var session = sharedGame
                ? new ChunkedUploadSession(sessionId, gameId, userId, "rules.pdf", totalFileSize: 14, tempDirectory: tempDir)
                : new ChunkedUploadSession(sessionId, null, userId, "rules.pdf", totalFileSize: 14, tempDirectory: tempDir, privateGameId: gameId);
            await File.WriteAllTextAsync(session.GetChunkFilePath(0), "dummy pdf data");
            session.MarkChunkReceived(0);

            var sessions = new Mock<IChunkedUploadSessionRepository>();
            sessions.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);

            var blobs = new Mock<IBlobStorageService>();
            blobs.Setup(b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new BlobStorageResult(true, Guid.NewGuid().ToString(), "/tmp/rules.pdf", 14));

            var dedup = new Mock<IPdfDeduplicationService>();
            dedup.Setup(d => d.EvaluateAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PdfDedupResult(PdfDedupDecision.NewUpload, null, "h"));

            var sc = new ServiceCollection();
            sc.AddSingleton(db);
            var handler = new CompleteChunkedUploadCommandHandler(
                sessions.Object,
                db,
                blobs.Object,
                Mock.Of<IBackgroundTaskService>(),
                NullLogger<CompleteChunkedUploadCommandHandler>.Instance,
                sc.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
                Mock.Of<IPdfTextExtractor>(),
                Mock.Of<IPdfTableExtractor>(),
                Mock.Of<IMediator>(),
                dedup.Object,
                TimeProvider.System);

            // Act
            var result = await handler.Handle(new CompleteChunkedUploadCommand(sessionId, userId), CancellationToken.None);

            // Assert
            result.Success.Should().BeTrue(result.ErrorMessage);
            var pdf = await db.PdfDocuments.SingleAsync();
            if (sharedGame)
            {
                pdf.SharedGameId.Should().Be(gameId, "il recupero trova i documenti di un gioco del catalogo per SharedGameId");
                pdf.PrivateGameId.Should().BeNull();
            }
            else
            {
                pdf.PrivateGameId.Should().Be(gameId);
                pdf.SharedGameId.Should().BeNull();
            }
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    private static CompleteChunkedUploadCommandHandler CreateHandlerForProcessing(
        string dbName, string[] chunkTexts, string? modelName = "test-embedding-model", float[]? vector = null,
        bool failVectorSave = false)
    {
        var extractor = new Mock<IPdfTextExtractor>();
        extractor
            .Setup(e => e.ExtractPagedTextAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PagedTextExtractionResult.CreateSuccess(
                [new PageTextChunk(1, string.Join("\n\n", chunkTexts), 0, 60)],
                totalPages: 1,
                totalCharacters: 60,
                ocrTriggered: false));

        var tables = new Mock<IPdfTableExtractor>();
        tables
            .Setup(t => t.ExtractStructuredContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(StructuredContentResult.CreateFailure("no structured content in test"));

        // Il chunker piatto: un passaggio per testo, in ordine.
        var chunking = new Mock<ITextChunkingService>();
        chunking
            .Setup(c => c.PrepareForEmbedding(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(chunkTexts.Select(t => new DocumentChunkInput { Text = t, Page = 1 }).ToList());

        var embeddings = new Mock<IEmbeddingService>();
        embeddings
            .Setup(e => e.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<string> texts, CancellationToken _) =>
                EmbeddingResult.CreateSuccess(texts.Select(_ => vector ?? new[] { 0.1f, 0.2f, 0.3f }).ToList()));
        embeddings.Setup(e => e.GetModelName()).Returns(modelName!);

        var roles = new Mock<IRoleClassifierService>();
        roles
            .Setup(c => c.ClassifyAsync(It.IsAny<IReadOnlyList<ChunkInput>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ChunkInput> inputs, CancellationToken _) =>
                inputs.Select((_, i) => i % 2 == 0 ? GameBookRole.Setup : GameBookRole.RulesReference).ToList());

        var sc = new ServiceCollection();
        sc.AddScoped<MeepleAiDbContext>(_ => CreateNoTrackingInMemoryDbContext(dbName, failVectorSave ? new FailVectorRowsInterceptor() : null));
        sc.AddScoped(_ => chunking.Object);
        sc.AddScoped(_ => embeddings.Object);
        sc.AddScoped(_ => roles.Object);
        sc.AddScoped(_ => Mock.Of<IPdfIndexingPipeline>());
        var scopeFactory = sc.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        return new CompleteChunkedUploadCommandHandler(
            Mock.Of<IChunkedUploadSessionRepository>(),
            CreateNoTrackingInMemoryDbContext(dbName),
            Mock.Of<IBlobStorageService>(),
            Mock.Of<IBackgroundTaskService>(),
            NullLogger<CompleteChunkedUploadCommandHandler>.Instance,
            scopeFactory,
            extractor.Object,
            tables.Object,
            Mock.Of<IMediator>(),
            Mock.Of<IPdfDeduplicationService>(),
            TimeProvider.System);
    }

    /// <summary>Un guasto del database sul salvataggio delle righe vettoriali, e solo su quello.</summary>
    private sealed class FailVectorRowsInterceptor : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
    {
        public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int>> SavingChangesAsync(
            Microsoft.EntityFrameworkCore.Diagnostics.DbContextEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<Api.Infrastructure.Entities.KnowledgeBase.PgVectorEmbeddingEntity>()
                .Any(e => e.State == EntityState.Added))
            {
                throw new DbUpdateException("simulated database failure on pgvector_embeddings");
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    /// <summary>Come in produzione: un contesto con <c>NoTracking</c> per default (PERF-06).</summary>
    private static MeepleAiDbContext CreateNoTrackingInMemoryDbContext(
        string dbName, Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<MeepleAiDbContext>();
        if (interceptor is not null)
        {
            builder.AddInterceptors(interceptor);
        }

        var options = builder
            .UseInMemoryDatabase(databaseName: dbName)
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .ConfigureWarnings(warnings =>
            {
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning);
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning);
            })
            .Options;

        return new MeepleAiDbContext(options, Mock.Of<IMediator>(), TestDbContextFactory.CreateMockEventCollector().Object);
    }
}
