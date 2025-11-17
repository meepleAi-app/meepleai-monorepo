using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Handlers;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Comprehensive tests for CreateGameCommandHandler.
/// Tests game creation with value object validation and DTO mapping.
/// </summary>
public class CreateGameCommandHandlerTests
{
    private readonly Mock<IGameRepository> _gameRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly CreateGameCommandHandler _handler;

    public CreateGameCommandHandlerTests()
    {
        _gameRepositoryMock = new Mock<IGameRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _handler = new CreateGameCommandHandler(
            _gameRepositoryMock.Object,
            _unitOfWorkMock.Object);
    }

    #region Happy Path Tests

    [Fact]
    public async Task Handle_WithAllProperties_CreatesGameAndReturnsDto()
    {
        // Arrange
        var command = new CreateGameCommand(
            Title: "Ticket to Ride",
            Publisher: "Days of Wonder",
            YearPublished: 2004,
            MinPlayers: 2,
            MaxPlayers: 5,
            MinPlayTimeMinutes: 45,
            MaxPlayTimeMinutes: 60);

        Game? capturedGame = null;
        _gameRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()))
            .Callback<Game, CancellationToken>((g, _) => capturedGame = g)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("Ticket to Ride", result.Title);
        Assert.Equal("Days of Wonder", result.Publisher);
        Assert.Equal(2004, result.YearPublished);
        Assert.Equal(2, result.MinPlayers);
        Assert.Equal(5, result.MaxPlayers);
        Assert.Equal(45, result.MinPlayTimeMinutes);
        Assert.Equal(60, result.MaxPlayTimeMinutes);
        Assert.Null(result.BggId);
        Assert.NotEqual(default(DateTime), result.CreatedAt);

        // Verify repository interactions
        _gameRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify captured game
        Assert.NotNull(capturedGame);
        Assert.Equal("Ticket to Ride", capturedGame.Title.Value);
        Assert.Equal("Days of Wonder", capturedGame.Publisher?.Name);
    }

    [Fact]
    public async Task Handle_WithMinimalProperties_CreatesGameWithTitleOnly()
    {
        // Arrange
        var command = new CreateGameCommand(
            Title: "Chess");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Chess", result.Title);
        Assert.Null(result.Publisher);
        Assert.Null(result.YearPublished);
        Assert.Null(result.MinPlayers);
        Assert.Null(result.MaxPlayers);
        Assert.Null(result.MinPlayTimeMinutes);
        Assert.Null(result.MaxPlayTimeMinutes);
        Assert.Null(result.BggId);

        _gameRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithPartialPlayerCount_CreatesGameWithNullPlayerCount()
    {
        // Arrange - Only MinPlayers provided
        var command = new CreateGameCommand(
            Title: "Solitaire",
            MinPlayers: 1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Solitaire", result.Title);
        Assert.Null(result.MinPlayers); // Not created because MaxPlayers is missing
        Assert.Null(result.MaxPlayers);
    }

    [Fact]
    public async Task Handle_WithPartialPlayTime_CreatesGameWithNullPlayTime()
    {
        // Arrange - Only MinPlayTimeMinutes provided
        var command = new CreateGameCommand(
            Title: "Quick Game",
            MinPlayTimeMinutes: 15);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Quick Game", result.Title);
        Assert.Null(result.MinPlayTimeMinutes); // Not created because MaxPlayTimeMinutes is missing
        Assert.Null(result.MaxPlayTimeMinutes);
    }

    [Fact]
    public async Task Handle_WithPlayerCountOnly_CreatesGameWithPlayerCount()
    {
        // Arrange
        var command = new CreateGameCommand(
            Title: "Pandemic",
            MinPlayers: 2,
            MaxPlayers: 4);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Pandemic", result.Title);
        Assert.Equal(2, result.MinPlayers);
        Assert.Equal(4, result.MaxPlayers);
        Assert.Null(result.Publisher);
        Assert.Null(result.YearPublished);
    }

    [Fact]
    public async Task Handle_WithPlayTimeOnly_CreatesGameWithPlayTime()
    {
        // Arrange
        var command = new CreateGameCommand(
            Title: "7 Wonders",
            MinPlayTimeMinutes: 30,
            MaxPlayTimeMinutes: 45);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("7 Wonders", result.Title);
        Assert.Equal(30, result.MinPlayTimeMinutes);
        Assert.Equal(45, result.MaxPlayTimeMinutes);
        Assert.Null(result.Publisher);
        Assert.Null(result.MinPlayers);
    }

    [Fact]
    public async Task Handle_WithPublisherAndYearOnly_CreatesGameCorrectly()
    {
        // Arrange
        var command = new CreateGameCommand(
            Title: "Azul",
            Publisher: "Plan B Games",
            YearPublished: 2017);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Azul", result.Title);
        Assert.Equal("Plan B Games", result.Publisher);
        Assert.Equal(2017, result.YearPublished);
        Assert.Null(result.MinPlayers);
        Assert.Null(result.MinPlayTimeMinutes);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Handle_WithVeryLongTitle_CreatesGame()
    {
        // Arrange
        var longTitle = new string('A', 200);
        var command = new CreateGameCommand(Title: longTitle);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(longTitle, result.Title);
    }

    [Fact]
    public async Task Handle_WithSoloGame_CreatesWithOnePlayerCount()
    {
        // Arrange
        var command = new CreateGameCommand(
            Title: "Onirim",
            MinPlayers: 1,
            MaxPlayers: 1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.MinPlayers);
        Assert.Equal(1, result.MaxPlayers);
    }

    [Fact]
    public async Task Handle_WithManyPlayers_CreatesCorrectly()
    {
        // Arrange
        var command = new CreateGameCommand(
            Title: "Werewolf",
            MinPlayers: 5,
            MaxPlayers: 20);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(5, result.MinPlayers);
        Assert.Equal(20, result.MaxPlayers);
    }

    [Fact]
    public async Task Handle_WithOldGame_CreatesWithOldYear()
    {
        // Arrange
        var command = new CreateGameCommand(
            Title: "Chess",
            YearPublished: 1475); // Historical game

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1475, result.YearPublished);
    }

    [Fact]
    public async Task Handle_WithRecentGame_CreatesWithCurrentYear()
    {
        // Arrange
        var currentYear = DateTime.UtcNow.Year;
        var command = new CreateGameCommand(
            Title: "New Release 2025",
            YearPublished: currentYear);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(currentYear, result.YearPublished);
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task Handle_WithCancellationToken_PassesToRepository()
    {
        // Arrange
        var command = new CreateGameCommand(Title: "Catan");
        var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        // Act
        await _handler.Handle(command, cancellationToken);

        // Assert
        _gameRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<Game>(), cancellationToken),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(cancellationToken),
            Times.Once);
    }

    #endregion

    #region Generated ID Tests

    [Fact]
    public async Task Handle_MultipleGames_GeneratesDifferentIds()
    {
        // Arrange
        var command1 = new CreateGameCommand(Title: "Game 1");
        var command2 = new CreateGameCommand(Title: "Game 2");

        // Act
        var result1 = await _handler.Handle(command1, CancellationToken.None);
        var result2 = await _handler.Handle(command2, CancellationToken.None);

        // Assert
        Assert.NotEqual(result1.Id, result2.Id);
    }

    #endregion

    #region DTO Mapping Tests

    [Fact]
    public async Task Handle_MapsAllPropertiesToDto()
    {
        // Arrange
        var command = new CreateGameCommand(
            Title: "Wingspan",
            Publisher: "Stonemaier Games",
            YearPublished: 2019,
            MinPlayers: 1,
            MaxPlayers: 5,
            MinPlayTimeMinutes: 40,
            MaxPlayTimeMinutes: 70);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert - DTO should have all properties mapped
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("Wingspan", result.Title);
        Assert.Equal("Stonemaier Games", result.Publisher);
        Assert.Equal(2019, result.YearPublished);
        Assert.Equal(1, result.MinPlayers);
        Assert.Equal(5, result.MaxPlayers);
        Assert.Equal(40, result.MinPlayTimeMinutes);
        Assert.Equal(70, result.MaxPlayTimeMinutes);
        Assert.Null(result.BggId); // Not set during creation
        Assert.True(result.CreatedAt <= DateTime.UtcNow);
        Assert.True(result.CreatedAt >= DateTime.UtcNow.AddSeconds(-5)); // Created within last 5 seconds
    }

    #endregion
}
