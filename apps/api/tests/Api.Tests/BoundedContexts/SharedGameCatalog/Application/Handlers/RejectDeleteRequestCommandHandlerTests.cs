using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class RejectDeleteRequestCommandHandlerTests
{
    private readonly Mock<ISharedGameDeleteRequestRepository> _deleteRequestRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<RejectDeleteRequestCommandHandler>> _loggerMock;
    private readonly RejectDeleteRequestCommandHandler _handler;

    public RejectDeleteRequestCommandHandlerTests()
    {
        _deleteRequestRepositoryMock = new Mock<ISharedGameDeleteRequestRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<RejectDeleteRequestCommandHandler>>();
        _handler = new RejectDeleteRequestCommandHandler(
            _deleteRequestRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_RejectsRequestSuccessfully()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var rejectedBy = Guid.NewGuid();
        var command = new RejectDeleteRequestCommand(
            RequestId: requestId,
            RejectedBy: rejectedBy,
            Reason: "Game is not a duplicate");

        var deleteRequest = SharedGameDeleteRequest.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Suspected duplicate");

        _deleteRequestRepositoryMock
            .Setup(r => r.GetByIdAsync(requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(deleteRequest);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(MediatR.Unit.Value);
        _deleteRequestRepositoryMock.Verify(r => r.Update(deleteRequest), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentRequest_ThrowsInvalidOperationException()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var command = new RejectDeleteRequestCommand(
            RequestId: requestId,
            RejectedBy: Guid.NewGuid(),
            Reason: "Test reason");

        _deleteRequestRepositoryMock
            .Setup(r => r.GetByIdAsync(requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGameDeleteRequest?)null);

        // Act & Assert
        var act = () =>
            _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<InvalidOperationException>();

        _deleteRequestRepositoryMock.Verify(r => r.Update(It.IsAny<SharedGameDeleteRequest>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}