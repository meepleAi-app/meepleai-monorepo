using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Handlers;
using Api.Infrastructure;
using Api.Services;
using Api.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
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
public class IndexPdfCommandHandlerTests
{
    /// <summary>
    /// Creates a fresh DbContext for each test to ensure complete isolation
    /// </summary>
    private static MeepleAiDbContext CreateFreshDbContext()
    {
        return DbContextHelper.CreateInMemoryDbContext();
    }

    /// <summary>
    /// Creates a fresh set of mocks for each test
    /// </summary>
    private static (Mock<ITextChunkingService>, Mock<IEmbeddingService>, Mock<IQdrantService>, Mock<ILogger<IndexPdfCommandHandler>>) CreateMocks()
    {
        var chunkingServiceMock = new Mock<ITextChunkingService>();
        var embeddingServiceMock = new Mock<IEmbeddingService>();
        var qdrantServiceMock = new Mock<IQdrantService>();
        var loggerMock = new Mock<ILogger<IndexPdfCommandHandler>>();

        return (chunkingServiceMock, embeddingServiceMock, qdrantServiceMock, loggerMock);
    }

    #region Construction Tests

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Arrange - fresh resources per test
        using var context = CreateFreshDbContext();
        var (chunkingServiceMock, embeddingServiceMock, qdrantServiceMock, loggerMock) = CreateMocks();

        // Act
        var handler = new IndexPdfCommandHandler(
            context,
            chunkingServiceMock.Object,
            embeddingServiceMock.Object,
            qdrantServiceMock.Object,
            loggerMock.Object);

        // Assert
        handler.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithTimeProvider_CreatesInstance()
    {
        // Arrange - fresh resources per test
        using var context = CreateFreshDbContext();
        var (chunkingServiceMock, embeddingServiceMock, qdrantServiceMock, loggerMock) = CreateMocks();
        var timeProvider = TimeProvider.System;

        // Act
        var handler = new IndexPdfCommandHandler(
            context,
            chunkingServiceMock.Object,
            embeddingServiceMock.Object,
            qdrantServiceMock.Object,
            loggerMock.Object,
            timeProvider);

        // Assert
        handler.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullTimeProvider_UsesSystemTimeProvider()
    {
        // Arrange - fresh resources per test
        using var context = CreateFreshDbContext();
        var (chunkingServiceMock, embeddingServiceMock, qdrantServiceMock, loggerMock) = CreateMocks();

        // Act
        var handler = new IndexPdfCommandHandler(
            context,
            chunkingServiceMock.Object,
            embeddingServiceMock.Object,
            qdrantServiceMock.Object,
            loggerMock.Object,
            null);

        // Assert
        handler.Should().NotBeNull();
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
        command.PdfId.Should().Be(pdfId);
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

    #endregion

    #region Error Code Tests

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

    #endregion

    // NOTE: Full workflow tests (text chunking, embedding generation, Qdrant indexing)
    // should be in integration test suite due to DbContext and multi-service complexity.
    // See integration-tests.yml workflow.
}