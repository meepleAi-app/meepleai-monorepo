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

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class ApproveShareRequestCommandHandlerTests
{
    private readonly Mock<IShareRequestRepository> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<ApproveShareRequestCommandHandler>> _loggerMock;
    private readonly ApproveShareRequestCommandHandler _handler;

    private static readonly Guid TestAdminId = Guid.NewGuid();
    private static readonly Guid TestUserId = Guid.NewGuid();
    private static readonly Guid TestGameId = Guid.NewGuid();

    public ApproveShareRequestCommandHandlerTests()
    {
        _repositoryMock = new Mock<IShareRequestRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<ApproveShareRequestCommandHandler>>();

        _handler = new ApproveShareRequestCommandHandler(
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ApprovesShareRequest()
    {
        // Arrange
        var shareRequest = CreateShareRequestInReview(TestAdminId);
        var command = new ApproveShareRequestCommand(
            ShareRequestId: shareRequest.Id,
            AdminId: TestAdminId,
            AdminNotes: "Looks good!");

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
        Assert.NotNull(response);
        Assert.Equal(shareRequest.Id, response.ShareRequestId);
        Assert.Equal(ShareRequestStatus.Approved, response.Status);
        Assert.NotEqual(default, response.ResolvedAt);

        _repositoryMock.Verify(r => r.Update(It.IsAny<ShareRequest>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        Assert.NotNull(capturedRequest);
        Assert.Equal(ShareRequestStatus.Approved, capturedRequest.Status);
    }

    [Fact]
    public async Task Handle_WithTargetSharedGameId_SetsTargetGame()
    {
        // Arrange
        var targetGameId = Guid.NewGuid();
        var shareRequest = CreateShareRequestInReview(TestAdminId);
        var command = new ApproveShareRequestCommand(
            ShareRequestId: shareRequest.Id,
            AdminId: TestAdminId,
            TargetSharedGameId: targetGameId);

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
        Assert.NotNull(capturedRequest);
        Assert.Equal(targetGameId, capturedRequest.TargetSharedGameId);
        Assert.Equal(targetGameId, response.TargetSharedGameId);
    }

    [Fact]
    public async Task Handle_WithNonExistentShareRequest_ThrowsNotFoundException()
    {
        // Arrange
        var command = new ApproveShareRequestCommand(
            ShareRequestId: Guid.NewGuid(),
            AdminId: TestAdminId);

        _repositoryMock
            .Setup(r => r.GetByIdForUpdateAsync(command.ShareRequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShareRequest?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        _repositoryMock.Verify(r => r.Update(It.IsAny<ShareRequest>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithWrongAdmin_ThrowsDomainException()
    {
        // Arrange
        var differentAdminId = Guid.NewGuid();
        var shareRequest = CreateShareRequestInReview(TestAdminId);
        var command = new ApproveShareRequestCommand(
            ShareRequestId: shareRequest.Id,
            AdminId: differentAdminId); // Wrong admin

        _repositoryMock
            .Setup(r => r.GetByIdForUpdateAsync(shareRequest.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);

        // Act & Assert - Domain throws ShareRequestReviewerMismatchException
        await Assert.ThrowsAnyAsync<Exception>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, TestContext.Current.CancellationToken));
    }

    private static ShareRequest CreateShareRequestInReview(Guid reviewingAdminId)
    {
        var shareRequest = ShareRequest.Create(
            TestUserId,
            TestGameId,
            ContributionType.NewGame,
            "Test notes");

        // Start review to put it in InReview status
        shareRequest.StartReview(reviewingAdminId);

        return shareRequest;
    }
}
