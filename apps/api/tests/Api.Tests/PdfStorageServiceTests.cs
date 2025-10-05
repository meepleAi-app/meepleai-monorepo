using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
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
    private readonly ServiceProvider _scopeServiceProvider;
    private readonly FakePdfTextExtractionService _fakeTextExtractionService;
    private readonly FakePdfTableExtractionService _fakeTableExtractionService;
    private readonly FakeEmbeddingService _fakeEmbeddingService;
    private readonly FakeQdrantService _fakeQdrantService;

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

        _fakeTextExtractionService = new FakePdfTextExtractionService();
        _fakeTableExtractionService = new FakePdfTableExtractionService();
        _fakeEmbeddingService = new FakeEmbeddingService();
        _fakeQdrantService = new FakeQdrantService();

        var scopeServices = new ServiceCollection();
        scopeServices.AddScoped(_ => _dbContext);
        scopeServices.AddSingleton<ITextChunkingService>(
            new TextChunkingService(NullLogger<TextChunkingService>.Instance));
        scopeServices.AddSingleton<IEmbeddingService>(_fakeEmbeddingService);
        scopeServices.AddSingleton<IQdrantService>(_fakeQdrantService);
        scopeServices.AddSingleton<PdfTableExtractionService>(_fakeTableExtractionService);

        _scopeServiceProvider = scopeServices.BuildServiceProvider();

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock
            .Setup(factory => factory.CreateScope())
            .Returns(() => _scopeServiceProvider.CreateScope());

        _backgroundTaskMock = new Mock<IBackgroundTaskService>();
        _backgroundTaskMock
            .Setup(service => service.Execute(It.IsAny<Func<Task>>()))
            .Callback<Func<Task>>(task => _capturedTasks.Add(task));

        _service = new PdfStorageService(
            _dbContext,
            scopeFactoryMock.Object,
            configuration,
            NullLogger<PdfStorageService>.Instance,
            _fakeTextExtractionService,
            _fakeTableExtractionService,
            _backgroundTaskMock.Object);
    }

    public void Dispose()
    {
        _scopeServiceProvider.Dispose();
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
        Assert.Equal("pending", savedDocument.ProcessingStatus);

        Assert.Single(_capturedTasks);

        var processedIndex = 0;
        while (processedIndex < _capturedTasks.Count)
        {
            var task = _capturedTasks[processedIndex];
            processedIndex++;
            await task();
        }

        Assert.Equal(2, _capturedTasks.Count);
        _backgroundTaskMock.Verify(service => service.Execute(It.IsAny<Func<Task>>()), Times.Exactly(2));

        await _dbContext.Entry(savedDocument).ReloadAsync();
        Assert.Equal("completed", savedDocument.ProcessingStatus);
        Assert.Equal(_fakeTextExtractionService.ExtractedText, savedDocument.ExtractedText);
        Assert.Equal(_fakeTextExtractionService.PageCount, savedDocument.PageCount);
        Assert.Equal(_fakeTextExtractionService.CharacterCount, savedDocument.CharacterCount);
        Assert.NotNull(savedDocument.ProcessedAt);
        Assert.True(_fakeTextExtractionService.Called);

        Assert.True(_fakeTableExtractionService.Called);
        var expectedTablesJson = JsonSerializer.Serialize(_fakeTableExtractionService.Tables);
        var expectedDiagramsJson = JsonSerializer.Serialize(_fakeTableExtractionService.Diagrams.Select(d => new
        {
            d.PageNumber,
            d.DiagramType,
            d.Description,
            d.Width,
            d.Height
        }));
        var expectedAtomicRulesJson = JsonSerializer.Serialize(_fakeTableExtractionService.AtomicRules);

        Assert.Equal(expectedTablesJson, savedDocument.ExtractedTables);
        Assert.Equal(expectedDiagramsJson, savedDocument.ExtractedDiagrams);
        Assert.Equal(expectedAtomicRulesJson, savedDocument.AtomicRules);
        Assert.Equal(_fakeTableExtractionService.Tables.Count, savedDocument.TableCount);
        Assert.Equal(_fakeTableExtractionService.Diagrams.Count, savedDocument.DiagramCount);
        Assert.Equal(_fakeTableExtractionService.AtomicRules.Count, savedDocument.AtomicRuleCount);

        Assert.Single(_fakeEmbeddingService.RequestedTexts);
        Assert.All(_fakeEmbeddingService.RequestedTexts.Single(), text => Assert.False(string.IsNullOrWhiteSpace(text)));

        Assert.NotNull(_fakeQdrantService.LastChunks);
        Assert.Equal(_fakeEmbeddingService.RequestedTexts.Single().Count, _fakeQdrantService.LastChunks!.Count);
        Assert.Equal(game.Id, _fakeQdrantService.LastGameId);
        Assert.Equal(savedDocument.Id, _fakeQdrantService.LastPdfId);

        var vectorDoc = await _dbContext.VectorDocuments.SingleAsync();
        Assert.Equal(savedDocument.Id, vectorDoc.PdfDocumentId);
        Assert.Equal(game.Id, vectorDoc.GameId);
        Assert.Equal("completed", vectorDoc.IndexingStatus);
        Assert.Equal(_fakeQdrantService.LastChunks!.Count, vectorDoc.ChunkCount);
        Assert.NotNull(vectorDoc.IndexedAt);
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

    private class FakePdfTextExtractionService : PdfTextExtractionService
    {
        public FakePdfTextExtractionService()
            : base(NullLogger<PdfTextExtractionService>.Instance)
        {
        }

        public bool Called { get; private set; }
        public string ExtractedText { get; } = "Fake extracted text for testing.";
        public int PageCount { get; } = 1;
        public int CharacterCount => ExtractedText.Length;

        public override Task<PdfTextExtractionResult> ExtractTextAsync(string filePath, CancellationToken ct = default)
        {
            Called = true;
            return Task.FromResult(PdfTextExtractionResult.CreateSuccess(ExtractedText, PageCount, CharacterCount));
        }
    }

    private class FakePdfTableExtractionService : PdfTableExtractionService
    {
        public FakePdfTableExtractionService()
            : base(NullLogger<PdfTableExtractionService>.Instance)
        {
        }

        public bool Called { get; private set; }
        public List<PdfTable> Tables { get; } = new()
        {
            new PdfTable
            {
                PageNumber = 1,
                StartLine = 0,
                Headers = new List<string> { "Column A", "Column B" },
                Rows = new List<string[]>
                {
                    new[] { "A1", "B1" }
                },
                ColumnCount = 2,
                RowCount = 1
            }
        };

        public List<PdfDiagram> Diagrams { get; } = new()
        {
            new PdfDiagram
            {
                PageNumber = 1,
                DiagramType = "Flowchart",
                Description = "Sample diagram",
                Width = 100,
                Height = 50
            }
        };

        public List<string> AtomicRules { get; } = new() { "If A then B" };

        public override Task<PdfStructuredExtractionResult> ExtractStructuredContentAsync(
            string filePath,
            CancellationToken ct = default)
        {
            Called = true;
            return Task.FromResult(PdfStructuredExtractionResult.CreateSuccess(Tables, Diagrams, AtomicRules));
        }
    }

    private class FakeEmbeddingService : IEmbeddingService
    {
        public List<List<string>> RequestedTexts { get; } = new();

        public Task<EmbeddingResult> GenerateEmbeddingsAsync(List<string> texts, CancellationToken ct = default)
        {
            RequestedTexts.Add(new List<string>(texts));
            var embeddings = texts.Select(_ => new float[] { 0.1f, 0.2f }).ToList();
            return Task.FromResult(EmbeddingResult.CreateSuccess(embeddings));
        }

        public Task<EmbeddingResult> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
        {
            return GenerateEmbeddingsAsync(new List<string> { text }, ct);
        }
    }

    private class FakeQdrantService : IQdrantService
    {
        public string? LastGameId { get; private set; }
        public string? LastPdfId { get; private set; }
        public List<DocumentChunk>? LastChunks { get; private set; }

        public Task EnsureCollectionExistsAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task<IndexResult> IndexDocumentChunksAsync(
            string gameId,
            string pdfId,
            List<DocumentChunk> chunks,
            CancellationToken ct = default)
        {
            LastGameId = gameId;
            LastPdfId = pdfId;
            LastChunks = chunks;
            return Task.FromResult(IndexResult.CreateSuccess(chunks.Count));
        }

        public Task<SearchResult> SearchAsync(
            string gameId,
            float[] queryEmbedding,
            int limit = 5,
            CancellationToken ct = default)
        {
            return Task.FromResult(SearchResult.CreateSuccess(new List<SearchResultItem>()));
        }

        public Task<bool> DeleteDocumentAsync(string pdfId, CancellationToken ct = default) => Task.FromResult(true);
    }
}
