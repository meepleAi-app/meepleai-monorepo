using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.BoundedContexts.DocumentProcessing.TestHelpers;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Unit tests for AcceptCopyrightDisclaimerCommandHandler.
/// Issue #5446: Copyright disclaimer acceptance required before RAG processing.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public class AcceptCopyrightDisclaimerCommandHandlerTests
{
    private readonly Mock<IPdfDocumentRepository> _mockPdfRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ILogger<AcceptCopyrightDisclaimerCommandHandler>> _mockLogger;
    private readonly AcceptCopyrightDisclaimerCommandHandler _handler;

    public AcceptCopyrightDisclaimerCommandHandlerTests()
    {
        _mockPdfRepository = new Mock<IPdfDocumentRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockLogger = new Mock<ILogger<AcceptCopyrightDisclaimerCommandHandler>>();

        _handler = new AcceptCopyrightDisclaimerCommandHandler(
            _mockPdfRepository.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithValidOwnerAndNewDisclaimer_AcceptsAndReturnsSuccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pdf = new PdfDocumentBuilder()
            .WithUploadedBy(userId)
            .Build();

        _mockPdfRepository
            .Setup(r => r.GetByIdAsync(pdf.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdf);

        var command = new AcceptCopyrightDisclaimerCommand(userId, pdf.Id);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.PdfDocumentId.Should().Be(pdf.Id);
        pdf.HasAcceptedDisclaimer.Should().BeTrue();
        pdf.CopyrightDisclaimerAcceptedBy.Should().Be(userId);
        pdf.CopyrightDisclaimerAcceptedAt.Should().NotBeNull();

        _mockPdfRepository.Verify(r => r.UpdateAsync(pdf, It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentPdf_ThrowsNotFoundException()
    {
        // Arrange
        var pdfId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _mockPdfRepository
            .Setup(r => r.GetByIdAsync(pdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Api.BoundedContexts.DocumentProcessing.Domain.Entities.PdfDocument?)null);

        var command = new AcceptCopyrightDisclaimerCommand(userId, pdfId);

        // Act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"*{pdfId}*");

        _mockPdfRepository.Verify(r => r.UpdateAsync(It.IsAny<Api.BoundedContexts.DocumentProcessing.Domain.Entities.PdfDocument>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotOwnPdf_ThrowsForbiddenException()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var requestingUserId = Guid.NewGuid();

        var pdf = new PdfDocumentBuilder()
            .WithUploadedBy(ownerId)
            .Build();

        _mockPdfRepository
            .Setup(r => r.GetByIdAsync(pdf.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdf);

        var command = new AcceptCopyrightDisclaimerCommand(requestingUserId, pdf.Id);

        // Act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();

        _mockPdfRepository.Verify(r => r.UpdateAsync(It.IsAny<Api.BoundedContexts.DocumentProcessing.Domain.Entities.PdfDocument>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenDisclaimerAlreadyAccepted_ThrowsConflictException()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var pdf = new PdfDocumentBuilder()
            .WithUploadedBy(userId)
            .Build();

        // Accept disclaimer first
        pdf.AcceptCopyrightDisclaimer(userId);

        _mockPdfRepository
            .Setup(r => r.GetByIdAsync(pdf.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdf);

        var command = new AcceptCopyrightDisclaimerCommand(userId, pdf.Id);

        // Act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();

        _mockPdfRepository.Verify(r => r.UpdateAsync(It.IsAny<Api.BoundedContexts.DocumentProcessing.Domain.Entities.PdfDocument>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act
        var act = async () => await _handler.Handle(null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
