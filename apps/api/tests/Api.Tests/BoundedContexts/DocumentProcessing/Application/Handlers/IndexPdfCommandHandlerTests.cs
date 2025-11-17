using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Handlers;
using Api.Infrastructure;
using Api.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Handlers;

/// <summary>
/// Tests for IndexPdfCommandHandler.
/// Tests PDF text indexing workflow (chunking, embedding, Qdrant indexing).
/// NOTE: Complex orchestrator with many dependencies - focused on construction and validation.
/// TODO: Add integration tests for full indexing workflow.
/// </summary>
public class IndexPdfCommandHandlerTests
{
    private readonly Mock<MeepleAiDbContext> _dbContextMock;
    private readonly Mock<ITextChunkingService> _chunkingServiceMock;
    private readonly Mock<IEmbeddingService> _embeddingServiceMock;
    private readonly Mock<IQdrantService> _qdrantServiceMock;
    private readonly Mock<ILogger<IndexPdfCommandHandler>> _loggerMock;

    public IndexPdfCommandHandlerTests()
    {
        _dbContextMock = new Mock<MeepleAiDbContext>();
        _chunkingServiceMock = new Mock<ITextChunkingService>();
        _embeddingServiceMock = new Mock<IEmbeddingService>();
        _qdrantServiceMock = new Mock<IQdrantService>();
        _loggerMock = new Mock<ILogger<IndexPdfCommandHandler>>();
    }

    #region Construction Tests

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Act
        var handler = new IndexPdfCommandHandler(
            _dbContextMock.Object,
            _chunkingServiceMock.Object,
            _embeddingServiceMock.Object,
            _qdrantServiceMock.Object,
            _loggerMock.Object);

        // Assert
        Assert.NotNull(handler);
    }

    [Fact]
    public void Constructor_WithTimeProvider_CreatesInstance()
    {
        // Arrange
        var timeProvider = TimeProvider.System;

        // Act
        var handler = new IndexPdfCommandHandler(
            _dbContextMock.Object,
            _chunkingServiceMock.Object,
            _embeddingServiceMock.Object,
            _qdrantServiceMock.Object,
            _loggerMock.Object,
            timeProvider);

        // Assert
        Assert.NotNull(handler);
    }

    [Fact]
    public void Constructor_WithNullTimeProvider_UsesSystemTimeProvider()
    {
        // Act
        var handler = new IndexPdfCommandHandler(
            _dbContextMock.Object,
            _chunkingServiceMock.Object,
            _embeddingServiceMock.Object,
            _qdrantServiceMock.Object,
            _loggerMock.Object,
            null);

        // Assert
        Assert.NotNull(handler);
    }

    #endregion

    #region Command Tests

    [Fact]
    public void IndexPdfCommand_ConstructsCorrectly()
    {
        // Arrange
        var pdfId = Guid.NewGuid().ToString();

        // Act
        var command = new IndexPdfCommand(pdfId);

        // Assert
        Assert.Equal(pdfId, command.PdfId);
    }

    #endregion

    #region Result DTO Tests

    [Fact]
    public void IndexingResultDto_CreateSuccess_ConstructsCorrectly()
    {
        // Arrange
        var chunkCount = 42;
        var vectorCount = 42;

        // Act
        var result = IndexingResultDto.CreateSuccess(chunkCount, vectorCount);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(chunkCount, result.ChunksCreated);
        Assert.Equal(vectorCount, result.VectorsIndexed);
        Assert.Null(result.ErrorMessage);
        Assert.Null(result.ErrorCode);
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
        Assert.False(result.Success);
        Assert.Equal(0, result.ChunksCreated);
        Assert.Equal(0, result.VectorsIndexed);
        Assert.Equal(errorMessage, result.ErrorMessage);
        Assert.Equal(errorCode, result.ErrorCode);
    }

    [Fact]
    public void IndexingResultDto_CreateFailure_WithTextExtractionRequired()
    {
        // Act
        var result = IndexingResultDto.CreateFailure(
            "Text extraction required",
            PdfIndexingErrorCode.TextExtractionRequired);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(PdfIndexingErrorCode.TextExtractionRequired, result.ErrorCode);
    }

    [Fact]
    public void IndexingResultDto_CreateFailure_WithChunkingFailure()
    {
        // Act
        var result = IndexingResultDto.CreateFailure(
            "Chunking failed",
            PdfIndexingErrorCode.ChunkingFailed);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(PdfIndexingErrorCode.ChunkingFailed, result.ErrorCode);
    }

    [Fact]
    public void IndexingResultDto_CreateFailure_WithEmbeddingFailure()
    {
        // Act
        var result = IndexingResultDto.CreateFailure(
            "Embedding generation failed",
            PdfIndexingErrorCode.EmbeddingFailed);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(PdfIndexingErrorCode.EmbeddingFailed, result.ErrorCode);
    }

    [Fact]
    public void IndexingResultDto_CreateFailure_WithQdrantIndexingFailure()
    {
        // Act
        var result = IndexingResultDto.CreateFailure(
            "Qdrant indexing failed",
            PdfIndexingErrorCode.QdrantIndexingFailed);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(PdfIndexingErrorCode.QdrantIndexingFailed, result.ErrorCode);
    }

    #endregion

    #region Error Code Tests

    [Theory]
    [InlineData(PdfIndexingErrorCode.PdfNotFound)]
    [InlineData(PdfIndexingErrorCode.TextExtractionRequired)]
    [InlineData(PdfIndexingErrorCode.ChunkingFailed)]
    [InlineData(PdfIndexingErrorCode.EmbeddingFailed)]
    [InlineData(PdfIndexingErrorCode.QdrantIndexingFailed)]
    [InlineData(PdfIndexingErrorCode.UnknownError)]
    public void PdfIndexingErrorCode_AllValuesAreValid(PdfIndexingErrorCode errorCode)
    {
        // Assert
        Assert.True(Enum.IsDefined(typeof(PdfIndexingErrorCode), errorCode));
    }

    #endregion

    // NOTE: Full workflow tests (text chunking, embedding generation, Qdrant indexing)
    // should be in integration test suite due to DbContext and multi-service complexity.
    // See integration-tests.yml workflow.
}
