using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

/// <summary>
/// Unit tests for WithdrawShareRequestCommandHandler.
/// Issue #2733: API Endpoints Utente per Share Requests
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class WithdrawShareRequestCommandHandlerTests
{
    private readonly Mock<IShareRequestRepository> _shareRequestRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<WithdrawShareRequestCommandHandler>> _loggerMock;
    private readonly WithdrawShareRequestCommandHandler _handler;

    private static readonly Guid TestUserId = Guid.NewGuid();
    private static readonly Guid TestGameId = Guid.NewGuid();

    public WithdrawShareRequestCommandHandlerTests()
    {
        _shareRequestRepositoryMock = new Mock<IShareRequestRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<WithdrawShareRequestCommandHandler>>();

        _handler = new WithdrawShareRequestCommandHandler(
            _shareRequestRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_WithdrawsRequest()
    {
        // Arrange
        var shareRequest = ShareRequest.Create(
            TestUserId,
            TestGameId,
            ContributionType.NewGame,
            userNotes: null,
            targetSharedGameId: null);

        var command = new WithdrawShareRequestCommand(
            shareRequest.Id,
            TestUserId);

        _shareRequestRepositoryMock
            .Setup(r => r.GetByIdForUpdateAsync(shareRequest.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _shareRequestRepositoryMock.Verify(
            r => r.Update(It.IsAny<ShareRequest>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.Equal(ShareRequestStatus.Withdrawn, shareRequest.Status);
    }

    [Fact]
    public async Task Handle_WithNonExistentRequest_ThrowsInvalidOperationException()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var command = new WithdrawShareRequestCommand(requestId, TestUserId);

        _shareRequestRepositoryMock
            .Setup(r => r.GetByIdForUpdateAsync(requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShareRequest?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_WithNonOwner_ThrowsInvalidOperationException()
    {
        // Arrange
        var differentUserId = Guid.NewGuid();
        var shareRequest = ShareRequest.Create(
            differentUserId,
            TestGameId,
            ContributionType.NewGame,
            userNotes: null,
            targetSharedGameId: null);

        var command = new WithdrawShareRequestCommand(
            shareRequest.Id,
            TestUserId); // Different user

        _shareRequestRepositoryMock
            .Setup(r => r.GetByIdForUpdateAsync(shareRequest.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
    }
}
