using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class RejectShareRequestCommandHandlerTests
{
    private readonly Mock<IShareRequestRepository> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<RejectShareRequestCommandHandler>> _loggerMock;
    private readonly RejectShareRequestCommandHandler _handler;

    private static readonly Guid TestAdminId = Guid.NewGuid();
    private static readonly Guid TestUserId = Guid.NewGuid();
    private static readonly Guid TestGameId = Guid.NewGuid();

    public RejectShareRequestCommandHandlerTests()
    {
        _repositoryMock = new Mock<IShareRequestRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<RejectShareRequestCommandHandler>>();

        _handler = new RejectShareRequestCommandHandler(
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_RejectsShareRequest()
    {
        // Arrange
        var shareRequest = CreateShareRequestInReview(TestAdminId);
        var command = new RejectShareRequestCommand(
            ShareRequestId: shareRequest.Id,
            AdminId: TestAdminId,
            Reason: "This game does not meet our quality standards.");

        _repositoryMock
            .Setup(r => r.GetByIdForUpdateAsync(shareRequest.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);

        ShareRequest? capturedRequest = null;
        _repositoryMock
            .Setup(r => r.Update(It.IsAny<ShareRequest>()))
            .Callback<ShareRequest>(req => capturedRequest = req);

        // Act
        var response = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        response.Should().NotBeNull();
        response.ShareRequestId.Should().Be(shareRequest.Id);
        response.Status.Should().Be(ShareRequestStatus.Rejected);
        response.ResolvedAt.Should().NotBe(default);

        _repositoryMock.Verify(r => r.Update(It.IsAny<ShareRequest>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        capturedRequest.Should().NotBeNull();
        capturedRequest.Status.Should().Be(ShareRequestStatus.Rejected);
        capturedRequest.AdminFeedback.Should().Be("This game does not meet our quality standards.");
    }

    [Fact]
    public async Task Handle_WithNonExistentShareRequest_ThrowsNotFoundException()
    {
        // Arrange
        var command = new RejectShareRequestCommand(
            ShareRequestId: Guid.NewGuid(),
            AdminId: TestAdminId,
            Reason: "Not a valid submission.");

        _repositoryMock
            .Setup(r => r.GetByIdForUpdateAsync(command.ShareRequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShareRequest?)null);

        // Act & Assert
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<NotFoundException>();

        _repositoryMock.Verify(r => r.Update(It.IsAny<ShareRequest>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithWrongAdmin_ThrowsDomainException()
    {
        // Arrange
        var differentAdminId = Guid.NewGuid();
        var shareRequest = CreateShareRequestInReview(TestAdminId);
        var command = new RejectShareRequestCommand(
            ShareRequestId: shareRequest.Id,
            AdminId: differentAdminId, // Wrong admin
            Reason: "Invalid submission.");

        _repositoryMock
            .Setup(r => r.GetByIdForUpdateAsync(shareRequest.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);

        // Act & Assert - Domain throws ShareRequestReviewerMismatchException
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<Exception>();

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => _handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    private static ShareRequest CreateShareRequestInReview(Guid reviewingAdminId)
    {
        var shareRequest = ShareRequest.Create(
            TestUserId,
            TestGameId,
            ContributionType.NewGame,
            "Test notes");

        shareRequest.StartReview(reviewingAdminId);

        return shareRequest;
    }
}