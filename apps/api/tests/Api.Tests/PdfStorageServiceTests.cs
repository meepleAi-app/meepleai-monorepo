using System;
using System.IO;
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
        Mock<IServiceScopeFactory>? scopeFactoryMock = null)
    {
        var configurationMock = new Mock<IConfiguration>();
        configurationMock.Setup(c => c[It.Is<string>(key => key == "PDF_STORAGE_PATH")]).Returns(storagePath);

        scopeFactoryMock ??= new Mock<IServiceScopeFactory>(MockBehavior.Strict);

        var loggerMock = new Mock<ILogger<PdfStorageService>>();
        var textExtractionService = new PdfTextExtractionService(Mock.Of<ILogger<PdfTextExtractionService>>());
        var tableExtractionService = new PdfTableExtractionService(Mock.Of<ILogger<PdfTableExtractionService>>());

        return new PdfStorageService(
            dbContext,
            scopeFactoryMock.Object,
            configurationMock.Object,
            loggerMock.Object,
            textExtractionService,
            tableExtractionService,
            backgroundTaskMock.Object);
    }

    [Fact]
    public async Task UploadPdfAsync_WithNullFile_ReturnsFailure()
    {
        await using var dbContext = CreateInMemoryContext();
        dbContext.Games.Add(new GameEntity { Id = "game-1", Name = "Game" });
        await dbContext.SaveChangesAsync();

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

        var storagePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Func<Task>? scheduledTask = null;
        try
        {
            var backgroundMock = new Mock<IBackgroundTaskService>();
            backgroundMock
                .Setup(b => b.Execute(It.IsAny<Func<Task>>()))
                .Callback<Func<Task>>(task => scheduledTask = task);

            var scopeFactoryMock = new Mock<IServiceScopeFactory>(MockBehavior.Strict);

            var service = CreateService(dbContext, storagePath, backgroundMock, scopeFactoryMock);

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
