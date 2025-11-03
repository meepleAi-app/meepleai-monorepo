using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.Services.Pdf;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

public class PdfStorageServiceTests
{
    private readonly ITestOutputHelper _output;

    private static MeepleAiDbContext CreateInMemoryContext()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"pdf-storage-tests-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={dbPath};Mode=ReadWriteCreate;Cache=Shared";

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"pdf-storage-tests-{Guid.NewGuid():N}")
            .Options;
        return new MeepleAiDbContext(options);
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
        scopeFactoryMock ??= new Mock<IServiceScopeFactory>(MockBehavior.Strict);
        cacheMock ??= new Mock<IAiResponseCacheService>();
        cacheMock
            .Setup(x => x.InvalidateGameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        cacheMock
            .Setup(x => x.InvalidateEndpointAsync(It.IsAny<string>(), It.IsAny<AiCacheEndpoint>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var loggerMock = new Mock<ILogger<PdfStorageService>>();
        var blobStorageMock = new Mock<IBlobStorageService>();
        blobStorageMock
            .Setup(b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream stream, string fileName, string gameId, CancellationToken _) =>
            {
                if (stream == null)
                {
                    throw new InvalidOperationException("Test blob storage received null stream");
                }

                if (!stream.CanRead)
                {
                    throw new InvalidOperationException("Test blob storage received unreadable stream");
                }

                if (stream.CanSeek)
                {
                    stream.Position = 0;
                }

                var extension = Path.GetExtension(fileName);
                if (string.IsNullOrEmpty(extension))
                {
                    extension = ".pdf";
                }

                var fileId = Guid.NewGuid().ToString("N");
                var gameDirectory = Path.Combine(storagePath, gameId);
                Directory.CreateDirectory(gameDirectory);
                var destinationPath = Path.Combine(gameDirectory, $"{fileId}{extension}");

                using (var destination = File.Create(destinationPath))
                {
                    stream.CopyTo(destination);
                }

                var fileInfo = new FileInfo(destinationPath);
                return new BlobStorageResult(
                    Success: true,
                    FileId: fileId,
                    FilePath: destinationPath,
                    FileSizeBytes: fileInfo.Length);
            });
        blobStorageMock
            .Setup(b => b.GetStoragePath(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string fileId, string gameId, string originalName) =>
            {
                var extension = Path.GetExtension(originalName);
                if (string.IsNullOrEmpty(extension))
                {
                    extension = ".pdf";
                }
                return Path.Combine(storagePath, gameId, $"{fileId}{extension}");
            });
        blobStorageMock
            .Setup(b => b.Exists(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((fileId, gameId) =>
            {
                var directory = Path.Combine(storagePath, gameId);
                return Directory.Exists(directory) &&
                    Directory.EnumerateFiles(directory, $"{fileId}*").Any();
            });

        return new PdfStorageService(
            dbContext,
            scopeFactoryMock.Object,
            loggerMock.Object,
            textExtractionService ?? new PdfTextExtractionService(
                Mock.Of<ILogger<PdfTextExtractionService>>(),
                Mock.Of<IConfiguration>(),
                ocrService: null),
            tableExtractionService ?? new PdfTableExtractionService(
                Mock.Of<ITableDetectionService>(),
                Mock.Of<ITableStructureAnalyzer>(),
                Mock.Of<ILogger<PdfTableExtractionService>>()),
            backgroundTaskMock.Object,
            cacheMock.Object,
            blobStorageMock.Object,
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

            result.Success.Should().BeFalse();
            result.Message.Should().Contain("No file provided");
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

            result.Success.Should().BeFalse();
            result.Message.Should().Contain("File is too large");
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

            result.Success.Should().BeFalse();
            result.Message.Should().Contain("Invalid file type");
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

            result.Success.Should().BeFalse();
            result.Message.Should().Contain("Game not found");
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
            backgroundMock
                .Setup(b => b.ExecuteWithCancellation(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task>>()))
                .Callback<string, Func<CancellationToken, Task>>((_, factory) =>
                {
                    scheduledTask = () => factory(CancellationToken.None);
                });

            var scopeFactoryMock = new Mock<IServiceScopeFactory>(MockBehavior.Strict);
            var cacheMock = new Mock<IAiResponseCacheService>();

            var service = CreateService(dbContext, storagePath, backgroundMock, scopeFactoryMock, cacheMock);

            var file = CreateFormFile("rules.pdf", "application/pdf", new byte[] { 1, 2, 3, 4 });

            var result = await service.UploadPdfAsync("game-1", "user", file, CancellationToken.None);

            result.Success.Should().BeTrue();
            result.Document.Should().NotBeNull();
            result.Document!.FileName.Should().Be("rules.pdf");

            var gameDirectory = Path.Combine(storagePath, "game-1");
            Directory.Exists(gameDirectory).Should().BeTrue();
            Directory.GetFiles(gameDirectory).Should().ContainSingle();

            var stored = await dbContext.PdfDocuments.FirstAsync();
            stored.GameId.Should().Be("game-1");
            stored.ContentType.Should().Be("application/pdf");
            stored.UploadedByUserId.Should().Be("user");

            scheduledTask.Should().NotBeNull();

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
            backgroundMock
                .Setup(b => b.ExecuteWithCancellation(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task>>()))
                .Callback<string, Func<CancellationToken, Task>>((_, factory) =>
                {
                    scheduledTasks.Add(() => factory(CancellationToken.None));
                });

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
                null!);
            textExtractionMock
                .Setup(s => s.ExtractPagedTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => PagedExtractionResult.CreateSuccess(
                    new List<PagedTextChunk>
                    {
                        new PagedTextChunk("chunk-one", 1, 0, 8),
                        new PagedTextChunk("chunk-two", 1, 9, 17)
                    },
                    pageCount: 1));

            var tableExtractionMock = new Mock<PdfTableExtractionService>(
                MockBehavior.Strict,
                Mock.Of<ITableDetectionService>(),
                Mock.Of<ITableStructureAnalyzer>(),
                Mock.Of<ILogger<PdfTableExtractionService>>());
            tableExtractionMock
                .Setup(s => s.ExtractStructuredContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(PdfStructuredExtractionResult.CreateSuccess(
                    new List<PdfTable>(),
                    new List<PdfDiagram>(),
                    new List<string>()));

        var chunkInputs = new List<DocumentChunkInput>
        {
            new() { Text = "chunk-one", Page = 1, CharStart = 0, CharEnd = 8 },
            new() { Text = "chunk-two", Page = 1, CharStart = 9, CharEnd = 17 }
        };
        var chunkingMock = new Mock<ITextChunkingService>(MockBehavior.Strict);
        // Setup PrepareForEmbedding to return empty, forcing service to use ChunkText fallback
        chunkingMock
            .Setup(c => c.PrepareForEmbedding(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(new List<DocumentChunkInput>());
        chunkingMock
            .Setup(c => c.ChunkText(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns<string, int, int>((text, _, _) =>
            {
                var match = chunkInputs.Single(chunk => chunk.Text == text);
                return new List<TextChunk>
                {
                    new()
                    {
                        Text = match.Text,
                        Page = match.Page,
                        CharStart = match.CharStart,
                        CharEnd = match.CharEnd,
                        Index = chunkInputs.IndexOf(match)
                    }
                };
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
                    texts.Should().BeEquivalentTo(chunkInputs.Select(c => c.Text));
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
                    gameId.Should().Be("game-1");
                    chunks.Count.Should().Be(chunkInputs.Count);
                    for (var i = 0; i < chunks.Count; i++)
                    {
                        chunks[i].Text.Should().Be(chunkInputs[i].Text);
                        chunks[i].Embedding.Should().BeEquivalentTo(embeddings[i]);
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

            uploadResult.Success.Should().BeTrue();
            scheduledTasks.Should().ContainSingle();

            var extractionTask = scheduledTasks.Single();
            await extractionTask();

            chunkingMock.Verify(
                c => c.ChunkText(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()),
                Times.AtLeastOnce);

            var processedDoc = await dbContext.PdfDocuments.SingleAsync();
            processedDoc.ProcessingStatus.Should().Be("completed");
            processedDoc.ProcessedAt.Should().NotBeNull();
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
            .Setup(f => f.OpenReadStream())
            .Returns(() =>
            {
                return new MemoryStream(content, writable: false);
            });
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