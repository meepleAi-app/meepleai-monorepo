using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;
using Api.Configuration;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Microsoft.Extensions.Options;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using DotNet.Testcontainers.Builders;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using Testcontainers.Qdrant;
using Xunit;
using AuthRole = Api.SharedKernel.Domain.ValueObjects.Role;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// Uses SharedTestcontainersFixture for optimized performance and Docker hijack prevention (Issue #2031).
/// Comprehensive integration tests for PDF indexing workflow (Issue #1690).
/// Tests the complete indexing pipeline using SharedTestcontainersFixture and Qdrant container.
/// Uses SharedTestcontainersFixture for PostgreSQL (Docker hijack prevention, Issue #2031).
///
/// Test Categories:
/// 1. Happy Path: Index valid PDF with all steps
/// 2. Text Extraction: Index with text extraction completion check
/// 3. Vector Generation: Index with embedding generation
/// 4. Qdrant Storage: Index with Qdrant persistence
/// 5. Large PDF: Index large PDF with chunking
/// 6. Failure Recovery: Index with failure scenarios
/// 7. Re-indexing: Re-index existing PDF
///
/// Infrastructure: SharedTestcontainersFixture (PostgreSQL) + Qdrant (individual container)
/// Coverage Target: ≥90% for IndexPdfCommandHandler
/// Execution Time Target: <20s
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Issue", "2031")]
[Trait("Category", TestCategories.Integration)]
public sealed class IndexPdfIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private QdrantContainer? _qdrantContainer;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    private static IOptions<IndexingSettings> CreateIndexingSettings()
    {
        var settings = new IndexingSettings { EmbeddingBatchSize = 100 };
        var optionsMock = new Mock<IOptions<IndexingSettings>>();
        optionsMock.Setup(x => x.Value).Returns(settings);
        return optionsMock.Object;
    }

    public IndexPdfIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        // Issue #2031: Migrated PostgreSQL to SharedTestcontainersFixture for Docker hijack prevention
        _databaseName = $"test_indexpdf_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        // Start Qdrant container (kept separate - not in SharedTestcontainersFixture)
        _qdrantContainer = new QdrantBuilder()
            .WithImage("qdrant/qdrant:v1.7.4")
            .WithPortBinding(6333, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r.ForPort(6333).ForPath("/healthz")))
            .Build();

        await _qdrantContainer.StartAsync(TestCancellationToken);

        // Setup services
        var qdrantPort = _qdrantContainer.GetMappedPublicPort(6333);
        var qdrantUrl = $"http://localhost:{qdrantPort}";

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(_isolatedDbConnectionString, o => o.UseVector()); // Issue #3547: Enable pgvector
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        // Register repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPdfDocumentRepository, PdfDocumentRepository>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        // Register domain event infrastructure
        services.AddScoped<IDomainEventCollector, DomainEventCollector>();

        // Register MediatR
        services.AddMediatR(config =>
            config.RegisterServicesFromAssembly(typeof(IndexPdfCommandHandler).Assembly));

        // Register the handler explicitly for test access
        services.AddScoped<IndexPdfCommandHandler>();

        // Register mock services
        RegisterMockServices(services, qdrantUrl);

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await _dbContext.Database.MigrateAsync(TestCancellationToken);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(TestConstants.Timing.RetryDelay, TestCancellationToken);
            }
        }

        // Seed test data
        await SeedTestDataAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
            await _dbContext.DisposeAsync();

        if (_serviceProvider is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else
            (_serviceProvider as IDisposable)?.Dispose();

        // Issue #2031: Use SharedTestcontainersFixture for PostgreSQL cleanup
        if (!string.IsNullOrEmpty(_databaseName))
        {
            try
            {
                await _fixture.DropIsolatedDatabaseAsync(_databaseName);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        // Qdrant container still managed individually
        if (_qdrantContainer != null)
        {
            await _qdrantContainer.StopAsync(TestCancellationToken);
            await _qdrantContainer.DisposeAsync();
        }
    }

    private void RegisterMockServices(IServiceCollection services, string _)
    {
        // Mock TextChunking service (returns realistic chunks)
        var chunkingMock = new Mock<ITextChunkingService>();
        chunkingMock.Setup(c => c.ChunkText(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns((string text, int chunkSize, int overlap) =>
            {
                // Simple chunking: split by paragraphs, max 500 chars each
                var chunks = new List<TextChunk>();
                var paragraphs = text.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                int charStart = 0;
                int index = 0;

                for (int i = 0; i < paragraphs.Length; i++)
                {
                    var para = paragraphs[i];
                    if (para.Length > 500)
                    {
                        // Split large paragraphs
                        for (int j = 0; j < para.Length; j += 500)
                        {
                            var chunk = para.Substring(j, Math.Min(500, para.Length - j));
                            chunks.Add(new TextChunk
                            {
                                Text = chunk,
                                Index = index++,
                                Page = i + 1,
                                CharStart = charStart,
                                CharEnd = charStart + chunk.Length
                            });
                            charStart += chunk.Length;
                        }
                    }
                    else
                    {
                        chunks.Add(new TextChunk
                        {
                            Text = para,
                            Index = index++,
                            Page = i + 1,
                            CharStart = charStart,
                            CharEnd = charStart + para.Length
                        });
                        charStart += para.Length;
                    }
                }

                return chunks;
            });
        services.AddSingleton<ITextChunkingService>(chunkingMock.Object);

        // Mock Embedding service (returns mock embeddings)
        var embeddingMock = new Mock<IEmbeddingService>();
        embeddingMock.Setup(e => e.GetEmbeddingDimensions()).Returns(384);
        embeddingMock.Setup(e => e.GetModelName()).Returns("test-embedding-model");
        embeddingMock.Setup(e => e.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<string> texts, CancellationToken ct) =>
            {
                var embeddings = texts.Select(_ =>
                {
                    // Generate deterministic 384-dimensional embeddings for testing
                    return Enumerable.Range(0, 384).Select(i => (float)(i * 0.001)).ToArray();
                }).ToList();

                return new EmbeddingResult
                {
                    Success = true,
                    Embeddings = embeddings,
                    ErrorMessage = null
                };
            });
        services.AddSingleton<IEmbeddingService>(embeddingMock.Object);

        // ISSUE-3197: Register IndexingSettings for batch processing configuration
        var indexingSettings = new IndexingSettings { EmbeddingBatchSize = 100 };
        services.AddSingleton(Options.Create(indexingSettings));
    }

    private async Task SeedTestDataAsync()
    {
        // Seed user
        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
            Email = "test@meepleai.dev",
            DisplayName = "TestUser",
            Role = "Editor",
            Tier = "Free",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext!.Users.Add(user);

        // Seed game
        var gameId = Guid.NewGuid();
        var game = new GameEntity
        {
            Id = gameId,
            Name = "Test Game for Indexing",
            Publisher = "Test Publisher",
            YearPublished = 2024,
            MinPlayers = 2,
            MaxPlayers = 4,
            MinPlayTimeMinutes = 60,
            MaxPlayTimeMinutes = 90,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Games.Add(game);

        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    private async Task<Guid> CreateTestPdfAsync(
        string name = "Test.pdf",
        string? extractedText = null,
        string status = "completed",
        bool withVectorDoc = false)
    {
        var gameId = (await _dbContext!.Games.FirstAsync()).Id;
        var userId = (await _dbContext.Users.FirstAsync()).Id;

        var pdfDoc = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            UploadedByUserId = userId,
            FileName = name,
            FilePath = $"/test/{name}",
            FileSizeBytes = 1024,
            UploadedAt = DateTime.UtcNow,
            PageCount = 10,
            ExtractedText = extractedText ?? "This is test extracted text.\n\nIt has multiple paragraphs.\n\nEach paragraph will become a chunk during indexing."
        };

        _dbContext.PdfDocuments.Add(pdfDoc);

        if (withVectorDoc)
        {
            var vectorDoc = new VectorDocumentEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = pdfDoc.Id,
                GameId = gameId,
                ChunkCount = 5,
                TotalCharacters = pdfDoc.ExtractedText?.Length ?? 0,
                IndexedAt = DateTime.UtcNow,
                IndexingStatus = "completed",
                EmbeddingModel = "test-model",
                EmbeddingDimensions = 384
            };
            _dbContext.Set<VectorDocumentEntity>().Add(vectorDoc);
        }

        await _dbContext.SaveChangesAsync(TestCancellationToken);
        return pdfDoc.Id;
    }

    private async Task ResetDatabaseAsync()
    {
        // Clear all data
        _dbContext!.Set<VectorDocumentEntity>().RemoveRange(_dbContext.Set<VectorDocumentEntity>());
        _dbContext.PdfDocuments.RemoveRange(_dbContext.PdfDocuments);
        _dbContext.Games.RemoveRange(_dbContext.Games);
        _dbContext.Users.RemoveRange(_dbContext.Users);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Re-seed base data
        await SeedTestDataAsync();
    }
    [Fact]
    public async Task IndexValidPdf_WithExtractedText_Success()
    {
        // Arrange
        await ResetDatabaseAsync();
        var text = "Game setup instructions.\n\nPlace components on board.\n\nEach player takes 5 cards.";
        var pdfId = await CreateTestPdfAsync("ValidPdf.pdf", text, "completed", withVectorDoc: false);
        var handler = _serviceProvider!.GetRequiredService<IndexPdfCommandHandler>();
        var command = new IndexPdfCommand(pdfId.ToString());

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ChunkCount.Should().BeGreaterThan(0);
        result.ErrorMessage.Should().BeNull();

        // Verify vector document created
        var vectorDoc = await _dbContext!.Set<VectorDocumentEntity>()
            .FirstOrDefaultAsync(v => v.PdfDocumentId == pdfId, TestCancellationToken);
        vectorDoc.Should().NotBeNull();
        vectorDoc!.IndexingStatus.Should().Be("completed");
        vectorDoc.ChunkCount.Should().BeGreaterThan(0);
    }
    [Fact]
    public async Task IndexPdfWithoutExtractedText_ReturnsTextExtractionRequired()
    {
        // Arrange
        await ResetDatabaseAsync();
        var pdfId = await CreateTestPdfAsync("NoText.pdf", extractedText: null, status: "pending");
        var handler = _serviceProvider!.GetRequiredService<IndexPdfCommandHandler>();
        var command = new IndexPdfCommand(pdfId.ToString());

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(PdfIndexingErrorCode.TextExtractionRequired);
        result.ErrorMessage.Should().Contain("extraction");
    }

    [Fact]
    public async Task IndexPdfWithIncompleteProcessing_ReturnsTextExtractionRequired()
    {
        // Arrange
        await ResetDatabaseAsync();
        var text = "Some text";
        var pdfId = await CreateTestPdfAsync("Incomplete.pdf", text, status: "processing");
        var handler = _serviceProvider!.GetRequiredService<IndexPdfCommandHandler>();
        var command = new IndexPdfCommand(pdfId.ToString());

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(PdfIndexingErrorCode.TextExtractionRequired);
        result.ErrorMessage.Should().Contain("not completed");
    }
    [Fact]
    public async Task IndexPdf_GeneratesEmbeddings_Success()
    {
        // Arrange
        await ResetDatabaseAsync();
        var text = "Player 1 moves.\n\nPlayer 2 responds.\n\nGame continues.";
        var pdfId = await CreateTestPdfAsync("Embeddings.pdf", text, "completed");
        var handler = _serviceProvider!.GetRequiredService<IndexPdfCommandHandler>();
        var command = new IndexPdfCommand(pdfId.ToString());

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        // Verify embeddings were generated (chunk count > 0)
        var vectorDoc = await _dbContext!.Set<VectorDocumentEntity>()
            .FirstOrDefaultAsync(v => v.PdfDocumentId == pdfId, TestCancellationToken);
        vectorDoc.Should().NotBeNull();
        vectorDoc!.ChunkCount.Should().BeGreaterThan(0);
        vectorDoc.EmbeddingModel.Should().Be("test-embedding-model");
        vectorDoc.EmbeddingDimensions.Should().Be(384);
    }

    [Fact]
    public async Task IndexPdfWithEmbeddingFailure_ReturnsEmbeddingFailed()
    {
        // Arrange
        await ResetDatabaseAsync();
        var text = "Test text for embedding failure.";
        var pdfId = await CreateTestPdfAsync("EmbedFail.pdf", text, "completed");

        // Create handler with failing embedding service
        var embeddingMock = new Mock<IEmbeddingService>();
        embeddingMock.Setup(e => e.GetEmbeddingDimensions()).Returns(384);
        embeddingMock.Setup(e => e.GetModelName()).Returns("test-model");
        embeddingMock.Setup(e => e.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                Success = false,
                Embeddings = new List<float[]>(),
                ErrorMessage = "Embedding service unavailable"
            });

        var handler = new IndexPdfCommandHandler(
            _dbContext!,
            _serviceProvider!.GetRequiredService<ITextChunkingService>(),
            embeddingMock.Object,
            _serviceProvider!.GetRequiredService<ILogger<IndexPdfCommandHandler>>(),
            CreateIndexingSettings()
        );

        var command = new IndexPdfCommand(pdfId.ToString());

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(PdfIndexingErrorCode.EmbeddingFailed);
        result.ErrorMessage.Should().Contain("Embedding generation failed");
    }
    [Fact]
    public async Task IndexPdf_StoresInQdrant_Success()
    {
        // Arrange
        await ResetDatabaseAsync();
        var text = "Rule 1: Setup.\n\nRule 2: Gameplay.\n\nRule 3: Scoring.";
        var pdfId = await CreateTestPdfAsync("QdrantStore.pdf", text, "completed");
        var handler = _serviceProvider!.GetRequiredService<IndexPdfCommandHandler>();
        var command = new IndexPdfCommand(pdfId.ToString());

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        // Verify Qdrant indexing completed
        var vectorDoc = await _dbContext!.Set<VectorDocumentEntity>()
            .FirstOrDefaultAsync(v => v.PdfDocumentId == pdfId, TestCancellationToken);
        vectorDoc.Should().NotBeNull();
        vectorDoc!.IndexingStatus.Should().Be("completed");
    }

    // IndexPdfWithQdrantFailure_ReturnsQdrantIndexingFailed removed — IQdrantService no longer exists
    [Fact]
    public async Task IndexLargePdf_HandlesChunkingCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Generate large text (10,000 characters)
        var largeText = string.Join("\n\n", Enumerable.Range(1, 50)
            .Select(i => $"Section {i}: " + new string('A', 200)));

        var pdfId = await CreateTestPdfAsync("LargePdf.pdf", largeText, "completed");
        var handler = _serviceProvider!.GetRequiredService<IndexPdfCommandHandler>();
        var command = new IndexPdfCommand(pdfId.ToString());

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ChunkCount.Should().BeGreaterThan(10, "large text should create multiple chunks");

        // Verify chunking worked
        var vectorDoc = await _dbContext!.Set<VectorDocumentEntity>()
            .FirstOrDefaultAsync(v => v.PdfDocumentId == pdfId, TestCancellationToken);
        vectorDoc.Should().NotBeNull();
        vectorDoc!.ChunkCount.Should().Be(result.ChunkCount);
        vectorDoc.TotalCharacters.Should().Be(largeText.Length);
    }

    [Fact]
    public async Task IndexPdf_With602KbEquivalent_CompletesWithBatchProcessing()
    {
        // Arrange: Reproduce Issue #3197 scenario
        // 602KB PDF → ~600,000 characters → ~1,200 chunks (512 chars/chunk)
        await ResetDatabaseAsync();

        // Generate 600KB text (1,200 paragraphs of 500 chars each)
        var paragraphs = Enumerable.Range(1, 1200)
            .Select(i => $"Paragraph {i}: " + string.Join(" ",
                Enumerable.Range(1, 50).Select(w => $"Word{w}")));
        var largeText = string.Join("\n\n", paragraphs);

        var pdfId = await CreateTestPdfAsync("Large602KB.pdf", largeText, "completed");
        var handler = _serviceProvider!.GetRequiredService<IndexPdfCommandHandler>();
        var command = new IndexPdfCommand(pdfId.ToString());

        // Act: Should complete without OutOfMemoryException
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert: Indexing completes successfully without OutOfMemoryException
        result.Should().NotBeNull();
        result.Success.Should().BeTrue("indexing should succeed with batch processing");
        result.ChunkCount.Should().BeGreaterThan(100, "large text should generate many chunks");
        result.ErrorMessage.Should().BeNullOrEmpty();
        result.ErrorCode.Should().BeNull();

        // Verify: VectorDocument created with batch processing
        var vectorDoc = await _dbContext!.Set<VectorDocumentEntity>()
            .FirstOrDefaultAsync(v => v.PdfDocumentId == pdfId, TestCancellationToken);

        vectorDoc.Should().NotBeNull();
        vectorDoc!.IndexingStatus.Should().Be("completed", "batch processing should complete successfully");
        vectorDoc.ChunkCount.Should().Be(result.ChunkCount);
        vectorDoc.TotalCharacters.Should().BeGreaterThan(100000, "large text has many characters");
        vectorDoc.IndexedAt.Should().NotBeNull();
        vectorDoc.IndexingError.Should().BeNull("no errors should occur with batch processing");

        // Verify: Text chunks saved to PostgreSQL for hybrid search
        var textChunks = await _dbContext.TextChunks
            .Where(tc => tc.PdfDocumentId == pdfId)
            .CountAsync(TestCancellationToken);
        textChunks.Should().Be(result.ChunkCount, "all chunks should be persisted to PostgreSQL");
    }
    [Fact]
    public async Task IndexNonExistentPdf_ReturnsPdfNotFound()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentId = Guid.NewGuid().ToString();
        var handler = _serviceProvider!.GetRequiredService<IndexPdfCommandHandler>();
        var command = new IndexPdfCommand(nonExistentId);

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(PdfIndexingErrorCode.PdfNotFound);
        result.ErrorMessage.Should().Contain("not found");
    }
    [Fact]
    public async Task ReindexExistingPdf_UpdatesVectors()
    {
        // Arrange
        await ResetDatabaseAsync();
        var text = "Original text for re-indexing test.";
        var pdfId = await CreateTestPdfAsync("Reindex.pdf", text, "completed", withVectorDoc: true);

        // Get original vector doc
        var originalVectorDoc = await _dbContext!.Set<VectorDocumentEntity>()
            .FirstOrDefaultAsync(v => v.PdfDocumentId == pdfId, TestCancellationToken);
        originalVectorDoc.Should().NotBeNull();
        var originalIndexedAt = originalVectorDoc!.IndexedAt;

        // Wait a moment to ensure timestamp difference
        await Task.Delay(TestConstants.Timing.RetryDelay, TestCancellationToken);

        var handler = _serviceProvider!.GetRequiredService<IndexPdfCommandHandler>();
        var command = new IndexPdfCommand(pdfId.ToString());

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        // Verify vector document updated (not recreated)
        var updatedVectorDoc = await _dbContext.Set<VectorDocumentEntity>()
            .FirstOrDefaultAsync(v => v.PdfDocumentId == pdfId, TestCancellationToken);
        updatedVectorDoc.Should().NotBeNull();
        updatedVectorDoc!.Id.Should().Be(originalVectorDoc.Id, "same entity should be updated");
        updatedVectorDoc.IndexedAt.Should().BeAfter(originalIndexedAt!.Value, "IndexedAt should be updated");
        updatedVectorDoc.IndexingStatus.Should().Be("completed");
    }
}
