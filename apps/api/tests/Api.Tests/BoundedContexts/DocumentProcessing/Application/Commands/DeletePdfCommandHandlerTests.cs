using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.Infrastructure;
using Api.Services;
using Api.Services.Pdf;
using Api.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Tests for DeletePdfCommandHandler.
/// Tests PDF deletion with cascade cleanup (document, vectors, blob storage, cache).
/// ISSUE-1818: Migrated to FluentAssertions for improved readability.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class DeletePdfCommandHandlerTests
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
    private static (Mock<IBlobStorageService>, Mock<IAiResponseCacheService>, Mock<ILogger<DeletePdfCommandHandler>>) CreateMocks()
    {
        var blobStorageServiceMock = new Mock<IBlobStorageService>();
        var cacheServiceMock = new Mock<IAiResponseCacheService>();
        var loggerMock = new Mock<ILogger<DeletePdfCommandHandler>>();

        return (blobStorageServiceMock, cacheServiceMock, loggerMock);
    }
    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Arrange - fresh context per test
        using var context = CreateFreshDbContext();
        var (blobStorageServiceMock, cacheServiceMock, loggerMock) = CreateMocks();

        // Act
        var handler = new DeletePdfCommandHandler(
            context,
            blobStorageServiceMock.Object,
            cacheServiceMock.Object,
            loggerMock.Object);

        // Assert
        handler.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullDbContext_ThrowsArgumentNullException()
    {
        // Arrange - fresh mocks per test
        var (blobStorageServiceMock, cacheServiceMock, loggerMock) = CreateMocks();

        // Act
        Action act = () => new DeletePdfCommandHandler(
                null!,
                blobStorageServiceMock.Object,
                cacheServiceMock.Object,
                loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("db");
    }

    [Fact]
    public void Constructor_WithNullBlobStorageService_ThrowsArgumentNullException()
    {
        // Arrange - fresh resources per test
        var (_, cacheServiceMock, loggerMock) = CreateMocks();

        // Act
        Action act = () => new DeletePdfCommandHandler(
                context,
                null!,
                cacheServiceMock.Object,
                loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("blobStorageService");
    }

    [Fact]
    public void Constructor_WithNullCacheService_ThrowsArgumentNullException()
    {
        // Arrange - fresh resources per test
        var (blobStorageServiceMock, _, loggerMock) = CreateMocks();

        // Act
        Action act = () => new DeletePdfCommandHandler(
                context,
                blobStorageServiceMock.Object,
                null!,
                loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("cacheService");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange - fresh resources per test
        var (blobStorageServiceMock, cacheServiceMock, _) = CreateMocks();

        // Act
        Action act = () => new DeletePdfCommandHandler(
                context,
                blobStorageServiceMock.Object,
                cacheServiceMock.Object,
                null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }
    [Fact]
    public void DeletePdfCommand_ConstructsCorrectly()
    {
        // Arrange
        var pdfId = Guid.NewGuid().ToString();

        // Act
        var command = new DeletePdfCommand(pdfId);

        // Assert
        command.PdfId.Should().Be(pdfId);
    }
    [Fact]
    public void PdfDeleteResult_WithSuccess_ConstructsCorrectly()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();

        // Act
        var result = new PdfDeleteResult(true, "PDF deleted successfully", gameId);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Be("PDF deleted successfully");
        result.GameId.Should().Be(gameId);
    }

    [Fact]
    public void PdfDeleteResult_WithFailure_ConstructsCorrectly()
    {
        // Act
        var result = new PdfDeleteResult(false, "PDF not found", null);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("PDF not found");
        result.GameId.Should().BeNull();
    }
    // NOTE: Full workflow tests (cascade deletion, blob storage cleanup, vector deletion)
    // should be in integration test suite due to DbContext and multi-service complexity.
    // See integration-tests.yml workflow.
}
