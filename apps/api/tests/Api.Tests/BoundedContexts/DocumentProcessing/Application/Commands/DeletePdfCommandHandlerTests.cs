using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.Infrastructure;
using Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Tests for DeletePdfCommandHandler.
/// Tests PDF deletion with cascade cleanup (document, vectors, blob storage, cache).
/// NOTE: Complex handler with many dependencies - focused on construction and error handling.
/// TODO: Add integration tests for full deletion workflow.
/// </summary>
public class DeletePdfCommandHandlerTests
{
    private readonly Mock<MeepleAiDbContext> _dbContextMock;
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
    private readonly Mock<IBlobStorageService> _blobStorageServiceMock;
    private readonly Mock<IAiResponseCacheService> _cacheServiceMock;
    private readonly Mock<ILogger<DeletePdfCommandHandler>> _loggerMock;

    public DeletePdfCommandHandlerTests()
    {
        _dbContextMock = new Mock<MeepleAiDbContext>();
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _blobStorageServiceMock = new Mock<IBlobStorageService>();
        _cacheServiceMock = new Mock<IAiResponseCacheService>();
        _loggerMock = new Mock<ILogger<DeletePdfCommandHandler>>();
    }

    #region Construction Tests

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Act
        var handler = new DeletePdfCommandHandler(
            _dbContextMock.Object,
            _scopeFactoryMock.Object,
            _blobStorageServiceMock.Object,
            _cacheServiceMock.Object,
            _loggerMock.Object);

        // Assert
        Assert.NotNull(handler);
    }

    [Fact]
    public void Constructor_WithNullDbContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new DeletePdfCommandHandler(
                null!,
                _scopeFactoryMock.Object,
                _blobStorageServiceMock.Object,
                _cacheServiceMock.Object,
                _loggerMock.Object));

        Assert.Equal("db", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullScopeFactory_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new DeletePdfCommandHandler(
                _dbContextMock.Object,
                null!,
                _blobStorageServiceMock.Object,
                _cacheServiceMock.Object,
                _loggerMock.Object));

        Assert.Equal("scopeFactory", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullBlobStorageService_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new DeletePdfCommandHandler(
                _dbContextMock.Object,
                _scopeFactoryMock.Object,
                null!,
                _cacheServiceMock.Object,
                _loggerMock.Object));

        Assert.Equal("blobStorageService", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullCacheService_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new DeletePdfCommandHandler(
                _dbContextMock.Object,
                _scopeFactoryMock.Object,
                _blobStorageServiceMock.Object,
                null!,
                _loggerMock.Object));

        Assert.Equal("cacheService", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new DeletePdfCommandHandler(
                _dbContextMock.Object,
                _scopeFactoryMock.Object,
                _blobStorageServiceMock.Object,
                _cacheServiceMock.Object,
                null!));

        Assert.Equal("logger", exception.ParamName);
    }

    #endregion

    #region Command Tests

    [Fact]
    public void DeletePdfCommand_ConstructsCorrectly()
    {
        // Arrange
        var pdfId = Guid.NewGuid().ToString();

        // Act
        var command = new DeletePdfCommand(pdfId);

        // Assert
        Assert.Equal(pdfId, command.PdfId);
    }

    #endregion

    #region Result Tests

    [Fact]
    public void PdfDeleteResult_WithSuccess_ConstructsCorrectly()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();

        // Act
        var result = new PdfDeleteResult(true, "PDF deleted successfully", gameId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("PDF deleted successfully", result.Message);
        Assert.Equal(gameId, result.GameId);
    }

    [Fact]
    public void PdfDeleteResult_WithFailure_ConstructsCorrectly()
    {
        // Act
        var result = new PdfDeleteResult(false, "PDF not found", null);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("PDF not found", result.Message);
        Assert.Null(result.GameId);
    }

    #endregion

    // NOTE: Full workflow tests (cascade deletion, blob storage cleanup, vector deletion)
    // should be in integration test suite due to DbContext and multi-service complexity.
    // See integration-tests.yml workflow.
}
