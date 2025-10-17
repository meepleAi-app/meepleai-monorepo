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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests;

public class PdfStorageServiceIntegrationTests : PostgresIntegrationTestBase
{
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
                configuration,
                NullLogger<PdfStorageService>.Instance,
                textExtractionService,
                tableExtractionService,
                backgroundService,
                cacheService);

            var file = CreateFormFile("rules.pdf", "application/pdf", new byte[] { 0x25, 0x50, 0x44, 0x46 });

            // Act - upload triggers background extraction
            var uploadResult = await service.UploadPdfAsync("game-1", "user-1", file, CancellationToken.None);

            Assert.True(uploadResult.Success);
            Assert.Equal(1, backgroundService.PendingTasks);

            // Execute extraction task
            await backgroundService.ExecuteNextAsync();

            // Indexing should have been scheduled by extraction
            Assert.Equal(1, backgroundService.PendingTasks);

            // Execute indexing task
            await backgroundService.ExecuteNextAsync();

            Assert.Equal(0, backgroundService.PendingTasks);

            // Assert chunking, embedding and qdrant services resolved from scope were used
            var chunkingService = Assert.Single(scopeFactory.ChunkingServices.Where(c => c.PrepareForEmbeddingCallCount > 0));
            Assert.Equal("chunk-one\nchunk-two", chunkingService.LastText);

            var embeddingService = Assert.Single(scopeFactory.EmbeddingServices.Where(e => e.GenerateEmbeddingsCallCount > 0));
            Assert.Equal(new[] { "chunk-one", "chunk-two" }, embeddingService.LastRequestedTexts);

            var qdrantService = Assert.Single(scopeFactory.QdrantServices.Where(q => q.IndexCallCount > 0));
            Assert.Equal("game-1", qdrantService.LastGameId);
            Assert.Equal(uploadResult.Document!.Id, qdrantService.LastPdfId);
            Assert.Equal(2, qdrantService.LastChunks!.Count);
            Assert.All(qdrantService.LastChunks!, chunk => Assert.Equal(2, chunk.Embedding.Length));

            await using var verificationContext = CreateScopedDbContext();
            var vectorDoc = await verificationContext.VectorDocuments.SingleAsync();
            Assert.Equal("completed", vectorDoc.IndexingStatus);
            Assert.Equal(2, vectorDoc.ChunkCount);
            Assert.Equal("chunk-one\nchunk-two".Length, vectorDoc.TotalCharacters);
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
        var formFile = new FormFile(stream, 0, content.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };

        return formFile;
    }

    private sealed class TestBackgroundTaskService : IBackgroundTaskService
    {
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
    }

    private sealed class TestPdfTableExtractionService : PdfTableExtractionService
    {
        public TestPdfTableExtractionService()
            : base(NullLogger<PdfTableExtractionService>.Instance)
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

        private static readonly List<DocumentChunkInput> Chunks = new()
        {
            new() { Text = "chunk-one", Page = 1, CharStart = 0, CharEnd = 9 },
            new() { Text = "chunk-two", Page = 1, CharStart = 10, CharEnd = 19 }
        };

        public List<TextChunk> ChunkText(string text, int chunkSize = 512, int overlap = 50)
        {
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
            return Chunks;
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
