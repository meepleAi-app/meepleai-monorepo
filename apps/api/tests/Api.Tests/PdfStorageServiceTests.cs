using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

public class PdfStorageServiceTests
{
    private static MeepleAiDbContext CreateInMemoryContext()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new MeepleAiDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static PdfStorageService CreateService(
        MeepleAiDbContext dbContext,
        string storagePath,
        Mock<IBackgroundTaskService> backgroundTaskMock,
        Mock<IServiceScopeFactory>? scopeFactoryMock = null,
        Mock<IAiResponseCacheService>? cacheMock = null,
        PdfTextExtractionService? textExtractionService = null,
        PdfTableExtractionService? tableExtractionService = null,
        ITextChunkingService? textChunkingService = null,
        IEmbeddingService? embeddingService = null,
        IQdrantService? qdrantService = null)
    {
        var configurationMock = new Mock<IConfiguration>();
        configurationMock.Setup(c => c[It.Is<string>(key => key == "PDF_STORAGE_PATH")]).Returns(storagePath);

        scopeFactoryMock ??= new Mock<IServiceScopeFactory>(MockBehavior.Strict);
        cacheMock ??= new Mock<IAiResponseCacheService>();
        cacheMock
            .Setup(x => x.InvalidateGameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        cacheMock
            .Setup(x => x.InvalidateEndpointAsync(It.IsAny<string>(), It.IsAny<AiCacheEndpoint>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var loggerMock = new Mock<ILogger<PdfStorageService>>();
        return new PdfStorageService(
            dbContext,
            scopeFactoryMock.Object,
            configurationMock.Object,
            loggerMock.Object,
            textExtractionService ?? new PdfTextExtractionService(
                Mock.Of<ILogger<PdfTextExtractionService>>(),
                Mock.Of<IConfiguration>(),
                ocrService: null),
            tableExtractionService ?? new PdfTableExtractionService(Mock.Of<ILogger<PdfTableExtractionService>>()),
            backgroundTaskMock.Object,
            cacheMock.Object,
            textChunkingService,
            embeddingService,
            qdrantService);
    }

    private static async Task SeedUserAsync(MeepleAiDbContext dbContext, string userId)
    {
        dbContext.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"{userId}@example.com",
            PasswordHash = "hashed-password",
            Role = UserRole.User,
            CreatedAt = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task UploadPdfAsync_WithNullFile_ReturnsFailure()
    {
        await using var dbContext = CreateInMemoryContext();
        dbContext.Games.Add(new GameEntity { Id = "game-1", Name = "Game" });
        await dbContext.SaveChangesAsync();
        await SeedUserAsync(dbContext, "user");

        var storagePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var backgroundMock = new Mock<IBackgroundTaskService>();
            var service = CreateService(dbContext, storagePath, backgroundMock);

            var result = await service.UploadPdfAsync("game-1", "user", null!, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("No file provided", result.Message);
            backgroundMock.Verify(b => b.Execute(It.IsAny<Func<Task>>()), Times.Never);
        }
        finally
        {
            if (Directory.Exists(storagePath))
            {
                Directory.Delete(storagePath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task UploadPdfAsync_WithTooLargeFile_ReturnsFailure()
    {
        await using var dbContext = CreateInMemoryContext();
        dbContext.Games.Add(new GameEntity { Id = "game-1", Name = "Game" });
        await dbContext.SaveChangesAsync();
        await SeedUserAsync(dbContext, "user");

        var storagePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var backgroundMock = new Mock<IBackgroundTaskService>();
            var service = CreateService(dbContext, storagePath, backgroundMock);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(60L * 1024 * 1024);
            mockFile.Setup(f => f.ContentType).Returns("application/pdf");
            mockFile.Setup(f => f.FileName).Returns("large.pdf");

            var result = await service.UploadPdfAsync("game-1", "user", mockFile.Object, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("File size exceeds", result.Message);
            backgroundMock.Verify(b => b.Execute(It.IsAny<Func<Task>>()), Times.Never);
        }
        finally
        {
            if (Directory.Exists(storagePath))
            {
                Directory.Delete(storagePath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task UploadPdfAsync_WithInvalidContentType_ReturnsFailure()
    {
        await using var dbContext = CreateInMemoryContext();
        dbContext.Games.Add(new GameEntity { Id = "game-1", Name = "Game" });
        await dbContext.SaveChangesAsync();
        await SeedUserAsync(dbContext, "user");

        var storagePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var backgroundMock = new Mock<IBackgroundTaskService>();
            var service = CreateService(dbContext, storagePath, backgroundMock);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(1024);
            mockFile.Setup(f => f.ContentType).Returns("text/plain");
            mockFile.Setup(f => f.FileName).Returns("notes.txt");

            var result = await service.UploadPdfAsync("game-1", "user", mockFile.Object, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("Invalid file type", result.Message);
            backgroundMock.Verify(b => b.Execute(It.IsAny<Func<Task>>()), Times.Never);
        }
        finally
        {
            if (Directory.Exists(storagePath))
            {
                Directory.Delete(storagePath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task UploadPdfAsync_WhenGameMissing_ReturnsFailure()
    {
        await using var dbContext = CreateInMemoryContext();
        await SeedUserAsync(dbContext, "user");

        var storagePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var backgroundMock = new Mock<IBackgroundTaskService>();
            var service = CreateService(dbContext, storagePath, backgroundMock);

            var file = CreateFormFile("rules.pdf", "application/pdf", new byte[] { 1, 2, 3 });

            var result = await service.UploadPdfAsync("unknown", "user", file, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("Game not found", result.Message);
            backgroundMock.Verify(b => b.Execute(It.IsAny<Func<Task>>()), Times.Never);
        }
        finally
        {
            if (Directory.Exists(storagePath))
            {
                Directory.Delete(storagePath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task UploadPdfAsync_WithValidFile_SavesDocumentAndSchedulesExtraction()
    {
        await using var dbContext = CreateInMemoryContext();
        dbContext.Games.Add(new GameEntity { Id = "game-1", Name = "Game" });
        await dbContext.SaveChangesAsync();
        await SeedUserAsync(dbContext, "user");

        var storagePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Func<Task>? scheduledTask = null;
        try
        {
            var backgroundMock = new Mock<IBackgroundTaskService>();
            backgroundMock
                .Setup(b => b.Execute(It.IsAny<Func<Task>>()))
                .Callback<Func<Task>>(task => scheduledTask = task);

            var scopeFactoryMock = new Mock<IServiceScopeFactory>(MockBehavior.Strict);
            var cacheMock = new Mock<IAiResponseCacheService>();

            var service = CreateService(dbContext, storagePath, backgroundMock, scopeFactoryMock, cacheMock);

            var file = CreateFormFile("rules.pdf", "application/pdf", new byte[] { 1, 2, 3, 4 });

            var result = await service.UploadPdfAsync("game-1", "user", file, CancellationToken.None);

            Assert.True(result.Success);
            Assert.NotNull(result.Document);
            Assert.Equal("rules.pdf", result.Document!.FileName);

            var gameDirectory = Path.Combine(storagePath, "game-1");
            Assert.True(Directory.Exists(gameDirectory));
            Assert.Single(Directory.GetFiles(gameDirectory));

            var stored = await dbContext.PdfDocuments.FirstAsync();
            Assert.Equal("game-1", stored.GameId);
            Assert.Equal("application/pdf", stored.ContentType);
            Assert.Equal("user", stored.UploadedByUserId);

            Assert.NotNull(scheduledTask);

            cacheMock.Verify(x => x.InvalidateGameAsync("game-1", It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            if (Directory.Exists(storagePath))
            {
                Directory.Delete(storagePath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task IndexVectorsAsync_UsesOverridesWhenProvided()
    {
        await using var dbContext = CreateInMemoryContext();
        dbContext.Games.Add(new GameEntity { Id = "game-1", Name = "Game" });
        await dbContext.SaveChangesAsync();
        await SeedUserAsync(dbContext, "user");

        var storagePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var scheduledTasks = new List<Func<Task>>();

        try
        {
            var backgroundMock = new Mock<IBackgroundTaskService>();
            backgroundMock
                .Setup(b => b.Execute(It.IsAny<Func<Task>>()))
                .Callback<Func<Task>>(task => scheduledTasks.Add(task));

            var scopeFactoryMock = new Mock<IServiceScopeFactory>();
            var serviceProviderMock = new Mock<IServiceProvider>(MockBehavior.Strict);
            serviceProviderMock
                .Setup(sp => sp.GetService(typeof(MeepleAiDbContext)))
                .Returns(dbContext);
            serviceProviderMock
                .Setup(sp => sp.GetService(typeof(PdfTableExtractionService)))
                .Returns((object?)null);
            serviceProviderMock
                .Setup(sp => sp.GetService(typeof(ITextChunkingService)))
                .Throws(new InvalidOperationException("Scope chunking should not be used"));
            serviceProviderMock
                .Setup(sp => sp.GetService(typeof(IEmbeddingService)))
                .Throws(new InvalidOperationException("Scope embedding should not be used"));
            serviceProviderMock
                .Setup(sp => sp.GetService(typeof(IQdrantService)))
                .Throws(new InvalidOperationException("Scope Qdrant should not be used"));

            var scopeMock = new Mock<IServiceScope>();
            scopeMock.SetupGet(s => s.ServiceProvider).Returns(serviceProviderMock.Object);
            scopeMock.Setup(s => s.Dispose());
            scopeFactoryMock.Setup(s => s.CreateScope()).Returns(scopeMock.Object);

            var cacheMock = new Mock<IAiResponseCacheService>();

            var textExtractionMock = new Mock<PdfTextExtractionService>(
                MockBehavior.Strict,
                Mock.Of<ILogger<PdfTextExtractionService>>(),
                Mock.Of<IConfiguration>(),
                (IOcrService?)null);
            textExtractionMock
                .Setup(s => s.ExtractTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(PdfTextExtractionResult.CreateSuccess("chunk-one\nchunk-two", 1, 18));

            var tableExtractionMock = new Mock<PdfTableExtractionService>(
                MockBehavior.Strict,
                Mock.Of<ILogger<PdfTableExtractionService>>());
            tableExtractionMock
                .Setup(s => s.ExtractStructuredContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(PdfStructuredExtractionResult.CreateSuccess(
                    new List<PdfTable>(),
                    new List<PdfDiagram>(),
                    new List<string>()));

            var chunkingMock = new Mock<ITextChunkingService>(MockBehavior.Strict);
            var chunkInputs = new List<DocumentChunkInput>
            {
                new() { Text = "chunk-one", Page = 1, CharStart = 0, CharEnd = 8 },
                new() { Text = "chunk-two", Page = 1, CharStart = 9, CharEnd = 17 }
            };
            chunkingMock
                .Setup(c => c.PrepareForEmbedding(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns<string, int, int>((text, _, _) =>
                {
                    Assert.Equal("chunk-one\nchunk-two", text);
                    return chunkInputs;
                });

            var embeddingMock = new Mock<IEmbeddingService>(MockBehavior.Strict);
            var embeddings = new List<float[]>
            {
                new float[] { 0.1f, 0.2f },
                new float[] { 0.3f, 0.4f }
            };
            embeddingMock
                .Setup(e => e.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((List<string> texts, CancellationToken _) =>
                {
                    Assert.Equal(chunkInputs.Select(c => c.Text), texts);
                    return EmbeddingResult.CreateSuccess(embeddings);
                });

            var qdrantMock = new Mock<IQdrantService>(MockBehavior.Strict);
            qdrantMock
                .Setup(q => q.IndexDocumentChunksAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<List<DocumentChunk>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((string gameId, string pdfId, List<DocumentChunk> chunks, CancellationToken _) =>
                {
                    Assert.Equal("game-1", gameId);
                    Assert.Equal(chunkInputs.Count, chunks.Count);
                    for (var i = 0; i < chunks.Count; i++)
                    {
                        Assert.Equal(chunkInputs[i].Text, chunks[i].Text);
                        Assert.Equal(embeddings[i], chunks[i].Embedding);
                    }

                    return IndexResult.CreateSuccess(chunks.Count);
                });

            var service = CreateService(
                dbContext,
                storagePath,
                backgroundMock,
                scopeFactoryMock,
                cacheMock,
                textExtractionMock.Object,
                tableExtractionMock.Object,
                chunkingMock.Object,
                embeddingMock.Object,
                qdrantMock.Object);

            var file = CreateFormFile("rules.pdf", "application/pdf", new byte[] { 1, 2, 3, 4 });
            var uploadResult = await service.UploadPdfAsync("game-1", "user", file, CancellationToken.None);

            Assert.True(uploadResult.Success);
            Assert.Single(scheduledTasks);

            var extractionTask = scheduledTasks.Single();
            await extractionTask();

            Assert.Equal(2, scheduledTasks.Count);

            var indexingTask = scheduledTasks[1];
            await indexingTask();

            chunkingMock.Verify(
                c => c.PrepareForEmbedding(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()),
                Times.Once);
            embeddingMock.Verify(
                e => e.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()),
                Times.Once);
            qdrantMock.Verify(
                q => q.IndexDocumentChunksAsync(
                    "game-1",
                    It.IsAny<string>(),
                    It.IsAny<List<DocumentChunk>>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
        finally
        {
            if (Directory.Exists(storagePath))
            {
                Directory.Delete(storagePath, recursive: true);
            }
        }
    }

    private static IFormFile CreateFormFile(string fileName, string contentType, byte[] content)
    {
        var stream = new MemoryStream(content);
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.Length).Returns(content.Length);
        mockFile.Setup(f => f.FileName).Returns(fileName);
        mockFile.Setup(f => f.ContentType).Returns(contentType);
        mockFile
            .Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns<Stream, CancellationToken>((target, token) =>
            {
                stream.Position = 0;
                return stream.CopyToAsync(target, token);
            });
        return mockFile.Object;
    }
}
