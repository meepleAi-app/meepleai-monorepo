using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests;

public class PdfStorageServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly MeepleAiDbContext _dbContext;
    private readonly PdfStorageService _service;
    private readonly Mock<IBackgroundTaskService> _backgroundTaskMock;
    private readonly List<Func<Task>> _capturedTasks = new();
    private readonly string _storagePath;

    public PdfStorageServiceTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new MeepleAiDbContext(options);
        _dbContext.Database.EnsureCreated();

        _storagePath = Path.Combine(Path.GetTempPath(), $"pdf-storage-tests-{Guid.NewGuid():N}");

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PDF_STORAGE_PATH"] = _storagePath
            })
            .Build();

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock
            .Setup(factory => factory.CreateScope())
            .Returns(Mock.Of<IServiceScope>());

        _backgroundTaskMock = new Mock<IBackgroundTaskService>();
        _backgroundTaskMock
            .Setup(service => service.Execute(It.IsAny<Func<Task>>()))
            .Callback<Func<Task>>(task => _capturedTasks.Add(task));

        var textExtraction = new PdfTextExtractionService(NullLogger<PdfTextExtractionService>.Instance);
        var tableExtraction = new PdfTableExtractionService(NullLogger<PdfTableExtractionService>.Instance);

        _service = new PdfStorageService(
            _dbContext,
            scopeFactoryMock.Object,
            configuration,
            NullLogger<PdfStorageService>.Instance,
            textExtraction,
            tableExtraction,
            _backgroundTaskMock.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();

        if (Directory.Exists(_storagePath))
        {
            Directory.Delete(_storagePath, recursive: true);
        }
    }

    [Fact]
    public async Task UploadPdfAsync_WhenFileIsNull_ReturnsFailure()
    {
        var result = await _service.UploadPdfAsync("game", "user", null!);

        Assert.False(result.Success);
        Assert.Equal("No file provided", result.Message);
        Assert.Null(result.Document);
    }

    [Fact]
    public async Task UploadPdfAsync_WhenFileIsEmpty_ReturnsFailure()
    {
        using var stream = new MemoryStream(Array.Empty<byte>());
        var formFile = CreateFormFile(stream, 0, "empty.pdf", "application/pdf");

        var result = await _service.UploadPdfAsync("game", "user", formFile);

        Assert.False(result.Success);
        Assert.Equal("No file provided", result.Message);
        Assert.Null(result.Document);
    }

    [Fact]
    public async Task UploadPdfAsync_WhenFileIsTooLarge_ReturnsFailure()
    {
        using var stream = new MemoryStream(new byte[1]);
        var formFile = CreateFormFile(stream, 51 * 1024 * 1024, "large.pdf", "application/pdf");

        var result = await _service.UploadPdfAsync("game", "user", formFile);

        Assert.False(result.Success);
        Assert.Contains("File size exceeds maximum", result.Message);
        Assert.Null(result.Document);
    }

    [Fact]
    public async Task UploadPdfAsync_WhenContentTypeIsInvalid_ReturnsFailure()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("not a pdf"));
        var formFile = CreateFormFile(stream, stream.Length, "invalid.txt", "text/plain");

        var result = await _service.UploadPdfAsync("game", "user", formFile);

        Assert.False(result.Success);
        Assert.Equal("Invalid file type. Only PDF files are allowed.", result.Message);
        Assert.Null(result.Document);
    }

    [Fact]
    public async Task UploadPdfAsync_WhenFileNameIsInvalid_ReturnsFailure()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("pdf"));
        var formFile = CreateFormFile(stream, stream.Length, " ", "application/pdf");

        var result = await _service.UploadPdfAsync("game", "user", formFile);

        Assert.False(result.Success);
        Assert.Equal("Invalid file name", result.Message);
        Assert.Null(result.Document);
    }

    [Fact]
    public async Task UploadPdfAsync_WhenGameDoesNotExist_ReturnsFailure()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("pdf"));
        var formFile = CreateFormFile(stream, stream.Length, "rules.pdf", "application/pdf");

        var result = await _service.UploadPdfAsync("missing-game", "user", formFile);

        Assert.False(result.Success);
        Assert.Equal("Game not found or access denied", result.Message);
        Assert.Null(result.Document);
    }

    [Fact]
    public async Task UploadPdfAsync_WhenUploadSucceeds_PersistsDocumentAndSchedulesProcessing()
    {
        var game = new GameEntity
        {
            Id = "game-123",
            Name = "Test Game",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        var pdfBytes = Encoding.UTF8.GetBytes("%PDF-1.4 test content");
        using var stream = new MemoryStream(pdfBytes);
        var formFile = CreateFormFile(stream, stream.Length, "Test<Rules>.pdf", "application/pdf");

        var result = await _service.UploadPdfAsync(game.Id, "user-1", formFile);

        Assert.True(result.Success);
        Assert.Equal("PDF uploaded successfully", result.Message);
        Assert.NotNull(result.Document);
        Assert.Equal("user-1", result.Document!.UploadedByUserId);
        Assert.Equal(formFile.Length, result.Document.FileSizeBytes);

        var savedDocument = await _dbContext.PdfDocuments.SingleAsync();
        Assert.Equal(game.Id, savedDocument.GameId);
        Assert.Equal("user-1", savedDocument.UploadedByUserId);
        Assert.StartsWith(savedDocument.Id, Path.GetFileNameWithoutExtension(savedDocument.FilePath));
        Assert.Equal("Test_Rules_.pdf", savedDocument.FileName);
        Assert.True(File.Exists(savedDocument.FilePath));

        Assert.Single(_capturedTasks);
        _backgroundTaskMock.Verify(service => service.Execute(It.IsAny<Func<Task>>()), Times.Once);
    }

    [Fact]
    public async Task GetPdfsByGameAsync_ReturnsDocumentsOrderedByUploadTime()
    {
        var game = new GameEntity
        {
            Id = "game-order",
            Name = "Ordered Game",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Games.Add(game);

        var older = new PdfDocumentEntity
        {
            Id = "doc-older",
            GameId = game.Id,
            FileName = "older.pdf",
            FilePath = Path.Combine(_storagePath, "older.pdf"),
            FileSizeBytes = 10,
            ContentType = "application/pdf",
            UploadedByUserId = "user-a",
            UploadedAt = DateTime.UtcNow.AddMinutes(-10)
        };

        var middle = new PdfDocumentEntity
        {
            Id = "doc-middle",
            GameId = game.Id,
            FileName = "middle.pdf",
            FilePath = Path.Combine(_storagePath, "middle.pdf"),
            FileSizeBytes = 20,
            ContentType = "application/pdf",
            UploadedByUserId = "user-b",
            UploadedAt = DateTime.UtcNow.AddMinutes(-5)
        };

        var newest = new PdfDocumentEntity
        {
            Id = "doc-newest",
            GameId = game.Id,
            FileName = "newest.pdf",
            FilePath = Path.Combine(_storagePath, "newest.pdf"),
            FileSizeBytes = 30,
            ContentType = "application/pdf",
            UploadedByUserId = "user-c",
            UploadedAt = DateTime.UtcNow
        };

        _dbContext.PdfDocuments.AddRange(older, middle, newest);
        await _dbContext.SaveChangesAsync();

        var results = await _service.GetPdfsByGameAsync(game.Id);

        Assert.Collection(results,
            first =>
            {
                Assert.Equal("doc-newest", first.Id);
                Assert.Equal("newest.pdf", first.FileName);
                Assert.Equal(30, first.FileSizeBytes);
                Assert.Equal("user-c", first.UploadedByUserId);
            },
            second =>
            {
                Assert.Equal("doc-middle", second.Id);
                Assert.Equal("middle.pdf", second.FileName);
                Assert.Equal(20, second.FileSizeBytes);
                Assert.Equal("user-b", second.UploadedByUserId);
            },
            third =>
            {
                Assert.Equal("doc-older", third.Id);
                Assert.Equal("older.pdf", third.FileName);
                Assert.Equal(10, third.FileSizeBytes);
                Assert.Equal("user-a", third.UploadedByUserId);
            });
    }

    private static FormFile CreateFormFile(Stream stream, long length, string fileName, string contentType)
    {
        var formFile = new FormFile(stream, 0, length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };

        return formFile;
    }
}
