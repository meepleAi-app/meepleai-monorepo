using Api.BoundedContexts.GameManagement.Application.Commands;
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
        Assert.NotNull(result);
        Assert.Equal(gameId, result.Id);
        Assert.Equal("New Title", result.Title);
        Assert.Equal("New Publisher", result.Publisher);
        Assert.Equal(2020, result.YearPublished);
        Assert.Equal(1, result.MinPlayers);
        Assert.Equal(6, result.MaxPlayers);
        Assert.Equal(45, result.MinPlayTimeMinutes);
        Assert.Equal(90, result.MaxPlayTimeMinutes);

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
        Assert.Equal("Updated Title", result.Title);
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
        Assert.Equal("Catan", result.Title); // Unchanged
        Assert.Equal("New Publisher", result.Publisher);
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
        Assert.Equal(1, result.MinPlayers);
        Assert.Equal(5, result.MaxPlayers);
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
        Assert.Equal(60, result.MinPlayTimeMinutes);
        Assert.Equal(120, result.MaxPlayTimeMinutes);
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
        Assert.Equal(2025, result.YearPublished);
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
        await Assert.ThrowsAsync<Api.SharedKernel.Domain.Exceptions.ValidationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken)
        );

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
        var exception = await Assert.ThrowsAsync<Api.SharedKernel.Domain.Exceptions.ValidationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken)
        );

        Assert.Contains("cannot exceed 200 characters", exception.Message);
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
        var exception = await Assert.ThrowsAsync<Api.SharedKernel.Domain.Exceptions.ValidationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken)
        );

        Assert.Contains("Minimum player count cannot be less than 1", exception.Message);
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
        var exception = await Assert.ThrowsAsync<Api.SharedKernel.Domain.Exceptions.ValidationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken)
        );

        Assert.Contains("Maximum player count cannot exceed 100", exception.Message);
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
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        Assert.Contains($"Game with ID {gameId} not found", exception.Message, StringComparison.OrdinalIgnoreCase);

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
        Assert.NotNull(result);
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
        Assert.Equal(2, result.MinPlayers);
        Assert.Equal(4, result.MaxPlayers);
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
        Assert.Equal(30, result.MinPlayTimeMinutes);
        Assert.Equal(45, result.MaxPlayTimeMinutes);
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
        Assert.NotEqual(default(DateTime), result.CreatedAt);
        Assert.Equal(existingGame.CreatedAt, result.CreatedAt);
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
        Assert.Equal(13, result.BggId);
        Assert.Equal("Settlers of Catan", result.Title);
    }
}

