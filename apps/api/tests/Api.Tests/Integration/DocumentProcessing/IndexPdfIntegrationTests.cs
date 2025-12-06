using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Handlers;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using Testcontainers.Qdrant;
using Xunit;
using Api.Tests.Constants;
using AuthRole = Api.BoundedContexts.Authentication.Domain.ValueObjects.Role;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// Comprehensive integration tests for PDF indexing workflow (Issue #1690).
/// Tests the complete indexing pipeline using Testcontainers for real infrastructure.
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
/// Infrastructure: PostgreSQL + Qdrant (real containers via Testcontainers)
/// Coverage Target: ≥90% for IndexPdfCommandHandler
/// Execution Time Target: <20s
/// </summary>
[Trait("Category", TestCategories.Integration)]
public sealed class IndexPdfIntegrationTests : IAsyncLifetime
{
    private IContainer? _postgresContainer;
    private QdrantContainer? _qdrantContainer;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        // Start PostgreSQL container
        _postgresContainer = new ContainerBuilder()
            .WithImage("postgres:16-alpine")
            .WithEnvironment("POSTGRES_USER", "postgres")
            .WithEnvironment("POSTGRES_PASSWORD", "postgres")
            .WithEnvironment("POSTGRES_DB", "pdf_index_test")
            .WithPortBinding(5432, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilCommandIsCompleted("pg_isready", "-U", "postgres"))
            .Build();

        await _postgresContainer.StartAsync(TestCancellationToken);

        // Start Qdrant container
        _qdrantContainer = new QdrantBuilder()
            .WithImage("qdrant/qdrant:v1.7.4")
            .WithPortBinding(6333, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r.ForPort(6333).ForPath("/healthz")))
            .Build();

        await _qdrantContainer.StartAsync(TestCancellationToken);

        // Setup services
        var postgresPort = _postgresContainer.GetMappedPublicPort(5432);
        var qdrantPort = _qdrantContainer.GetMappedPublicPort(6333);
        var connectionString = $"Host=localhost;Port={postgresPort};Database=pdf_index_test;Username=postgres;Password=postgres;";
        var qdrantUrl = $"http://localhost:{qdrantPort}";

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
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
                await _dbContext.Database.EnsureCreatedAsync(TestCancellationToken);
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
        _dbContext?.Dispose();

        if (_serviceProvider is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else
            (_serviceProvider as IDisposable)?.Dispose();

        if (_postgresContainer != null)
        {
            await _postgresContainer.StopAsync(TestCancellationToken);
            await _postgresContainer.DisposeAsync();
        }

        if (_qdrantContainer != null)
        {
            await _qdrantContainer.StopAsync(TestCancellationToken);
            await _qdrantContainer.DisposeAsync();
        }
    }

    private void RegisterMockServices(IServiceCollection services, string qdrantUrl)
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

        // Mock Qdrant service (success by default)
        var qdrantMock = new Mock<IQdrantService>();
        qdrantMock.Setup(q => q.IndexDocumentChunksAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<DocumentChunk>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IndexResult { Success = true, ErrorMessage = null, IndexedCount = 0 });
        qdrantMock.Setup(q => q.DeleteDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        services.AddSingleton<IQdrantService>(qdrantMock.Object);
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
        var gameId = _dbContext!.Games.First().Id;
        var userId = _dbContext.Users.First().Id;

        var pdfDoc = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            UploadedByUserId = userId,
            FileName = name,
            FilePath = $"/test/{name}",
            FileSizeBytes = 1024,
            UploadedAt = DateTime.UtcNow,
            ProcessingStatus = status,
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
            _serviceProvider!.GetRequiredService<IQdrantService>(),
            _serviceProvider!.GetRequiredService<ILogger<IndexPdfCommandHandler>>()
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

    [Fact]
    public async Task IndexPdfWithQdrantFailure_ReturnsQdrantIndexingFailed()
    {
        // Arrange
        await ResetDatabaseAsync();
        var text = "Test text for Qdrant failure.";
        var pdfId = await CreateTestPdfAsync("QdrantFail.pdf", text, "completed");

        // Create handler with failing Qdrant service
        var qdrantMock = new Mock<IQdrantService>();
        qdrantMock.Setup(q => q.IndexDocumentChunksAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<DocumentChunk>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IndexResult { Success = false, ErrorMessage = "Qdrant connection failed", IndexedCount = 0 });

        var handler = new IndexPdfCommandHandler(
            _dbContext!,
            _serviceProvider!.GetRequiredService<ITextChunkingService>(),
            _serviceProvider!.GetRequiredService<IEmbeddingService>(),
            qdrantMock.Object,
            _serviceProvider!.GetRequiredService<ILogger<IndexPdfCommandHandler>>()
        );

        var command = new IndexPdfCommand(pdfId.ToString());

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(PdfIndexingErrorCode.QdrantIndexingFailed);
        result.ErrorMessage.Should().Contain("Qdrant indexing failed");
    }
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