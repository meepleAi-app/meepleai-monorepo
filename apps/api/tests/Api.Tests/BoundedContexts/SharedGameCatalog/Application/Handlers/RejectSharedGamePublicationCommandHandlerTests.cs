using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

/// <summary>
/// Integration tests for RejectSharedGamePublicationCommandHandler.
/// Issue #2514: Approval workflow implementation
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class RejectSharedGamePublicationCommandHandlerTests
{
    private readonly Mock<ISharedGameRepository> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<RejectSharedGamePublicationCommandHandler>> _loggerMock;
    private readonly RejectSharedGamePublicationCommandHandler _handler;

    public RejectSharedGamePublicationCommandHandlerTests()
    {
        _repositoryMock = new Mock<ISharedGameRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<RejectSharedGamePublicationCommandHandler>>();
        _handler = new RejectSharedGamePublicationCommandHandler(
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithPendingApprovalGame_RejectsSuccessfully()
    {
        // Arrange
        var rejecterId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var reason = "Needs more detailed description";

        var game = SharedGame.Create(
            "Test Game",
            2024,
            "Description",
            2,
            4,
            60,
            10,
            2.5m,
            7.5m,
            "https://example.com/image.jpg",
            "https://example.com/thumb.jpg",
            GameRules.Create("Rules", "en"),
            creatorId);

        // Submit for approval first
        game.SubmitForApproval(creatorId);
        Assert.Equal(GameStatus.PendingApproval, game.Status);

        var command = new RejectSharedGamePublicationCommand(game.Id, rejecterId, reason);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        SharedGame? capturedGame = null;
        _repositoryMock
            .Setup(r => r.Update(It.IsAny<SharedGame>()))
            .Callback<SharedGame>(g => capturedGame = g);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _repositoryMock.Verify(
            r => r.Update(It.IsAny<SharedGame>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.NotNull(capturedGame);
        Assert.Equal(GameStatus.Draft, capturedGame.Status);
        Assert.Equal(rejecterId, capturedGame.ModifiedBy);
        Assert.NotNull(capturedGame.ModifiedAt);

        // Verify domain event raised
        var rejectEvent = Assert.IsType<SharedGamePublicationRejectedEvent>(capturedGame.DomainEvents.Last());
        Assert.Equal(game.Id, rejectEvent.GameId);
        Assert.Equal(rejecterId, rejectEvent.RejectedBy);
        Assert.Equal(reason, rejectEvent.Reason);
    }

    [Fact]
    public async Task Handle_WithNonExistentGame_ThrowsInvalidOperationException()
    {
        // Arrange
        var command = new RejectSharedGamePublicationCommand(Guid.NewGuid(), Guid.NewGuid(), "Reason");

        _repositoryMock
            .Setup(r => r.GetByIdAsync(command.GameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_WithDraftGame_ThrowsInvalidOperationException()
    {
        // Arrange
        var game = SharedGame.Create(
            "Test Game",
            2024,
            "Description",
            2,
            4,
            60,
            10,
            null,
            null,
            "https://example.com/image.jpg",
            "https://example.com/thumb.jpg",
            null,
            Guid.NewGuid());

        var command = new RejectSharedGamePublicationCommand(game.Id, Guid.NewGuid(), "Reason");

        _repositoryMock
            .Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
    }
}
