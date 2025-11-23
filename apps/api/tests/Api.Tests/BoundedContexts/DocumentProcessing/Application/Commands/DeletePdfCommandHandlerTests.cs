using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.Infrastructure;
using Api.Services;
using Api.Services.Pdf;
using Api.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Tests for DeletePdfCommandHandler.
/// Tests PDF deletion with cascade cleanup (document, vectors, blob storage, cache).
/// NOTE: Complex handler with many dependencies - focused on construction and error handling.
/// RESOLVED: Issue #1690 - Integration tests added in DeletePdfIntegrationTests.cs.
/// ISSUE-1500: TEST-002 - Fixed test isolation (fresh context per test)
/// </summary>
public class DeletePdfCommandHandlerTests
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
    private static (Mock<IServiceScopeFactory>, Mock<IBlobStorageService>, Mock<IAiResponseCacheService>, Mock<ILogger<DeletePdfCommandHandler>>) CreateMocks()
    {
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var blobStorageServiceMock = new Mock<IBlobStorageService>();
        var cacheServiceMock = new Mock<IAiResponseCacheService>();
        var loggerMock = new Mock<ILogger<DeletePdfCommandHandler>>();

        return (scopeFactoryMock, blobStorageServiceMock, cacheServiceMock, loggerMock);
    }

    #region Construction Tests

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Arrange - fresh context per test
        using var context = CreateFreshDbContext();
        var (scopeFactoryMock, blobStorageServiceMock, cacheServiceMock, loggerMock) = CreateMocks();

        // Act
        var handler = new DeletePdfCommandHandler(
            context,
            scopeFactoryMock.Object,
            blobStorageServiceMock.Object,
            cacheServiceMock.Object,
            loggerMock.Object);

        // Assert
        Assert.NotNull(handler);
    }

    [Fact]
    public void Constructor_WithNullDbContext_ThrowsArgumentNullException()
    {
        // Arrange - fresh mocks per test
        var (scopeFactoryMock, blobStorageServiceMock, cacheServiceMock, loggerMock) = CreateMocks();

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new DeletePdfCommandHandler(
                null!,
                scopeFactoryMock.Object,
                blobStorageServiceMock.Object,
                cacheServiceMock.Object,
                loggerMock.Object));

        Assert.Equal("db", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullScopeFactory_ThrowsArgumentNullException()
    {
        // Arrange - fresh resources per test
        using var context = CreateFreshDbContext();
        var (_, blobStorageServiceMock, cacheServiceMock, loggerMock) = CreateMocks();

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new DeletePdfCommandHandler(
                context,
                null!,
                blobStorageServiceMock.Object,
                cacheServiceMock.Object,
                loggerMock.Object));

        Assert.Equal("scopeFactory", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullBlobStorageService_ThrowsArgumentNullException()
    {
        // Arrange - fresh resources per test
        using var context = CreateFreshDbContext();
        var (scopeFactoryMock, _, cacheServiceMock, loggerMock) = CreateMocks();

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new DeletePdfCommandHandler(
                context,
                scopeFactoryMock.Object,
                null!,
                cacheServiceMock.Object,
                loggerMock.Object));

        Assert.Equal("blobStorageService", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullCacheService_ThrowsArgumentNullException()
    {
        // Arrange - fresh resources per test
        using var context = CreateFreshDbContext();
        var (scopeFactoryMock, blobStorageServiceMock, _, loggerMock) = CreateMocks();

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new DeletePdfCommandHandler(
                context,
                scopeFactoryMock.Object,
                blobStorageServiceMock.Object,
                null!,
                loggerMock.Object));

        Assert.Equal("cacheService", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange - fresh resources per test
        using var context = CreateFreshDbContext();
        var (scopeFactoryMock, blobStorageServiceMock, cacheServiceMock, _) = CreateMocks();

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new DeletePdfCommandHandler(
                context,
                scopeFactoryMock.Object,
                blobStorageServiceMock.Object,
                cacheServiceMock.Object,
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
