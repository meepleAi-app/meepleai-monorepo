using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.BoundedContexts.GameManagement.TestHelpers;
using Api.Tests.TestHelpers;
using Moq;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Comprehensive tests for UpdateGameCommandHandler.
/// Tests game updates with partial updates and value object validation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class UpdateGameCommandHandlerTests
{
    private readonly Mock<IGameRepository> _gameRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly UpdateGameCommandHandler _handler;

    public UpdateGameCommandHandlerTests()
    {
        _gameRepositoryMock = new Mock<IGameRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _handler = new UpdateGameCommandHandler(
            _gameRepositoryMock.Object,
            _unitOfWorkMock.Object);
    }
    [Fact]
    public async Task Handle_WithAllProperties_UpdatesGameAndReturnsDto()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var existingGame = new GameBuilder()
            .WithId(gameId)
            .WithTitle("Old Title")
            .WithPublisher("Old Publisher")
            .WithYearPublished(2000)
            .WithPlayerCount(2, 4)
            .WithPlayTime(30, 45)
            .Build();

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingGame);

        var command = new UpdateGameCommand(
            GameId: gameId,
            Title: "New Title",
            Publisher: "New Publisher",
            YearPublished: 2020,
            MinPlayers: 1,
            MaxPlayers: 6,
            MinPlayTimeMinutes: 45,
            MaxPlayTimeMinutes: 90);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(gameId);
        result.Title.Should().Be("New Title");
        result.Publisher.Should().Be("New Publisher");
        result.YearPublished.Should().Be(2020);
        result.MinPlayers.Should().Be(1);
        result.MaxPlayers.Should().Be(6);
        result.MinPlayTimeMinutes.Should().Be(45);
        result.MaxPlayTimeMinutes.Should().Be(90);

        // Verify repository interactions
        _gameRepositoryMock.Verify(
            r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()),
            Times.Once);
        _gameRepositoryMock.Verify(
            r => r.UpdateAsync(existingGame, It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithTitleOnly_UpdatesOnlyTitle()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var existingGame = new GameBuilder()
            .WithId(gameId)
            .WithTitle("Original Title")
            .Build();

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingGame);

        var command = new UpdateGameCommand(
            GameId: gameId,
            Title: "Updated Title");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Title.Should().Be("Updated Title");
    }

    [Fact]
    public async Task Handle_WithPublisherOnly_UpdatesOnlyPublisher()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var existingGame = new GameBuilder()
            .WithId(gameId)
            .WithTitle("Catan")
            .WithPublisher("Old Publisher")
            .Build();

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingGame);

        var command = new UpdateGameCommand(
            GameId: gameId,
            Publisher: "New Publisher");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Title.Should().Be("Catan"); // Unchanged
        result.Publisher.Should().Be("New Publisher");
    }

    [Fact]
    public async Task Handle_WithPlayerCountUpdate_UpdatesPlayerCount()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var existingGame = new GameBuilder()
            .WithId(gameId)
            .WithTitle("Game")
            .WithPlayerCount(2, 4)
            .Build();

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingGame);

        var command = new UpdateGameCommand(
            GameId: gameId,
            MinPlayers: 1,
            MaxPlayers: 5);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.MinPlayers.Should().Be(1);
        result.MaxPlayers.Should().Be(5);
    }

    [Fact]
    public async Task Handle_WithPlayTimeUpdate_UpdatesPlayTime()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var existingGame = new GameBuilder()
            .WithId(gameId)
            .WithTitle("Game")
            .WithPlayTime(30, 45)
            .Build();

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingGame);

        var command = new UpdateGameCommand(
            GameId: gameId,
            MinPlayTimeMinutes: 60,
            MaxPlayTimeMinutes: 120);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.MinPlayTimeMinutes.Should().Be(60);
        result.MaxPlayTimeMinutes.Should().Be(120);
    }

    [Fact]
    public async Task Handle_WithYearUpdate_UpdatesYear()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var existingGame = new GameBuilder()
            .WithId(gameId)
            .WithTitle("Game")
            .WithYearPublished(2000)
            .Build();

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingGame);

        var command = new UpdateGameCommand(
            GameId: gameId,
            YearPublished: 2025);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.YearPublished.Should().Be(2025);
    }

    // ===== VALIDATION TESTS =====

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_EmptyOrWhitespaceTitle_ThrowsValidationException(string invalidTitle)
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var game = CreateTestGame(gameId);

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        var command = new UpdateGameCommand(
            GameId: gameId,
            Title: invalidTitle);

        // Act & Assert
        var act =
            () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<Api.SharedKernel.Domain.Exceptions.ValidationException>();

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_TitleExceedsMaxLength_ThrowsValidationException()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var game = CreateTestGame(gameId);

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        var longTitle = new string('A', 201); // Exceeds 200 character limit
        var command = new UpdateGameCommand(
            GameId: gameId,
            Title: longTitle);

        // Act & Assert
        var act =
            () => _handler.Handle(command, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<Api.SharedKernel.Domain.Exceptions.ValidationException>()).Which;

        exception.Message.Should().Contain("cannot exceed 200 characters");
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0, 4)]   // Min < 1
    [InlineData(-1, 4)]  // Negative min
    public async Task Handle_InvalidMinPlayers_ThrowsValidationException(int minPlayers, int maxPlayers)
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var game = CreateTestGame(gameId);

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        var command = new UpdateGameCommand(
            GameId: gameId,
            MinPlayers: minPlayers,
            MaxPlayers: maxPlayers);

        // Act & Assert
        var act =
            () => _handler.Handle(command, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<Api.SharedKernel.Domain.Exceptions.ValidationException>()).Which;

        exception.Message.Should().Contain("Minimum player count cannot be less than 1");
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(2, 101)]  // Max > 100
    [InlineData(1, 150)]  // Max far exceeds limit
    public async Task Handle_MaxPlayersExceedsLimit_ThrowsValidationException(int minPlayers, int maxPlayers)
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var game = CreateTestGame(gameId);

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        var command = new UpdateGameCommand(
            GameId: gameId,
            MinPlayers: minPlayers,
            MaxPlayers: maxPlayers);

        // Act & Assert
        var act =
            () => _handler.Handle(command, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<Api.SharedKernel.Domain.Exceptions.ValidationException>()).Which;

        exception.Message.Should().Contain("Maximum player count cannot exceed 100");
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Game CreateTestGame(Guid id)
    {
        var title = new GameTitle("Test Game");
        return new Game(
            id: id,
            title: title,
            publisher: null,
            yearPublished: null,
            playerCount: null,
            playTime: null
        );
    }

    [Fact]
    public async Task Handle_NonExistentGame_ThrowsInvalidOperationException()
    {
        // Arrange
        var gameId = Guid.NewGuid();

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Game?)null);

        var command = new UpdateGameCommand(
            GameId: gameId,
            Title: "Updated Title");

        // Act & Assert
        var act =
            () => _handler.Handle(command, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;

        exception.Message.Should().ContainEquivalentOf($"Game with ID {gameId} not found");

        // Verify update was NOT called
        _gameRepositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithNoUpdates_StillCallsSaveChanges()
    {
        // Arrange - Command with all null values
        var gameId = Guid.NewGuid();
        var existingGame = new GameBuilder()
            .WithId(gameId)
            .WithTitle("Game")
            .Build();

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingGame);

        var command = new UpdateGameCommand(GameId: gameId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - Should still persist (UpdateDetails called with all nulls)
        result.Should().NotBeNull();
        _gameRepositoryMock.Verify(
            r => r.UpdateAsync(existingGame, It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithPartialPlayerCount_DoesNotUpdatePlayerCount()
    {
        // Arrange - Only MinPlayers provided, MaxPlayers missing
        var gameId = Guid.NewGuid();
        var existingGame = new GameBuilder()
            .WithId(gameId)
            .WithTitle("Game")
            .WithPlayerCount(2, 4)
            .Build();

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingGame);

        var command = new UpdateGameCommand(
            GameId: gameId,
            MinPlayers: 1); // MaxPlayers is null

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - PlayerCount should remain unchanged (2-4)
        result.MinPlayers.Should().Be(2);
        result.MaxPlayers.Should().Be(4);
    }

    [Fact]
    public async Task Handle_WithPartialPlayTime_DoesNotUpdatePlayTime()
    {
        // Arrange - Only MinPlayTimeMinutes provided
        var gameId = Guid.NewGuid();
        var existingGame = new GameBuilder()
            .WithId(gameId)
            .WithTitle("Game")
            .WithPlayTime(30, 45)
            .Build();

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingGame);

        var command = new UpdateGameCommand(
            GameId: gameId,
            MinPlayTimeMinutes: 60); // MaxPlayTimeMinutes is null

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - PlayTime should remain unchanged (30-45)
        result.MinPlayTimeMinutes.Should().Be(30);
        result.MaxPlayTimeMinutes.Should().Be(45);
    }
    [Fact]
    public async Task Handle_WithCancellationToken_PassesToRepository()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var existingGame = new GameBuilder()
            .WithId(gameId)
            .WithTitle("Game")
            .Build();

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingGame);

        var command = new UpdateGameCommand(
            GameId: gameId,
            Title: "Updated");

        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        // Act
        await _handler.Handle(command, cancellationToken);

        // Assert
        _gameRepositoryMock.Verify(
            r => r.GetByIdAsync(gameId, cancellationToken),
            Times.Once);
        _gameRepositoryMock.Verify(
            r => r.UpdateAsync(existingGame, cancellationToken),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(cancellationToken),
            Times.Once);
    }
    [Fact]
    public async Task Handle_UpdatePreservesCreatedAt()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow.AddDays(-30);
        var existingGame = new GameBuilder()
            .WithId(gameId)
            .WithTitle("Original")
            .Build();

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingGame);

        var command = new UpdateGameCommand(
            GameId: gameId,
            Title: "Updated Title");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - CreatedAt should be preserved
        result.CreatedAt.Should().NotBe(default(DateTime));
        result.CreatedAt.Should().Be(existingGame.CreatedAt);
    }

    [Fact]
    public async Task Handle_UpdatePreservesBggId()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var existingGame = new GameBuilder()
            .WithId(gameId)
            .WithTitle("Catan")
            .WithBggLink(13, "{\"rating\":7.2}")
            .Build();

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingGame);

        var command = new UpdateGameCommand(
            GameId: gameId,
            Title: "Settlers of Catan"); // Update title, preserve BGG link

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - BGG ID should be preserved
        result.BggId.Should().Be(13);
        result.Title.Should().Be("Settlers of Catan");
    }
}

