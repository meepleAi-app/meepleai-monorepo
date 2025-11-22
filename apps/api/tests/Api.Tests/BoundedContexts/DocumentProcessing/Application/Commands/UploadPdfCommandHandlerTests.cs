using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.Infrastructure;
using Api.Services;
using Api.Services.Pdf;
using Api.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Tests for UploadPdfCommandHandler.
/// Tests PDF upload, validation, storage, and background processing orchestration.
/// NOTE: Highly complex handler (574 lines) with many dependencies and background processing.
/// These tests focus on construction, validation, and core scenarios.
/// TODO: Add comprehensive integration tests for full upload workflow.
/// ISSUE-1500: TEST-002 - Fixed test isolation (fresh context per test)
/// </summary>
public class UploadPdfCommandHandlerTests
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
    private static (Mock<IServiceScopeFactory>, Mock<ILogger<UploadPdfCommandHandler>>, Mock<IPdfTextExtractor>, Mock<IPdfTableExtractor>, Mock<IBackgroundTaskService>, Mock<IAiResponseCacheService>, Mock<IBlobStorageService>, Mock<IPdfUploadQuotaService>) CreateMocks()
    {
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var loggerMock = new Mock<ILogger<UploadPdfCommandHandler>>();
        var pdfTextExtractorMock = new Mock<IPdfTextExtractor>();
        var tableExtractorMock = new Mock<IPdfTableExtractor>();
        var backgroundTaskServiceMock = new Mock<IBackgroundTaskService>();
        var cacheServiceMock = new Mock<IAiResponseCacheService>();
        var blobStorageServiceMock = new Mock<IBlobStorageService>();
        var quotaServiceMock = new Mock<IPdfUploadQuotaService>();

        // Setup quota service to allow unlimited access by default
        quotaServiceMock.Setup(q => q.CheckQuotaAsync(It.IsAny<Guid>(), It.IsAny<UserTier>(), It.IsAny<Role>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PdfUploadQuotaResult.Success(0, int.MaxValue, 0, int.MaxValue, DateTime.MaxValue, DateTime.MaxValue));

        return (scopeFactoryMock, loggerMock, pdfTextExtractorMock, tableExtractorMock, backgroundTaskServiceMock, cacheServiceMock, blobStorageServiceMock, quotaServiceMock);
    }

    #region Construction Tests

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Arrange - fresh resources per test
        using var context = CreateFreshDbContext();
        var (scopeFactoryMock, loggerMock, pdfTextExtractorMock, tableExtractorMock, backgroundTaskServiceMock, cacheServiceMock, blobStorageServiceMock, quotaServiceMock) = CreateMocks();

        // Act
        var handler = new UploadPdfCommandHandler(
            context,
            scopeFactoryMock.Object,
            loggerMock.Object,
            pdfTextExtractorMock.Object,
            tableExtractorMock.Object,
            backgroundTaskServiceMock.Object,
            cacheServiceMock.Object,
            blobStorageServiceMock.Object,
            quotaServiceMock.Object);

        // Assert
        Assert.NotNull(handler);
    }

    [Fact]
    public void Constructor_WithNullDbContext_ThrowsArgumentNullException()
    {
        // Arrange - fresh resources per test
        var (scopeFactoryMock, loggerMock, pdfTextExtractorMock, tableExtractorMock, backgroundTaskServiceMock, cacheServiceMock, blobStorageServiceMock, quotaServiceMock) = CreateMocks();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new UploadPdfCommandHandler(
                null!,
                scopeFactoryMock.Object,
                loggerMock.Object,
                pdfTextExtractorMock.Object,
                tableExtractorMock.Object,
                backgroundTaskServiceMock.Object,
                cacheServiceMock.Object,
                blobStorageServiceMock.Object,
                quotaServiceMock.Object));
    }

    [Fact]
    public void Constructor_WithNullScopeFactory_ThrowsArgumentNullException()
    {
        // Arrange - fresh resources per test
        using var context = CreateFreshDbContext();
        var (_, loggerMock, pdfTextExtractorMock, tableExtractorMock, backgroundTaskServiceMock, cacheServiceMock, blobStorageServiceMock, quotaServiceMock) = CreateMocks();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new UploadPdfCommandHandler(
                context,
                null!,
                loggerMock.Object,
                pdfTextExtractorMock.Object,
                tableExtractorMock.Object,
                backgroundTaskServiceMock.Object,
                cacheServiceMock.Object,
                blobStorageServiceMock.Object,
                quotaServiceMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange - fresh resources per test
        using var context = CreateFreshDbContext();
        var (scopeFactoryMock, _, pdfTextExtractorMock, tableExtractorMock, backgroundTaskServiceMock, cacheServiceMock, blobStorageServiceMock, quotaServiceMock) = CreateMocks();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new UploadPdfCommandHandler(
                context,
                scopeFactoryMock.Object,
                null!,
                pdfTextExtractorMock.Object,
                tableExtractorMock.Object,
                backgroundTaskServiceMock.Object,
                cacheServiceMock.Object,
                blobStorageServiceMock.Object,
                quotaServiceMock.Object));
    }

    [Fact]
    public void Constructor_WithNullPdfTextExtractor_ThrowsArgumentNullException()
    {
        // Arrange - fresh resources per test
        using var context = CreateFreshDbContext();
        var (scopeFactoryMock, loggerMock, _, tableExtractorMock, backgroundTaskServiceMock, cacheServiceMock, blobStorageServiceMock, quotaServiceMock) = CreateMocks();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new UploadPdfCommandHandler(
                context,
                scopeFactoryMock.Object,
                loggerMock.Object,
                null!,
                tableExtractorMock.Object,
                backgroundTaskServiceMock.Object,
                cacheServiceMock.Object,
                blobStorageServiceMock.Object,
                quotaServiceMock.Object));
    }

    [Fact]
    public void Constructor_WithNullTableExtractor_ThrowsArgumentNullException()
    {
        // Arrange - fresh resources per test
        using var context = CreateFreshDbContext();
        var (scopeFactoryMock, loggerMock, pdfTextExtractorMock, _, backgroundTaskServiceMock, cacheServiceMock, blobStorageServiceMock, quotaServiceMock) = CreateMocks();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new UploadPdfCommandHandler(
                context,
                scopeFactoryMock.Object,
                loggerMock.Object,
                pdfTextExtractorMock.Object,
                null!,
                backgroundTaskServiceMock.Object,
                cacheServiceMock.Object,
                blobStorageServiceMock.Object,
                quotaServiceMock.Object));
    }

    [Fact]
    public void Constructor_WithNullBackgroundTaskService_ThrowsArgumentNullException()
    {
        // Arrange - fresh resources per test
        using var context = CreateFreshDbContext();
        var (scopeFactoryMock, loggerMock, pdfTextExtractorMock, tableExtractorMock, _, cacheServiceMock, blobStorageServiceMock, quotaServiceMock) = CreateMocks();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new UploadPdfCommandHandler(
                context,
                scopeFactoryMock.Object,
                loggerMock.Object,
                pdfTextExtractorMock.Object,
                tableExtractorMock.Object,
                null!,
                cacheServiceMock.Object,
                blobStorageServiceMock.Object,
                quotaServiceMock.Object));
    }

    [Fact]
    public void Constructor_WithNullCacheService_ThrowsArgumentNullException()
    {
        // Arrange - fresh resources per test
        using var context = CreateFreshDbContext();
        var (scopeFactoryMock, loggerMock, pdfTextExtractorMock, tableExtractorMock, backgroundTaskServiceMock, _, blobStorageServiceMock, quotaServiceMock) = CreateMocks();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new UploadPdfCommandHandler(
                context,
                scopeFactoryMock.Object,
                loggerMock.Object,
                pdfTextExtractorMock.Object,
                tableExtractorMock.Object,
                backgroundTaskServiceMock.Object,
                null!,
                blobStorageServiceMock.Object,
                quotaServiceMock.Object));
    }

    [Fact]
    public void Constructor_WithNullBlobStorageService_ThrowsArgumentNullException()
    {
        // Arrange - fresh resources per test
        using var context = CreateFreshDbContext();
        var (scopeFactoryMock, loggerMock, pdfTextExtractorMock, tableExtractorMock, backgroundTaskServiceMock, cacheServiceMock, _, quotaServiceMock) = CreateMocks();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new UploadPdfCommandHandler(
                context,
                scopeFactoryMock.Object,
                loggerMock.Object,
                pdfTextExtractorMock.Object,
                tableExtractorMock.Object,
                backgroundTaskServiceMock.Object,
                cacheServiceMock.Object,
                null!,
                quotaServiceMock.Object));
    }

    [Fact]
    public void Constructor_WithNullQuotaService_ThrowsArgumentNullException()
    {
        // Arrange - fresh resources per test
        using var context = CreateFreshDbContext();
        var (scopeFactoryMock, loggerMock, pdfTextExtractorMock, tableExtractorMock, backgroundTaskServiceMock, cacheServiceMock, blobStorageServiceMock, _) = CreateMocks();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new UploadPdfCommandHandler(
                context,
                scopeFactoryMock.Object,
                loggerMock.Object,
                pdfTextExtractorMock.Object,
                tableExtractorMock.Object,
                backgroundTaskServiceMock.Object,
                cacheServiceMock.Object,
                blobStorageServiceMock.Object,
                null!));
    }

    #endregion

    #region Command Tests

    [Fact]
    public void UploadPdfCommand_ConstructsCorrectly()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid();
        var formFileMock = new Mock<IFormFile>();

        // Act
        var command = new UploadPdfCommand(
            GameId: gameId,
            UserId: userId,
            File: formFileMock.Object);

        // Assert
        Assert.Equal(gameId, command.GameId);
        Assert.Equal(userId, command.UserId);
        Assert.Equal(formFileMock.Object, command.File);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public void UploadPdfCommand_WithEmptyGameId_AllowsConstruction()
    {
        // Arrange
        var formFileMock = new Mock<IFormFile>();
        var command = new UploadPdfCommand(
            GameId: string.Empty, // Will be validated by handler
            UserId: Guid.NewGuid(),
            File: formFileMock.Object);

        // Assert
        Assert.NotNull(command);
        Assert.Empty(command.GameId);
    }

    [Fact]
    public void UploadPdfCommand_WithEmptyUserId_AllowsConstruction()
    {
        // Arrange
        var formFileMock = new Mock<IFormFile>();
        var command = new UploadPdfCommand(
            GameId: Guid.NewGuid().ToString(),
            UserId: Guid.Empty, // Will be validated by handler
            File: formFileMock.Object);

        // Assert
        Assert.NotNull(command);
        Assert.Equal(Guid.Empty, command.UserId);
    }

    #endregion

    // NOTE: Full workflow tests (file validation, blob storage upload, background processing,
    // PDF extraction, indexing, error handling, progress tracking, cache invalidation)
    // should be in integration test suite due to complex dependencies and async background work.
    //
    // Key scenarios requiring integration tests:
    // 1. Successful upload → storage → extraction → indexing pipeline
    // 2. File size validation (exceeds MaxFileSizeBytes)
    // 3. Invalid file format validation
    // 4. Duplicate file handling
    // 5. Background processing failure scenarios
    // 6. Progress tracking updates
    // 7. Cache invalidation on upload
    // 8. Blob storage upload failures
    // 9. PDF extraction failures (3-stage fallback)
    // 10. Vector indexing failures
    //
    // See integration-tests.yml workflow for comprehensive testing.
}
