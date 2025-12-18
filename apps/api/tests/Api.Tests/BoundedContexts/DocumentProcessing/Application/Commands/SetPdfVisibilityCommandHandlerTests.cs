using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Tests for SetPdfVisibilityCommandHandler.
/// Tests setting PDF visibility in the public library.
/// Admin Wizard: Required for public library feature.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class SetPdfVisibilityCommandHandlerTests
{
    /// <summary>
    /// Creates a fresh set of mocks for each test
    /// </summary>
    private static (Mock<IPdfDocumentRepository>, Mock<IUnitOfWork>, Mock<ILogger<SetPdfVisibilityCommandHandler>>) CreateMocks()
    {
        var pdfRepositoryMock = new Mock<IPdfDocumentRepository>();
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var loggerMock = new Mock<ILogger<SetPdfVisibilityCommandHandler>>();

        return (pdfRepositoryMock, unitOfWorkMock, loggerMock);
    }

    /// <summary>
    /// Creates a test PdfDocument with specified visibility
    /// </summary>
    private static PdfDocument CreateTestPdfDocument(Guid? id = null, bool isPublic = false)
    {
        var pdfId = id ?? Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var fileName = new FileName("test-rules.pdf");
        var fileSize = new FileSize(1024 * 1024); // 1MB

        var pdf = new PdfDocument(
            pdfId,
            gameId,
            fileName,
            "/uploads/test-rules.pdf",
            fileSize,
            userId);

        if (isPublic)
        {
            pdf.MakePublic();
        }

        return pdf;
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Arrange
        var (pdfRepositoryMock, unitOfWorkMock, loggerMock) = CreateMocks();

        // Act
        var handler = new SetPdfVisibilityCommandHandler(
            pdfRepositoryMock.Object,
            unitOfWorkMock.Object,
            loggerMock.Object);

        // Assert
        handler.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullPdfRepository_ThrowsArgumentNullException()
    {
        // Arrange
        var (_, unitOfWorkMock, loggerMock) = CreateMocks();

        // Act
        Action act = () => new SetPdfVisibilityCommandHandler(
            null!,
            unitOfWorkMock.Object,
            loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("pdfRepository");
    }

    [Fact]
    public void Constructor_WithNullUnitOfWork_ThrowsArgumentNullException()
    {
        // Arrange
        var (pdfRepositoryMock, _, loggerMock) = CreateMocks();

        // Act
        Action act = () => new SetPdfVisibilityCommandHandler(
            pdfRepositoryMock.Object,
            null!,
            loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("unitOfWork");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var (pdfRepositoryMock, unitOfWorkMock, _) = CreateMocks();

        // Act
        Action act = () => new SetPdfVisibilityCommandHandler(
            pdfRepositoryMock.Object,
            unitOfWorkMock.Object,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    #endregion

    #region Command Tests

    [Fact]
    public void SetPdfVisibilityCommand_ConstructsCorrectly()
    {
        // Arrange
        var pdfId = Guid.NewGuid();
        var isPublic = true;

        // Act
        var command = new SetPdfVisibilityCommand(pdfId, isPublic);

        // Assert
        command.PdfId.Should().Be(pdfId);
        command.IsPublic.Should().BeTrue();
    }

    [Fact]
    public void SetPdfVisibilityCommand_WithFalseIsPublic_ConstructsCorrectly()
    {
        // Arrange
        var pdfId = Guid.NewGuid();

        // Act
        var command = new SetPdfVisibilityCommand(pdfId, false);

        // Assert
        command.PdfId.Should().Be(pdfId);
        command.IsPublic.Should().BeFalse();
    }

    #endregion

    #region Result Tests

    [Fact]
    public void SetPdfVisibilityResult_WithSuccess_ConstructsCorrectly()
    {
        // Arrange
        var pdfId = Guid.NewGuid();

        // Act
        var result = new SetPdfVisibilityResult(true, "PDF is now visible in the public library", pdfId);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Be("PDF is now visible in the public library");
        result.PdfId.Should().Be(pdfId);
    }

    [Fact]
    public void SetPdfVisibilityResult_WithFailure_ConstructsCorrectly()
    {
        // Act
        var result = new SetPdfVisibilityResult(false, "PDF not found", null);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("PDF not found");
        result.PdfId.Should().BeNull();
    }

    #endregion

    #region Handle Tests

    [Fact]
    public async Task Handle_WhenPdfNotFound_ReturnsFailure()
    {
        // Arrange
        var (pdfRepositoryMock, unitOfWorkMock, loggerMock) = CreateMocks();
        var pdfId = Guid.NewGuid();

        pdfRepositoryMock
            .Setup(x => x.GetByIdAsync(pdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PdfDocument?)null);

        var handler = new SetPdfVisibilityCommandHandler(
            pdfRepositoryMock.Object,
            unitOfWorkMock.Object,
            loggerMock.Object);

        var command = new SetPdfVisibilityCommand(pdfId, true);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("PDF not found");
        result.PdfId.Should().BeNull();

        // Verify no updates were attempted
        pdfRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<PdfDocument>(), It.IsAny<CancellationToken>()), Times.Never);
        unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenMakingPublic_ReturnsSuccessAndUpdates()
    {
        // Arrange
        var (pdfRepositoryMock, unitOfWorkMock, loggerMock) = CreateMocks();
        var pdfId = Guid.NewGuid();
        var pdf = CreateTestPdfDocument(pdfId, isPublic: false);

        pdfRepositoryMock
            .Setup(x => x.GetByIdAsync(pdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdf);

        pdfRepositoryMock
            .Setup(x => x.UpdateAsync(It.IsAny<PdfDocument>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        unitOfWorkMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = new SetPdfVisibilityCommandHandler(
            pdfRepositoryMock.Object,
            unitOfWorkMock.Object,
            loggerMock.Object);

        var command = new SetPdfVisibilityCommand(pdfId, true);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Be("PDF is now visible in the public library");
        result.PdfId.Should().Be(pdfId);
        pdf.IsPublic.Should().BeTrue();

        // Verify repository was updated
        pdfRepositoryMock.Verify(x => x.UpdateAsync(pdf, It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenMakingPrivate_ReturnsSuccessAndUpdates()
    {
        // Arrange
        var (pdfRepositoryMock, unitOfWorkMock, loggerMock) = CreateMocks();
        var pdfId = Guid.NewGuid();
        var pdf = CreateTestPdfDocument(pdfId, isPublic: true);

        pdfRepositoryMock
            .Setup(x => x.GetByIdAsync(pdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdf);

        pdfRepositoryMock
            .Setup(x => x.UpdateAsync(It.IsAny<PdfDocument>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        unitOfWorkMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = new SetPdfVisibilityCommandHandler(
            pdfRepositoryMock.Object,
            unitOfWorkMock.Object,
            loggerMock.Object);

        var command = new SetPdfVisibilityCommand(pdfId, false);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Be("PDF is now private");
        result.PdfId.Should().Be(pdfId);
        pdf.IsPublic.Should().BeFalse();

        // Verify repository was updated
        pdfRepositoryMock.Verify(x => x.UpdateAsync(pdf, It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAlreadyPublicAndMakingPublic_StillSucceeds()
    {
        // Arrange
        var (pdfRepositoryMock, unitOfWorkMock, loggerMock) = CreateMocks();
        var pdfId = Guid.NewGuid();
        var pdf = CreateTestPdfDocument(pdfId, isPublic: true);

        pdfRepositoryMock
            .Setup(x => x.GetByIdAsync(pdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdf);

        pdfRepositoryMock
            .Setup(x => x.UpdateAsync(It.IsAny<PdfDocument>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        unitOfWorkMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = new SetPdfVisibilityCommandHandler(
            pdfRepositoryMock.Object,
            unitOfWorkMock.Object,
            loggerMock.Object);

        var command = new SetPdfVisibilityCommand(pdfId, true);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        pdf.IsPublic.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenAlreadyPrivateAndMakingPrivate_StillSucceeds()
    {
        // Arrange
        var (pdfRepositoryMock, unitOfWorkMock, loggerMock) = CreateMocks();
        var pdfId = Guid.NewGuid();
        var pdf = CreateTestPdfDocument(pdfId, isPublic: false);

        pdfRepositoryMock
            .Setup(x => x.GetByIdAsync(pdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdf);

        pdfRepositoryMock
            .Setup(x => x.UpdateAsync(It.IsAny<PdfDocument>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        unitOfWorkMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = new SetPdfVisibilityCommandHandler(
            pdfRepositoryMock.Object,
            unitOfWorkMock.Object,
            loggerMock.Object);

        var command = new SetPdfVisibilityCommand(pdfId, false);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        pdf.IsPublic.Should().BeFalse();
    }

    #endregion

    // NOTE: Integration tests for full workflow with database should be in integration test suite.
    // See Integration/DocumentProcessing/ folder.
}
