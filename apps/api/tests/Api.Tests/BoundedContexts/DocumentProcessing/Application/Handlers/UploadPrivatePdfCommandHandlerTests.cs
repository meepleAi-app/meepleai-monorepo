using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.Services.Pdf;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using AuthRole = Api.SharedKernel.Domain.ValueObjects.Role;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Handlers;

/// <summary>
/// Unit tests for UploadPrivatePdfCommandHandler.
/// Issue #3479: Private PDF Upload Endpoint
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public class UploadPrivatePdfCommandHandlerTests
{
    private readonly Mock<IUserLibraryRepository> _mockLibraryRepository;
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IPdfDocumentRepository> _mockPdfRepository;
    private readonly Mock<IBlobStorageService> _mockBlobStorageService;
    private readonly Mock<IBackgroundTaskService> _mockBackgroundTaskService;
    private readonly Mock<IPdfUploadQuotaService> _mockQuotaService;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ILogger<UploadPrivatePdfCommandHandler>> _mockLogger;
    private readonly UploadPrivatePdfCommandHandler _handler;

    public UploadPrivatePdfCommandHandlerTests()
    {
        _mockLibraryRepository = new Mock<IUserLibraryRepository>();
        _mockUserRepository = new Mock<IUserRepository>();
        _mockPdfRepository = new Mock<IPdfDocumentRepository>();
        _mockBlobStorageService = new Mock<IBlobStorageService>();
        _mockBackgroundTaskService = new Mock<IBackgroundTaskService>();
        _mockQuotaService = new Mock<IPdfUploadQuotaService>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockLogger = new Mock<ILogger<UploadPrivatePdfCommandHandler>>();

        // Setup default quota mock to allow uploads
        SetupDefaultQuotaMocks();

        _handler = new UploadPrivatePdfCommandHandler(
            _mockLibraryRepository.Object,
            _mockUserRepository.Object,
            _mockPdfRepository.Object,
            _mockBlobStorageService.Object,
            _mockBackgroundTaskService.Object,
            _mockQuotaService.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object);
    }

    private void SetupDefaultQuotaMocks()
    {
        // Default: allow all quota checks
        _mockQuotaService
            .Setup(s => s.CheckPerGameQuotaAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<UserTier>(),
                It.IsAny<AuthRole>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(PerGameQuotaResult.Success(0, 10));

        _mockQuotaService
            .Setup(s => s.CheckQuotaAsync(
                It.IsAny<Guid>(),
                It.IsAny<UserTier>(),
                It.IsAny<AuthRole>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(PdfUploadQuotaResult.Success(0, 20, 0, 100, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(7)));

        // Setup default user mock
        _mockUserRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid userId, CancellationToken _) =>
            {
                var user = new User(
                    userId,
                    new Email("test@example.com"),
                    "TestUser",
                    PasswordHash.Create("TestPassword123!"),
                    AuthRole.User,
                    UserTier.Normal);
                return user;
            });
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLibraryRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new UploadPrivatePdfCommandHandler(
            null!,
            _mockUserRepository.Object,
            _mockPdfRepository.Object,
            _mockBlobStorageService.Object,
            _mockBackgroundTaskService.Object,
            _mockQuotaService.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullUserRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new UploadPrivatePdfCommandHandler(
            _mockLibraryRepository.Object,
            null!,
            _mockPdfRepository.Object,
            _mockBlobStorageService.Object,
            _mockBackgroundTaskService.Object,
            _mockQuotaService.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullPdfRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new UploadPrivatePdfCommandHandler(
            _mockLibraryRepository.Object,
            _mockUserRepository.Object,
            null!,
            _mockBlobStorageService.Object,
            _mockBackgroundTaskService.Object,
            _mockQuotaService.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullBlobStorageService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new UploadPrivatePdfCommandHandler(
            _mockLibraryRepository.Object,
            _mockUserRepository.Object,
            _mockPdfRepository.Object,
            null!,
            _mockBackgroundTaskService.Object,
            _mockQuotaService.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullBackgroundTaskService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new UploadPrivatePdfCommandHandler(
            _mockLibraryRepository.Object,
            _mockUserRepository.Object,
            _mockPdfRepository.Object,
            _mockBlobStorageService.Object,
            null!,
            _mockQuotaService.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullQuotaService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new UploadPrivatePdfCommandHandler(
            _mockLibraryRepository.Object,
            _mockUserRepository.Object,
            _mockPdfRepository.Object,
            _mockBlobStorageService.Object,
            _mockBackgroundTaskService.Object,
            null!,
            _mockUnitOfWork.Object,
            _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullUnitOfWork_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new UploadPrivatePdfCommandHandler(
            _mockLibraryRepository.Object,
            _mockUserRepository.Object,
            _mockPdfRepository.Object,
            _mockBlobStorageService.Object,
            _mockBackgroundTaskService.Object,
            _mockQuotaService.Object,
            null!,
            _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new UploadPrivatePdfCommandHandler(
            _mockLibraryRepository.Object,
            _mockUserRepository.Object,
            _mockPdfRepository.Object,
            _mockBlobStorageService.Object,
            _mockBackgroundTaskService.Object,
            _mockQuotaService.Object,
            _mockUnitOfWork.Object,
            null!));
    }

    #endregion

    #region Null Guard Tests

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, TestContext.Current.CancellationToken));
    }

    #endregion

    #region Library Entry Validation Tests

    [Fact]
    public async Task Handle_WhenLibraryEntryNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var command = new UploadPrivatePdfCommand(userId, entryId, CreateMockPdfFormFile("test.pdf"));

        _mockLibraryRepository
            .Setup(r => r.GetByIdAsync(entryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserLibraryEntry?)null);

        // Act
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await ((Func<Task>)(act)).Should().ThrowAsync<NotFoundException>()).Which;
        exception.Message.Should().Contain(entryId.ToString());

        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotOwnEntry_ThrowsForbiddenException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var command = new UploadPrivatePdfCommand(userId, entryId, CreateMockPdfFormFile("test.pdf"));

        // Entry belongs to otherUserId, not userId
        var libraryEntry = new UserLibraryEntry(entryId, otherUserId, gameId);

        _mockLibraryRepository
            .Setup(r => r.GetByIdAsync(entryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(libraryEntry);

        // Act
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await ((Func<Task>)(act)).Should().ThrowAsync<ForbiddenException>()).Which;
        exception.Message.Should().Contain("permission");

        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region PDF Validation Tests

    [Fact]
    public async Task Handle_WithInvalidPdfStructure_ThrowsValidationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        // Create a file that looks like PDF but doesn't have valid structure
        var invalidPdfContent = "This is not a PDF file"u8.ToArray();
        var command = new UploadPrivatePdfCommand(userId, entryId, CreateMockFormFile("test.pdf", invalidPdfContent));

        var libraryEntry = new UserLibraryEntry(entryId, userId, gameId);

        _mockLibraryRepository
            .Setup(r => r.GetByIdAsync(entryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(libraryEntry);

        // Act
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await ((Func<Task>)(act)).Should().ThrowAsync<ValidationException>()).Which;
        exception.Message.Should().Contain("PDF");

        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithTooSmallFile_ThrowsValidationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        // Create a tiny file (< 50 bytes)
        var tinyContent = "tiny"u8.ToArray();
        var command = new UploadPrivatePdfCommand(userId, entryId, CreateMockFormFile("test.pdf", tinyContent));

        var libraryEntry = new UserLibraryEntry(entryId, userId, gameId);

        _mockLibraryRepository
            .Setup(r => r.GetByIdAsync(entryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(libraryEntry);

        // Act
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await ((Func<Task>)(act)).Should().ThrowAsync<ValidationException>()).Which;
        exception.Message.Should().Contain("too small");

        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Storage Failure Tests

    [Fact]
    public async Task Handle_WhenStorageFails_ThrowsInvalidOperationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var command = new UploadPrivatePdfCommand(userId, entryId, CreateMockPdfFormFile("test.pdf"));

        var libraryEntry = new UserLibraryEntry(entryId, userId, gameId);

        _mockLibraryRepository
            .Setup(r => r.GetByIdAsync(entryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(libraryEntry);

        _mockBlobStorageService
            .Setup(s => s.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobStorageResult(false, null, null, 0, "Storage unavailable"));

        // Act
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await ((Func<Task>)(act)).Should().ThrowAsync<InvalidOperationException>()).Which;
        exception.Message.Should().Contain("Failed to store");

        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Successful Upload Tests

    [Fact]
    public async Task Handle_WithValidCommand_UploadsAndReturnsResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        var command = new UploadPrivatePdfCommand(userId, entryId, CreateMockPdfFormFile("rulebook.pdf"));

        var libraryEntry = new UserLibraryEntry(entryId, userId, gameId);

        _mockLibraryRepository
            .Setup(r => r.GetByIdAsync(entryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(libraryEntry);

        _mockBlobStorageService
            .Setup(s => s.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobStorageResult(true, pdfId.ToString(), $"/storage/{gameId}/{pdfId}/rulebook.pdf", 1024));

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.PdfId.Should().Be(pdfId);
        result.FileName.Should().Be("rulebook.pdf");
        result.FileSize.Should().Be(1024);
        result.Status.Should().Be("processing");
        result.SseStreamUrl.Should().Contain(entryId.ToString());
        result.SseStreamUrl.Should().Contain(userId.ToString());

        // Verify repository interactions
        _mockPdfRepository.Verify(
            r => r.AddAsync(It.IsAny<Api.BoundedContexts.DocumentProcessing.Domain.Entities.PdfDocument>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _mockUnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify background processing was triggered
        _mockBackgroundTaskService.Verify(
            s => s.ExecuteWithCancellation(
                pdfId.ToString(),
                It.IsAny<Func<CancellationToken, Task>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_SanitizesFileName()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();

        // File with potentially dangerous characters in name
        var command = new UploadPrivatePdfCommand(userId, entryId, CreateMockPdfFormFile("../../../etc/passwd.pdf"));

        var libraryEntry = new UserLibraryEntry(entryId, userId, gameId);

        _mockLibraryRepository
            .Setup(r => r.GetByIdAsync(entryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(libraryEntry);

        _mockBlobStorageService
            .Setup(s => s.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobStorageResult(true, pdfId.ToString(), $"/storage/{gameId}/{pdfId}/passwd.pdf", 1024));

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - File name should be sanitized (no path traversal)
        result.Should().NotBeNull();
        result.FileName.Should().NotContain("..");
        result.FileName.Should().NotContain("/");
        result.FileName.Should().NotContain("\\");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a mock IFormFile with valid PDF content.
    /// </summary>
    private static IFormFile CreateMockPdfFormFile(string fileName)
    {
        // Minimal valid PDF content
        var pdfContent = "%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\nxref\n0 2\n0000000000 65535 f \n0000000009 00000 n \ntrailer\n<< /Root 1 0 R /Size 2 >>\nstartxref\n58\n%%EOF"u8.ToArray();
        return CreateMockFormFile(fileName, pdfContent);
    }

    /// <summary>
    /// Creates a mock IFormFile with specified content.
    /// </summary>
    private static IFormFile CreateMockFormFile(string fileName, byte[] content, string contentType = "application/pdf")
    {
        var formFile = new Mock<IFormFile>();
        formFile.Setup(f => f.FileName).Returns(fileName);
        formFile.Setup(f => f.Length).Returns(content.Length);
        formFile.Setup(f => f.ContentType).Returns(contentType);
        formFile.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream(content));
        return formFile.Object;
    }

    #endregion
}
