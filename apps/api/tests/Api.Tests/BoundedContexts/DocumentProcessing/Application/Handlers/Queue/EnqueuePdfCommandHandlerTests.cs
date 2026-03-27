using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Handlers.Queue;

[Trait("Category", TestCategories.Unit)]
public sealed class EnqueuePdfCommandHandlerTests
{
    private readonly Mock<IProcessingJobRepository> _jobRepositoryMock = new();
    private readonly Mock<IPdfDocumentRepository> _pdfRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly EnqueuePdfCommandHandler _handler;

    public EnqueuePdfCommandHandlerTests()
    {
        _handler = new EnqueuePdfCommandHandler(
            _jobRepositoryMock.Object,
            _pdfRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _timeProvider);
    }

    [Fact]
    public async Task Handle_ValidCommand_CreatesJobAndReturnsId()
    {
        // Arrange
        var pdfId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = new EnqueuePdfCommand(pdfId, userId, 5);

        _pdfRepositoryMock
            .Setup(r => r.ExistsAsync(pdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _jobRepositoryMock
            .Setup(r => r.ExistsByPdfDocumentIdAsync(pdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _jobRepositoryMock
            .Setup(r => r.CountByStatusAsync(JobStatus.Queued, It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);
        _jobRepositoryMock
            .Setup(r => r.CountByStatusAsync(JobStatus.Processing, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeEmpty();
        _jobRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<ProcessingJob>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_PdfNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var command = new EnqueuePdfCommand(Guid.NewGuid(), Guid.NewGuid());
        _pdfRepositoryMock
            .Setup(r => r.ExistsAsync(command.PdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act & Assert
        await FluentActions.Invoking(() => _handler.Handle(command, CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_PdfAlreadyInQueue_ThrowsConflictException()
    {
        // Arrange
        var pdfId = Guid.NewGuid();
        var command = new EnqueuePdfCommand(pdfId, Guid.NewGuid());

        _pdfRepositoryMock
            .Setup(r => r.ExistsAsync(pdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _jobRepositoryMock
            .Setup(r => r.ExistsByPdfDocumentIdAsync(pdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act & Assert
        await FluentActions.Invoking(() => _handler.Handle(command, CancellationToken.None))
            .Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_QueueFull_ThrowsInvalidOperationException()
    {
        // Arrange
        var command = new EnqueuePdfCommand(Guid.NewGuid(), Guid.NewGuid());

        _pdfRepositoryMock
            .Setup(r => r.ExistsAsync(command.PdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _jobRepositoryMock
            .Setup(r => r.ExistsByPdfDocumentIdAsync(command.PdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _jobRepositoryMock
            .Setup(r => r.CountByStatusAsync(JobStatus.Queued, It.IsAny<CancellationToken>()))
            .ReturnsAsync(90);
        _jobRepositoryMock
            .Setup(r => r.CountByStatusAsync(JobStatus.Processing, It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);

        // Act & Assert
        await FluentActions.Invoking(() => _handler.Handle(command, CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Queue is full*");
    }
}
