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
public class RequestShareRequestChangesCommandHandlerTests
{
    private readonly Mock<IShareRequestRepository> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<RequestShareRequestChangesCommandHandler>> _loggerMock;
    private readonly RequestShareRequestChangesCommandHandler _handler;

    private static readonly Guid TestAdminId = Guid.NewGuid();
    private static readonly Guid TestUserId = Guid.NewGuid();
    private static readonly Guid TestGameId = Guid.NewGuid();

    public RequestShareRequestChangesCommandHandlerTests()
    {
        _repositoryMock = new Mock<IShareRequestRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<RequestShareRequestChangesCommandHandler>>();

        _handler = new RequestShareRequestChangesCommandHandler(
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_RequestsChanges()
    {
        // Arrange
        var shareRequest = CreateShareRequestInReview(TestAdminId);
        var command = new RequestShareRequestChangesCommand(
            ShareRequestId: shareRequest.Id,
            AdminId: TestAdminId,
            Feedback: "Please add higher resolution images and more detailed game description.");

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
        Assert.Equal(ShareRequestStatus.ChangesRequested, response.Status);
        Assert.NotEqual(default, response.ModifiedAt);

        _repositoryMock.Verify(r => r.Update(It.IsAny<ShareRequest>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        Assert.NotNull(capturedRequest);
        Assert.Equal(ShareRequestStatus.ChangesRequested, capturedRequest.Status);
        Assert.Contains("higher resolution images", capturedRequest.AdminFeedback);
    }

    [Fact]
    public async Task Handle_WithNonExistentShareRequest_ThrowsNotFoundException()
    {
        // Arrange
        var command = new RequestShareRequestChangesCommand(
            ShareRequestId: Guid.NewGuid(),
            AdminId: TestAdminId,
            Feedback: "Please update the description.");

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
        var command = new RequestShareRequestChangesCommand(
            ShareRequestId: shareRequest.Id,
            AdminId: differentAdminId, // Wrong admin
            Feedback: "Please make some changes.");

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

    [Fact]
    public async Task Handle_AfterChangesRequested_UserCanResubmit()
    {
        // Arrange - Request in InReview status
        var shareRequest = CreateShareRequestInReview(TestAdminId);
        var command = new RequestShareRequestChangesCommand(
            ShareRequestId: shareRequest.Id,
            AdminId: TestAdminId,
            Feedback: "Please improve the documentation.");

        _repositoryMock
            .Setup(r => r.GetByIdForUpdateAsync(shareRequest.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);

        ShareRequest? capturedRequest = null;
        _repositoryMock
            .Setup(r => r.Update(It.IsAny<ShareRequest>()))
            .Callback<ShareRequest>(req => capturedRequest = req);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - Verify request can be resubmitted
        Assert.NotNull(capturedRequest);
        Assert.Equal(ShareRequestStatus.ChangesRequested, capturedRequest.Status);

        // User should be able to resubmit after changes are requested
        capturedRequest.Resubmit("Updated notes after changes");
        Assert.Equal(ShareRequestStatus.Pending, capturedRequest.Status);
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
