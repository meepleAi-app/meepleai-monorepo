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
        response.Should().NotBeNull();
        response.ShareRequestId.Should().Be(shareRequest.Id);
        response.Status.Should().Be(ShareRequestStatus.ChangesRequested);
        response.ModifiedAt.Should().NotBe(default);

        _repositoryMock.Verify(r => r.Update(It.IsAny<ShareRequest>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        capturedRequest.Should().NotBeNull();
        capturedRequest.Status.Should().Be(ShareRequestStatus.ChangesRequested);
        capturedRequest.AdminFeedback.Should().Contain("higher resolution images");
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
        var command = new RequestShareRequestChangesCommand(
            ShareRequestId: shareRequest.Id,
            AdminId: differentAdminId, // Wrong admin
            Feedback: "Please make some changes.");

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
        capturedRequest.Should().NotBeNull();
        capturedRequest.Status.Should().Be(ShareRequestStatus.ChangesRequested);

        // User should be able to resubmit after changes are requested
        capturedRequest.Resubmit("Updated notes after changes");
        capturedRequest.Status.Should().Be(ShareRequestStatus.Pending);
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