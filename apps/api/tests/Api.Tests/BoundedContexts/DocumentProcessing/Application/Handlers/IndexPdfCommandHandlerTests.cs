using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Handlers;
using Api.Configuration;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Handlers;

/// <summary>
/// Tests for IndexPdfCommandHandler.
/// ISSUE-1818: Migrated to FluentAssertions for improved readability.
/// Tests PDF text indexing workflow (chunking, embedding, Qdrant indexing).
/// NOTE: Complex orchestrator with many dependencies - focused on construction and validation.
/// RESOLVED: Issue #1690 - Integration tests added in IndexPdfIntegrationTests.cs.
/// ISSUE-1500: TEST-002 - Fixed test isolation (fresh context per test)
/// ISSUE-1818: Migrated to FluentAssertions for improved readability.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class IndexPdfCommandHandlerTests
{
    /// <summary>
    /// Creates a fresh DbContext for each test to ensure complete isolation
    /// </summary>
    private static MeepleAiDbContext CreateFreshDbContext()
    {
        return TestDbContextFactory.CreateInMemoryDbContext();
    }

    /// <summary>
    /// Creates a fresh set of mocks for each test
    /// </summary>
    private static (Mock<ITextChunkingService>, Mock<IEmbeddingService>, Mock<IQdrantService>, Mock<ILogger<IndexPdfCommandHandler>>, Mock<IOptions<IndexingSettings>>) CreateMocks()
    {
        var chunkingServiceMock = new Mock<ITextChunkingService>();
        var embeddingServiceMock = new Mock<IEmbeddingService>();
        var qdrantServiceMock = new Mock<IQdrantService>();
        var loggerMock = new Mock<ILogger<IndexPdfCommandHandler>>();
        var indexingSettingsMock = new Mock<IOptions<IndexingSettings>>();

        // Configure default batch size
        var settings = new IndexingSettings { EmbeddingBatchSize = 100 };
        indexingSettingsMock.Setup(x => x.Value).Returns(settings);

        return (chunkingServiceMock, embeddingServiceMock, qdrantServiceMock, loggerMock, indexingSettingsMock);
    }
    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Arrange - fresh resources per test
        using var context = CreateFreshDbContext();
        var (chunkingServiceMock, embeddingServiceMock, qdrantServiceMock, loggerMock, indexingSettingsMock) = CreateMocks();

        // Act
        var handler = new IndexPdfCommandHandler(
            context,
            chunkingServiceMock.Object,
            embeddingServiceMock.Object,
            qdrantServiceMock.Object,
            loggerMock.Object,
            indexingSettingsMock.Object);

        // Assert
        handler.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithTimeProvider_CreatesInstance()
    {
        // Arrange - fresh resources per test
        using var context = CreateFreshDbContext();
        var (chunkingServiceMock, embeddingServiceMock, qdrantServiceMock, loggerMock, indexingSettingsMock) = CreateMocks();
        var timeProvider = TimeProvider.System;

        // Act
        var handler = new IndexPdfCommandHandler(
            context,
            chunkingServiceMock.Object,
            embeddingServiceMock.Object,
            qdrantServiceMock.Object,
            loggerMock.Object,
            indexingSettingsMock.Object,
            timeProvider);

        // Assert
        handler.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullTimeProvider_UsesSystemTimeProvider()
    {
        // Arrange - fresh resources per test
        using var context = CreateFreshDbContext();
        var (chunkingServiceMock, embeddingServiceMock, qdrantServiceMock, loggerMock, indexingSettingsMock) = CreateMocks();

        // Act
        var handler = new IndexPdfCommandHandler(
            context,
            chunkingServiceMock.Object,
            embeddingServiceMock.Object,
            qdrantServiceMock.Object,
            loggerMock.Object,
            indexingSettingsMock.Object,
            null);

        // Assert
        handler.Should().NotBeNull();
    }
    [Fact]
    public void IndexPdfCommand_ConstructsCorrectly()
    {
        // Arrange
        var pdfId = Guid.NewGuid().ToString();

        // Act
        var command = new IndexPdfCommand(pdfId);

        // Assert
        command.PdfId.Should().Be(pdfId);
    }
    [Fact]
    public void IndexingResultDto_CreateSuccess_ConstructsCorrectly()
    {
        // Arrange
        var chunkCount = 42;
        var vectorCount = 42;

        // Act
        var result = IndexingResultDto.CreateSuccess("vector-doc-id", chunkCount, DateTime.UtcNow);

        // Assert
        result.Success.Should().BeTrue();
        result.ChunkCount.Should().Be(chunkCount);
        result.ErrorMessage.Should().BeNull();
        result.ErrorCode.Should().BeNull();
    }

    [Fact]
    public void IndexingResultDto_CreateFailure_ConstructsCorrectly()
    {
        // Arrange
        var errorMessage = "PDF not found";
        var errorCode = PdfIndexingErrorCode.PdfNotFound;

        // Act
        var result = IndexingResultDto.CreateFailure(errorMessage, errorCode);

        // Assert
        result.Success.Should().BeFalse();
        result.ChunkCount.Should().Be(0);
        result.ErrorMessage.Should().Be(errorMessage);
        result.ErrorCode.Should().Be(errorCode);
    }

    [Fact]
    public void IndexingResultDto_CreateFailure_WithTextExtractionRequired()
    {
        // Act
        var result = IndexingResultDto.CreateFailure(
            "Text extraction required",
            PdfIndexingErrorCode.TextExtractionRequired);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(PdfIndexingErrorCode.TextExtractionRequired);
    }

    [Fact]
    public void IndexingResultDto_CreateFailure_WithChunkingFailure()
    {
        // Act
        var result = IndexingResultDto.CreateFailure(
            "Chunking failed",
            PdfIndexingErrorCode.ChunkingFailed);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(PdfIndexingErrorCode.ChunkingFailed);
    }

    [Fact]
    public void IndexingResultDto_CreateFailure_WithEmbeddingFailure()
    {
        // Act
        var result = IndexingResultDto.CreateFailure(
            "Embedding generation failed",
            PdfIndexingErrorCode.EmbeddingFailed);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(PdfIndexingErrorCode.EmbeddingFailed);
    }

    [Fact]
    public void IndexingResultDto_CreateFailure_WithQdrantIndexingFailure()
    {
        // Act
        var result = IndexingResultDto.CreateFailure(
            "Qdrant indexing failed",
            PdfIndexingErrorCode.QdrantIndexingFailed);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(PdfIndexingErrorCode.QdrantIndexingFailed);
    }
    [Theory]
    [InlineData(PdfIndexingErrorCode.PdfNotFound)]
    [InlineData(PdfIndexingErrorCode.TextExtractionRequired)]
    [InlineData(PdfIndexingErrorCode.ChunkingFailed)]
    [InlineData(PdfIndexingErrorCode.EmbeddingFailed)]
    [InlineData(PdfIndexingErrorCode.QdrantIndexingFailed)]
    [InlineData(PdfIndexingErrorCode.UnexpectedError)]
    public void PdfIndexingErrorCode_AllValuesAreValid(PdfIndexingErrorCode errorCode)
    {
        // Assert
        Enum.IsDefined(typeof(PdfIndexingErrorCode), errorCode).Should().BeTrue();
    }

    // ISSUE-3197: Batch processing tests for memory optimization
    [Fact]
    public async Task Handle_WithLargeInput_ProcessesEmbeddingsInBatches()
    {
        // Arrange: Create 250 chunks (should trigger 3 batches with size 100)
        using var context = CreateFreshDbContext();
        var (chunkingServiceMock, embeddingServiceMock, qdrantServiceMock, loggerMock, indexingSettingsMock) = CreateMocks();

        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        var pdf = CreatePdfDocument(pdfId, gameId, "completed", GenerateExtractedText(250));
        await context.PdfDocuments.AddAsync(pdf);
        await context.SaveChangesAsync();

        var textChunks = GenerateTextChunks(250);
        chunkingServiceMock
            .Setup(x => x.ChunkText(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(textChunks);

        var embeddingCallCount = 0;
        embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<string> texts, CancellationToken ct) =>
            {
                embeddingCallCount++;
                var embeddings = texts.Select(_ => GenerateRandomEmbedding(3072)).ToList();
                return new EmbeddingResult { Success = true, Embeddings = embeddings };
            });

        embeddingServiceMock.Setup(x => x.GetEmbeddingDimensions()).Returns(3072);
        embeddingServiceMock.Setup(x => x.GetModelName()).Returns("text-embedding-3-large");

        qdrantServiceMock
            .Setup(x => x.IndexDocumentChunksAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<DocumentChunk>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IndexResult { Success = true });

        var handler = new IndexPdfCommandHandler(
            context,
            chunkingServiceMock.Object,
            embeddingServiceMock.Object,
            qdrantServiceMock.Object,
            loggerMock.Object,
            indexingSettingsMock.Object);

        var command = new IndexPdfCommand(pdfId.ToString());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        // Verify: 3 embedding calls (100 + 100 + 50)
        embeddingServiceMock.Verify(
            x => x.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3)
        );

        // Verify batch sizes
        embeddingServiceMock.Verify(
            x => x.GenerateEmbeddingsAsync(It.Is<List<string>>(l => l.Count == 100), It.IsAny<CancellationToken>()),
            Times.Exactly(2)
        );
        embeddingServiceMock.Verify(
            x => x.GenerateEmbeddingsAsync(It.Is<List<string>>(l => l.Count == 50), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Handle_WithBatchSize100_Makes12ApiCallsFor1200Chunks()
    {
        // Arrange
        using var context = CreateFreshDbContext();
        var (chunkingServiceMock, embeddingServiceMock, qdrantServiceMock, loggerMock, indexingSettingsMock) = CreateMocks();

        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        var pdf = CreatePdfDocument(pdfId, gameId, "completed", GenerateExtractedText(1200));
        await context.PdfDocuments.AddAsync(pdf);
        await context.SaveChangesAsync();

        var textChunks = GenerateTextChunks(1200);
        chunkingServiceMock
            .Setup(x => x.ChunkText(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(textChunks);

        embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<string> texts, CancellationToken ct) =>
            {
                var embeddings = texts.Select(_ => GenerateRandomEmbedding(3072)).ToList();
                return new EmbeddingResult { Success = true, Embeddings = embeddings };
            });

        embeddingServiceMock.Setup(x => x.GetEmbeddingDimensions()).Returns(3072);
        embeddingServiceMock.Setup(x => x.GetModelName()).Returns("text-embedding-3-large");

        qdrantServiceMock
            .Setup(x => x.IndexDocumentChunksAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<DocumentChunk>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IndexResult { Success = true });

        var handler = new IndexPdfCommandHandler(
            context,
            chunkingServiceMock.Object,
            embeddingServiceMock.Object,
            qdrantServiceMock.Object,
            loggerMock.Object,
            indexingSettingsMock.Object);

        var command = new IndexPdfCommand(pdfId.ToString());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        // Verify: 12 embedding calls (1200 / 100 = 12)
        embeddingServiceMock.Verify(
            x => x.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()),
            Times.Exactly(12)
        );
    }

    [Fact]
    public async Task Handle_WithFailedBatch_PropagatesException()
    {
        // Arrange
        using var context = CreateFreshDbContext();
        var (chunkingServiceMock, embeddingServiceMock, qdrantServiceMock, loggerMock, indexingSettingsMock) = CreateMocks();

        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        var pdf = CreatePdfDocument(pdfId, gameId, "completed", GenerateExtractedText(200));
        await context.PdfDocuments.AddAsync(pdf);
        await context.SaveChangesAsync();

        var textChunks = GenerateTextChunks(200);
        chunkingServiceMock
            .Setup(x => x.ChunkText(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(textChunks);

        var callCount = 0;
        embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<string> texts, CancellationToken ct) =>
            {
                callCount++;
                if (callCount == 2)
                {
                    // Fail on second batch
                    return new EmbeddingResult { Success = false, ErrorMessage = "Batch processing failed" };
                }
                var embeddings = texts.Select(_ => GenerateRandomEmbedding(3072)).ToList();
                return new EmbeddingResult { Success = true, Embeddings = embeddings };
            });

        embeddingServiceMock.Setup(x => x.GetEmbeddingDimensions()).Returns(3072);
        embeddingServiceMock.Setup(x => x.GetModelName()).Returns("text-embedding-3-large");

        var handler = new IndexPdfCommandHandler(
            context,
            chunkingServiceMock.Object,
            embeddingServiceMock.Object,
            qdrantServiceMock.Object,
            loggerMock.Object,
            indexingSettingsMock.Object);

        var command = new IndexPdfCommand(pdfId.ToString());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Embedding generation failed");
        result.ErrorCode.Should().Be(PdfIndexingErrorCode.EmbeddingFailed);

        // Verify: Only 2 calls (first succeeds, second fails)
        embeddingServiceMock.Verify(
            x => x.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2)
        );
    }

    [Fact]
    public async Task Handle_WithEmptyExtractedText_ReturnsTextExtractionRequired()
    {
        // Arrange
        using var context = CreateFreshDbContext();
        var (chunkingServiceMock, embeddingServiceMock, qdrantServiceMock, loggerMock, indexingSettingsMock) = CreateMocks();

        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        var pdf = CreatePdfDocument(pdfId, gameId, "completed", "");
        await context.PdfDocuments.AddAsync(pdf);
        await context.SaveChangesAsync();

        embeddingServiceMock.Setup(x => x.GetEmbeddingDimensions()).Returns(3072);
        embeddingServiceMock.Setup(x => x.GetModelName()).Returns("text-embedding-3-large");

        var handler = new IndexPdfCommandHandler(
            context,
            chunkingServiceMock.Object,
            embeddingServiceMock.Object,
            qdrantServiceMock.Object,
            loggerMock.Object,
            indexingSettingsMock.Object);

        var command = new IndexPdfCommand(pdfId.ToString());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(PdfIndexingErrorCode.TextExtractionRequired);
        result.ErrorMessage.Should().Contain("extraction required");

        // Verify: No chunking or embedding calls
        chunkingServiceMock.Verify(
            x => x.ChunkText(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()),
            Times.Never
        );
        embeddingServiceMock.Verify(
            x => x.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    // Helper methods
    private static PdfDocumentEntity CreatePdfDocument(Guid id, Guid gameId, string status, string extractedText)
    {
        return new PdfDocumentEntity
        {
            Id = id,
            GameId = gameId,
            FileName = "test.pdf",
            FilePath = "/uploads/test.pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = Guid.NewGuid(),
            UploadedAt = DateTime.UtcNow,
            ProcessingStatus = status,
            // Handler validates ProcessingState == "Ready" before indexing
            ProcessingState = status == "completed" ? "Ready" : "Pending",
            ExtractedText = extractedText,
            Game = new GameEntity
            {
                Id = gameId,
                Name = "Test Game",
                MinPlayers = 2,
                MaxPlayers = 4,
                CreatedAt = DateTime.UtcNow
            }
        };
    }

    private static string GenerateExtractedText(int chunkCount)
    {
        // Generate text that will produce roughly chunkCount chunks
        // Assume ~512 chars per chunk
        var text = string.Join(" ", Enumerable.Range(1, chunkCount * 50).Select(i => $"Word{i}"));
        return text;
    }

    private static List<TextChunk> GenerateTextChunks(int count)
    {
        return Enumerable.Range(1, count)
            .Select(i => new TextChunk
            {
                Text = $"Chunk {i} content with sufficient text to simulate real chunks",
                Page = (i / 10) + 1,
                CharStart = (i - 1) * 512,
                CharEnd = i * 512
            })
            .ToList();
    }

    private static float[] GenerateRandomEmbedding(int dimensions)
    {
#pragma warning disable CA5394 // Random is sufficient for test data generation
        var random = new Random();
        return Enumerable.Range(0, dimensions).Select(_ => (float)random.NextDouble()).ToArray();
#pragma warning restore CA5394
    }

    // NOTE: Full workflow tests (text chunking, embedding generation, Qdrant indexing)
    // should be in integration test suite due to DbContext and multi-service complexity.
    // See integration-tests.yml workflow.
}
