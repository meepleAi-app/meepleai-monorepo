using Api.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;
using Api.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Handlers.PrivateGames;

/// <summary>
/// Comprehensive unit tests for DeletePrivateGameCommandHandler.
/// Issue #3670: Phase 9 - Testing &amp; Polish.
/// Tests: Successful delete, ownership verification, not found scenarios.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public sealed class DeletePrivateGameCommandHandlerTests
{
    private readonly Mock<IPrivateGameRepository> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<DeletePrivateGameCommandHandler>> _loggerMock;
    private readonly DeletePrivateGameCommandHandler _handler;

    public DeletePrivateGameCommandHandlerTests()
    {
        _repositoryMock = new Mock<IPrivateGameRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<DeletePrivateGameCommandHandler>>();

        _handler = new DeletePrivateGameCommandHandler(
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_NullRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () =>
            new DeletePrivateGameCommandHandler(
                null!,
                _unitOfWorkMock.Object,
                _loggerMock.Object);
        var exception = act.Should().Throw<ArgumentNullException>().Which;

        exception.ParamName.Should().Be("repository");
    }

    [Fact]
    public void Constructor_NullUnitOfWork_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act2 = () =>
            new DeletePrivateGameCommandHandler(
                _repositoryMock.Object,
                null!,
                _loggerMock.Object);
        var exception = act2.Should().Throw<ArgumentNullException>().Which;

        exception.ParamName.Should().Be("unitOfWork");
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act3 = () =>
            new DeletePrivateGameCommandHandler(
                _repositoryMock.Object,
                _unitOfWorkMock.Object,
                null!);
        var exception = act3.Should().Throw<ArgumentNullException>().Which;

        exception.ParamName.Should().Be("logger");
    }

    #endregion

    #region Successful Delete Tests

    [Fact]
    public async Task Handle_ValidOwner_SoftDeletesGame()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var existingGame = PrivateGame.CreateManual(
            ownerId: userId,
            title: "Game to Delete",
            minPlayers: 2,
            maxPlayers: 4);

        typeof(PrivateGame).GetProperty("Id")!.SetValue(existingGame, gameId);

        var command = new DeletePrivateGameCommand(gameId, userId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingGame);

        _repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<PrivateGame>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        existingGame.IsDeleted.Should().BeTrue();
        existingGame.DeletedAt.Should().NotBeNull();
        existingGame.DeletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        _repositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<PrivateGame>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_BggGame_SoftDeletesSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var existingGame = PrivateGame.CreateFromBgg(
            ownerId: userId,
            bggId: 12345,
            title: "BGG Game to Delete",
            yearPublished: 2020,
            description: "Test",
            minPlayers: 2,
            maxPlayers: 4,
            playingTimeMinutes: 60,
            minAge: 10,
            complexityRating: 2.5m,
            imageUrl: "https://example.com/image.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg");

        typeof(PrivateGame).GetProperty("Id")!.SetValue(existingGame, gameId);

        var command = new DeletePrivateGameCommand(gameId, userId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingGame);

        _repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<PrivateGame>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        existingGame.IsDeleted.Should().BeTrue();
        existingGame.BggId.Should().Be(12345); // BggId preserved after delete
    }

    #endregion

    #region Not Found Tests

    [Fact]
    public async Task Handle_GameNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var command = new DeletePrivateGameCommand(gameId, Guid.NewGuid());

        _repositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PrivateGame?)null);

        // Act & Assert
        var act4 = () =>
            _handler.Handle(command, CancellationToken.None);
        var exception = (await act4.Should().ThrowAsync<NotFoundException>()).Which;

        exception.Message.Should().Contain(gameId.ToString());

        _repositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<PrivateGame>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Ownership Verification Tests

    [Fact]
    public async Task Handle_DifferentOwner_ThrowsForbiddenException()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var existingGame = PrivateGame.CreateManual(
            ownerId: ownerId,
            title: "Victim's Game",
            minPlayers: 2,
            maxPlayers: 4);

        typeof(PrivateGame).GetProperty("Id")!.SetValue(existingGame, gameId);

        var command = new DeletePrivateGameCommand(gameId, attackerId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingGame);

        // Act & Assert
        var act5 = () =>
            _handler.Handle(command, CancellationToken.None);
        var exception = (await act5.Should().ThrowAsync<ForbiddenException>()).Which;

        exception.Message.Should().Contain("only delete your own");

        // Verify game was NOT deleted
        existingGame.IsDeleted.Should().BeFalse();

        _repositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<PrivateGame>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_SameOwner_DeletesSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var existingGame = PrivateGame.CreateManual(
            ownerId: userId,
            title: "My Game to Delete",
            minPlayers: 2,
            maxPlayers: 4);

        typeof(PrivateGame).GetProperty("Id")!.SetValue(existingGame, gameId);

        var command = new DeletePrivateGameCommand(gameId, userId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingGame);

        _repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<PrivateGame>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        existingGame.IsDeleted.Should().BeTrue();
    }

    #endregion

    #region Null Command Tests

    [Fact]
    public async Task Handle_NullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act6 = () =>
            _handler.Handle(null!, CancellationToken.None);
        await act6.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region Command Record Tests

    [Fact]
    public void DeletePrivateGameCommand_CreatesWithRequiredProperties()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act
        var command = new DeletePrivateGameCommand(gameId, userId);

        // Assert
        command.PrivateGameId.Should().Be(gameId);
        command.UserId.Should().Be(userId);
    }

    [Fact]
    public void DeletePrivateGameCommand_TwoInstancesWithSameValues_AreEqual()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act
        var command1 = new DeletePrivateGameCommand(gameId, userId);
        var command2 = new DeletePrivateGameCommand(gameId, userId);

        // Assert
        command1.Should().Be(command2);
    }

    #endregion

    #region Soft Delete Verification Tests

    [Fact]
    public async Task Handle_Delete_PreservesGameData()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var existingGame = PrivateGame.CreateManual(
            ownerId: userId,
            title: "Preserved Data Game",
            minPlayers: 2,
            maxPlayers: 6,
            yearPublished: 2023,
            description: "This description should be preserved",
            complexityRating: 3.5m);

        typeof(PrivateGame).GetProperty("Id")!.SetValue(existingGame, gameId);

        var command = new DeletePrivateGameCommand(gameId, userId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingGame);

        _repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<PrivateGame>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert - All data is preserved, only IsDeleted and DeletedAt change
        existingGame.IsDeleted.Should().BeTrue();
        existingGame.DeletedAt.Should().NotBeNull();
        existingGame.Title.Should().Be("Preserved Data Game");
        existingGame.MinPlayers.Should().Be(2);
        existingGame.MaxPlayers.Should().Be(6);
        existingGame.YearPublished.Should().Be(2023);
        existingGame.Description.Should().Be("This description should be preserved");
        existingGame.ComplexityRating.Should().Be(3.5m);
        existingGame.OwnerId.Should().Be(userId);
    }

    [Fact]
    public async Task Handle_Delete_UpdatesDeletedAtTimestamp()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var existingGame = PrivateGame.CreateManual(
            ownerId: userId,
            title: "Timestamp Test Game",
            minPlayers: 2,
            maxPlayers: 4);

        typeof(PrivateGame).GetProperty("Id")!.SetValue(existingGame, gameId);

        var command = new DeletePrivateGameCommand(gameId, userId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingGame);

        _repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<PrivateGame>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var beforeDelete = DateTime.UtcNow;

        // Act
        await _handler.Handle(command, CancellationToken.None);

        var afterDelete = DateTime.UtcNow;

        // Assert
        existingGame.DeletedAt.Should().NotBeNull();
        existingGame.DeletedAt!.Value.Should().BeOnOrAfter(beforeDelete);
        existingGame.DeletedAt!.Value.Should().BeOnOrBefore(afterDelete);
    }

    #endregion
}
