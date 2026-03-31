using Api.BoundedContexts.GameManagement.Application.EventHandlers;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.EventHandlers;

/// <summary>
/// Tests for the cross-BC handler that unlinks Games when a SharedGame is soft-deleted.
/// Spec-panel recommendation C-2.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class SharedGameSoftDeletedEventHandlerTests
{
    private readonly Mock<IGameRepository> _gameRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly SharedGameSoftDeletedEventHandler _handler;

    public SharedGameSoftDeletedEventHandlerTests()
    {
        _gameRepositoryMock = new Mock<IGameRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _handler = new SharedGameSoftDeletedEventHandler(
            _gameRepositoryMock.Object,
            _unitOfWorkMock.Object,
            new Mock<ILogger<SharedGameSoftDeletedEventHandler>>().Object);
    }

    [Fact]
    public async Task Handle_WhenGameIsLinkedToDeletedSharedGame_UnlinksIt()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var deletedBy = Guid.NewGuid();

        var linkedGame = new Game(Guid.NewGuid(), new GameTitle("Catan"));
        linkedGame.LinkToSharedGame(sharedGameId);

        _gameRepositoryMock
            .Setup(r => r.GetBySharedGameIdAsync(sharedGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Game> { linkedGame });

        var domainEvent = new SharedGameDeletedEvent(sharedGameId, deletedBy);

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        linkedGame.SharedGameId.Should().BeNull("the game must be unlinked from the deleted SharedGame");
        _gameRepositoryMock.Verify(r => r.UpdateAsync(linkedGame, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNoGameIsLinkedToDeletedSharedGame_DoesNotSave()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();

        _gameRepositoryMock
            .Setup(r => r.GetBySharedGameIdAsync(sharedGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Game>());

        var domainEvent = new SharedGameDeletedEvent(sharedGameId, Guid.NewGuid());

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        _gameRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenMultipleGamesLinkedToDeletedSharedGame_UnlinksAll()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var game1 = new Game(Guid.NewGuid(), new GameTitle("Catan"));
        var game2 = new Game(Guid.NewGuid(), new GameTitle("Wingspan"));
        game1.LinkToSharedGame(sharedGameId);
        game2.LinkToSharedGame(sharedGameId);

        _gameRepositoryMock
            .Setup(r => r.GetBySharedGameIdAsync(sharedGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Game> { game1, game2 });

        var domainEvent = new SharedGameDeletedEvent(sharedGameId, Guid.NewGuid());

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        game1.SharedGameId.Should().BeNull();
        game2.SharedGameId.Should().BeNull();
        _gameRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenLinkedGameIsApproved_RevokeApprovalOnUnlink()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();

        // Simulate an approved game linked to the deleted SharedGame
        var approvedGame = new Game(Guid.NewGuid(), new GameTitle("Catan"));
        approvedGame.LinkToSharedGame(sharedGameId);
        approvedGame.Publish(ApprovalStatus.Approved);

        approvedGame.IsPublished.Should().BeTrue("precondition: game is published before handler runs");

        _gameRepositoryMock
            .Setup(r => r.GetBySharedGameIdAsync(sharedGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Game> { approvedGame });

        var domainEvent = new SharedGameDeletedEvent(sharedGameId, Guid.NewGuid());

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        approvedGame.SharedGameId.Should().BeNull();
        approvedGame.IsPublished.Should().BeFalse(
            "approval is revoked when the linked SharedGame is deleted");
        approvedGame.ApprovalStatus.Should().Be(ApprovalStatus.Draft);
    }
}
