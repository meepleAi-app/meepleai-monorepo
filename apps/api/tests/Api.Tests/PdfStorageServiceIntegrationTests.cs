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
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests;

public class PdfStorageServiceIntegrationTests : PostgresIntegrationTestBase
{
    private readonly ITestOutputHelper _output;

    [Fact]
    public async Task IndexVectorsAsync_UsesScopedServicesWhenOverridesAreNull()
    {
        var storagePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(storagePath);

        try
        {
            // Arrange database entities
            DbContext.Games.Add(new GameEntity { Id = "game-1", Name = "Test Game" });
            DbContext.Users.Add(new UserEntity
            {
                Id = "user-1",
                Email = "user1@example.com",
                PasswordHash = "hash",
                Role = UserRole.User,
                CreatedAt = DateTime.UtcNow
            });
            await DbContext.SaveChangesAsync();

            var scopeFactory = new CapturingScopeFactory(CreateScopedDbContext);
            var backgroundService = new TestBackgroundTaskService();
            var textExtractionService = new TestPdfTextExtractionService();
            var tableExtractionService = new TestPdfTableExtractionService();
            var cacheService = new Mock<IAiResponseCacheService>().Object;

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["PDF_STORAGE_PATH"] = storagePath
                })
                .Build();

            var service = new PdfStorageService(
                DbContext,
                scopeFactory,
                NullLogger<PdfStorageService>.Instance,
                textExtractionService,
                tableExtractionService,
                backgroundService,
                cacheService,
                new TestBlobStorageService(storagePath));

            var file = CreateFormFile("rules.pdf", "application/pdf", new byte[] { 0x25, 0x50, 0x44, 0x46 });

            // Act - upload triggers background extraction
            var uploadResult = await service.UploadPdfAsync("game-1", "user-1", file, CancellationToken.None);

            uploadResult.Success.Should().BeTrue();
            backgroundService.PendingTasks.Should().Be(1);

            // Execute extraction task
            await backgroundService.ExecuteNextAsync();

            backgroundService.ExecutedTasksCount.Should().Be(1);
            backgroundService.PendingTasks.Should().Be(0);

            // Assert chunking, embedding and qdrant services resolved from scope were used
            var chunkingService = Assert.Single(scopeFactory.ChunkingServices.Where(c => c.PrepareForEmbeddingCallCount > 0 && c.ChunkTextCallCount > 0));
            chunkingService.LastText.Should().Be("chunk-one\nchunk-two");
            chunkingService.LastChunkText.Should().Be("chunk-one\nchunk-two");
            chunkingService.PrepareForEmbeddingCallCount.Should().Be(1);
            chunkingService.ChunkTextCallCount.Should().Be(1);

            var embeddingService = Assert.Single(scopeFactory.EmbeddingServices.Where(e => e.GenerateEmbeddingsCallCount > 0));
            "chunk-two" }, embeddingService.LastRequestedTexts.Should().Be(new[] { "chunk-one");

            var qdrantService = Assert.Single(scopeFactory.QdrantServices.Where(q => q.IndexCallCount > 0));
            qdrantService.LastGameId.Should().Be("game-1");
            qdrantService.LastPdfId.Should().Be(uploadResult.Document!.Id);
            qdrantService.LastChunks!.Count.Should().Be(2);
            Assert.All(qdrantService.LastChunks!, chunk => Assert.Equal(2, chunk.Embedding.Length));

            await using var verificationContext = CreateScopedDbContext();
            var vectorDoc = await verificationContext.VectorDocuments.SingleAsync();
            vectorDoc.IndexingStatus.Should().Be("completed");
            vectorDoc.ChunkCount.Should().Be(2);
            vectorDoc.TotalCharacters.Should().Be("chunk-one\nchunk-two".Length);
        }
        finally
        {
            if (Directory.Exists(storagePath))
            {
                Directory.Delete(storagePath, recursive: true);
            }
        }
    }

    private sealed class TestBlobStorageService : IBlobStorageService
    {
        private readonly string _root;

        public TestBlobStorageService(string root)
        {
            _root = root;
        }

        public Task<BlobStorageResult> StoreAsync(Stream stream, string fileName, string gameId, CancellationToken ct = default)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            var extension = Path.GetExtension(fileName);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".pdf";
            }

            var fileId = Guid.NewGuid().ToString("N");
            var gameDirectory = Path.Combine(_root, gameId);
            Directory.CreateDirectory(gameDirectory);

            var destinationPath = Path.Combine(gameDirectory, $"{fileId}{extension}");
            using (var destination = File.Create(destinationPath))
            {
                stream.CopyTo(destination);
            }

            var info = new FileInfo(destinationPath);
            return Task.FromResult(new BlobStorageResult(true, fileId, destinationPath, info.Length));
        }

        public Task<Stream?> RetrieveAsync(string fileId, string gameId, CancellationToken ct = default)
        {
            var path = ResolvePath(fileId, gameId);
            if (path == null || !File.Exists(path))
            {
                return Task.FromResult<Stream?>(null);
            }

            Stream stream = File.OpenRead(path);
            return Task.FromResult<Stream?>(stream);
        }

        public Task<bool> DeleteAsync(string fileId, string gameId, CancellationToken ct = default)
        {
            var path = ResolvePath(fileId, gameId);
            if (path == null || !File.Exists(path))
            {
                return Task.FromResult(false);
            }

            File.Delete(path);
            return Task.FromResult(true);
        }

        public string GetStoragePath(string fileId, string gameId, string fileName)
        {
            var extension = Path.GetExtension(fileName);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".pdf";
            }

            return Path.Combine(_root, gameId, $"{fileId}{extension}");
        }

        public bool Exists(string fileId, string gameId)
        {
            var directory = Path.Combine(_root, gameId);
            if (!Directory.Exists(directory))
            {
                return false;
            }

            return Directory.EnumerateFiles(directory, $"{fileId}*").Any();
        }

        private string? ResolvePath(string fileId, string gameId)
        {
            var directory = Path.Combine(_root, gameId);
            if (!Directory.Exists(directory))
            {
                return null;
            }

            return Directory.EnumerateFiles(directory, $"{fileId}*").FirstOrDefault();
        }
    }

    private static IFormFile CreateFormFile(string fileName, string contentType, byte[] content)
    {
        var stream = new MemoryStream(content);
        var formFile = new FormFile(stream, 0, content.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };

        return formFile;
    }

    private sealed class TestBackgroundTaskService : IBackgroundTaskService
    {
        public int ExecutedTasksCount { get; private set; }
        private readonly Queue<Func<Task>> _tasks = new();

        public int PendingTasks => _tasks.Count;

        public void Execute(Func<Task> task)
        {
            _tasks.Enqueue(task);
        }

        public void ExecuteWithCancellation(string taskId, Func<CancellationToken, Task> taskFactory)
        {
            // For testing, we'll just execute the task without cancellation support
            _tasks.Enqueue(() => taskFactory(CancellationToken.None));
        }

        public bool CancelTask(string taskId)
        {
            // For testing, we don't actually track cancellation
            return false;
        }

        public async Task ExecuteNextAsync()
        {
            if (_tasks.Count == 0)
            {
                throw new InvalidOperationException("No background tasks scheduled");
            }

            var task = _tasks.Dequeue();
            await task();
            ExecutedTasksCount++;
        }
    }

    private sealed class TestPdfTextExtractionService : PdfTextExtractionService
    {
        private static readonly string Extracted = "chunk-one\nchunk-two";

        public TestPdfTextExtractionService()
            : base(
                NullLogger<PdfTextExtractionService>.Instance,
                new ConfigurationBuilder().Build(),
                ocrService: null)
        {
        }

        public override Task<PdfTextExtractionResult> ExtractTextAsync(string filePath, CancellationToken ct = default)
        {
            return Task.FromResult(PdfTextExtractionResult.CreateSuccess(Extracted, 1, Extracted.Length));
        }

        public override Task<PagedExtractionResult> ExtractPagedTextAsync(string filePath, CancellationToken ct = default)
        {
            var chunks = new List<PagedTextChunk>
            {
                new(
                    Text: Extracted,
                    PageNumber: 1,
                    CharStartIndex: 0,
                    CharEndIndex: Extracted.Length - 1)
            };

            return Task.FromResult(PagedExtractionResult.CreateSuccess(chunks, 1));
        }
    }

    private sealed class TestPdfTableExtractionService : PdfTableExtractionService
    {
        public TestPdfTableExtractionService()
            : base(
                Mock.Of<ITableDetectionService>(),
                Mock.Of<ITableStructureAnalyzer>(),
                NullLogger<PdfTableExtractionService>.Instance)
        {
        }

        public override Task<PdfStructuredExtractionResult> ExtractStructuredContentAsync(string filePath, CancellationToken ct = default)
        {
            return Task.FromResult(PdfStructuredExtractionResult.CreateSuccess(new List<PdfTable>(), new List<PdfDiagram>(), new List<string>()));
        }
    }

    private sealed class TestChunkingService : ITextChunkingService
    {
        public int PrepareForEmbeddingCallCount { get; private set; }
        public string? LastText { get; private set; }
        public int ChunkTextCallCount { get; private set; }
        public string? LastChunkText { get; private set; }

        private static readonly List<DocumentChunkInput> Chunks = new()
        {
            new() { Text = "chunk-one", Page = 1, CharStart = 0, CharEnd = 9 },
            new() { Text = "chunk-two", Page = 1, CharStart = 10, CharEnd = 19 }
        };

        public List<TextChunk> ChunkText(string text, int chunkSize = 512, int overlap = 50)
        {
            ChunkTextCallCount++;
            LastChunkText = text;

            var index = 0;
            return Chunks.Select(c => new TextChunk
            {
                Text = c.Text,
                CharStart = c.CharStart,
                CharEnd = c.CharEnd,
                Page = c.Page,
                Index = index++
            }).ToList();
        }

        public List<DocumentChunkInput> PrepareForEmbedding(string text, int chunkSize = 512, int overlap = 50)
        {
            PrepareForEmbeddingCallCount++;
            LastText = text;
            return new List<DocumentChunkInput>();
        }
    }

    private sealed class TestEmbeddingService : IEmbeddingService
    {
        public int GenerateEmbeddingsCallCount { get; private set; }
        public IReadOnlyList<string>? LastRequestedTexts { get; private set; }

        private static readonly List<float[]> Embeddings = new()
        {
            new float[] { 0.1f, 0.2f },
            new float[] { 0.3f, 0.4f }
        };

        public int GetEmbeddingDimensions() => 2; // Matches test embedding dimensions

        public string GetModelName() => "openai/text-embedding-3-small";

        public Task<EmbeddingResult> GenerateEmbeddingsAsync(List<string> texts, CancellationToken ct = default)
        {
            GenerateEmbeddingsCallCount++;
            LastRequestedTexts = texts;
            return Task.FromResult(EmbeddingResult.CreateSuccess(Embeddings));
        }

        public Task<EmbeddingResult> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
        {
            return GenerateEmbeddingsAsync(new List<string> { text }, ct);
        }

        // AI-09: Language-aware overloads
        public Task<EmbeddingResult> GenerateEmbeddingsAsync(List<string> texts, string language, CancellationToken ct = default)
        {
            return GenerateEmbeddingsAsync(texts, ct);
        }

        public Task<EmbeddingResult> GenerateEmbeddingAsync(string text, string language, CancellationToken ct = default)
        {
            return GenerateEmbeddingAsync(text, ct);
        }
    }

    private sealed class TestQdrantService : IQdrantService
    {
        public int IndexCallCount { get; private set; }
        public string? LastGameId { get; private set; }
        public string? LastPdfId { get; private set; }
        public List<DocumentChunk>? LastChunks { get; private set; }

        public Task EnsureCollectionExistsAsync(CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }

        public Task<bool> CollectionExistsAsync(CancellationToken ct = default)
        {
            return Task.FromResult(true);
        }

        public Task<IndexResult> IndexDocumentChunksAsync(string gameId, string pdfId, List<DocumentChunk> chunks, CancellationToken ct = default)
        {
            IndexCallCount++;
            LastGameId = gameId;
            LastPdfId = pdfId;
            LastChunks = chunks.Select(chunk => new DocumentChunk
            {
                Text = chunk.Text,
                Embedding = chunk.Embedding.ToArray(),
                Page = chunk.Page,
                CharStart = chunk.CharStart,
                CharEnd = chunk.CharEnd
            }).ToList();

            return Task.FromResult(IndexResult.CreateSuccess(chunks.Count));
        }

        public Task<IndexResult> IndexChunksWithMetadataAsync(Dictionary<string, string> metadata, List<DocumentChunk> chunks, CancellationToken ct = default)
        {
            return Task.FromResult(IndexResult.CreateSuccess(chunks.Count));
        }

        public Task<SearchResult> SearchAsync(string gameId, float[] queryEmbedding, int limit = 5, CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        // AI-09: Language-aware overloads
        public Task<IndexResult> IndexDocumentChunksAsync(string gameId, string pdfId, List<DocumentChunk> chunks, string language, CancellationToken ct = default)
        {
            return IndexDocumentChunksAsync(gameId, pdfId, chunks, ct);
        }

        public Task<SearchResult> SearchAsync(string gameId, float[] queryEmbedding, string language, int limit = 5, CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task<SearchResult> SearchByCategoryAsync(string category, float[] queryEmbedding, int limit = 5, CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> DeleteDocumentAsync(string pdfId, CancellationToken ct = default)
        {
            return Task.FromResult(true);
        }

        public Task<bool> DeleteByCategoryAsync(string category, CancellationToken ct = default)
        {
            return Task.FromResult(true);
        }
    }

    private sealed class CapturingScopeFactory : IServiceScopeFactory
    {
        private readonly Func<MeepleAiDbContext> _dbContextFactory;

        public List<TestChunkingService> ChunkingServices { get; } = new();
        public List<TestEmbeddingService> EmbeddingServices { get; } = new();
        public List<TestQdrantService> QdrantServices { get; } = new();

        public CapturingScopeFactory(Func<MeepleAiDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        public IServiceScope CreateScope()
        {
            var dbContext = _dbContextFactory();
            var chunking = new TestChunkingService();
            var embedding = new TestEmbeddingService();
            var qdrant = new TestQdrantService();

            ChunkingServices.Add(chunking);
            EmbeddingServices.Add(embedding);
            QdrantServices.Add(qdrant);

            return new CapturingScope(dbContext, chunking, embedding, qdrant);
        }

        private sealed class CapturingScope : IServiceScope
        {
            private readonly MeepleAiDbContext _dbContext;

            public IServiceProvider ServiceProvider { get; }

            public CapturingScope(
                MeepleAiDbContext dbContext,
                TestChunkingService chunkingService,
                TestEmbeddingService embeddingService,
                TestQdrantService qdrantService)
            {
                _dbContext = dbContext;
                ServiceProvider = new CapturingServiceProvider(dbContext, chunkingService, embeddingService, qdrantService);
            }

            public void Dispose()
            {
                _dbContext.Dispose();
            }
        }

        private sealed class CapturingServiceProvider : IServiceProvider
        {
            private readonly MeepleAiDbContext _dbContext;
            private readonly TestChunkingService _chunkingService;
            private readonly TestEmbeddingService _embeddingService;
            private readonly TestQdrantService _qdrantService;

            public CapturingServiceProvider(
                MeepleAiDbContext dbContext,
                TestChunkingService chunkingService,
                TestEmbeddingService embeddingService,
                TestQdrantService qdrantService)
            {
                _dbContext = dbContext;
                _chunkingService = chunkingService;
                _embeddingService = embeddingService;
                _qdrantService = qdrantService;
            }

            public object? GetService(Type serviceType)
            {
                if (serviceType == typeof(MeepleAiDbContext))
                {
                    return _dbContext;
                }

                if (serviceType == typeof(ITextChunkingService))
                {
                    return _chunkingService;
                }

                if (serviceType == typeof(IEmbeddingService))
                {
                    return _embeddingService;
                }

                if (serviceType == typeof(IQdrantService))
                {
                    return _qdrantService;
                }

                return null;
            }
        }
    }
}






