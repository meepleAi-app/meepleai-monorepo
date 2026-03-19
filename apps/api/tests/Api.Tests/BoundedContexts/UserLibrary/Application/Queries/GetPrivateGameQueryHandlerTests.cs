using Api.BoundedContexts.UserLibrary.Application.Handlers.PrivateGames;
using Api.BoundedContexts.UserLibrary.Application.Queries.PrivateGames;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Queries.PrivateGames;

/// <summary>
/// Comprehensive unit tests for GetPrivateGameQueryHandler.
/// Issue #3670: Phase 9 - Testing &amp; Polish.
/// Tests: Successful retrieval, ownership verification, not found scenarios.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public sealed class GetPrivateGameQueryHandlerTests
{
    private readonly Mock<IPrivateGameRepository> _repositoryMock;
    private readonly Mock<ILogger<GetPrivateGameQueryHandler>> _loggerMock;
    private readonly GetPrivateGameQueryHandler _handler;

    public GetPrivateGameQueryHandlerTests()
    {
        _repositoryMock = new Mock<IPrivateGameRepository>();
        _loggerMock = new Mock<ILogger<GetPrivateGameQueryHandler>>();

        _handler = new GetPrivateGameQueryHandler(
            _repositoryMock.Object,
            _loggerMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_NullRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new GetPrivateGameQueryHandler(
                null!,
                _loggerMock.Object));

        exception.ParamName.Should().Be("repository");
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new GetPrivateGameQueryHandler(
                _repositoryMock.Object,
                null!));

        exception.ParamName.Should().Be("logger");
    }

    #endregion

    #region Successful Retrieval Tests

    [Fact]
    public async Task Handle_ManualGameByOwner_ReturnsDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var existingGame = PrivateGame.CreateManual(
            ownerId: userId,
            title: "Manual Test Game",
            minPlayers: 2,
            maxPlayers: 4,
            yearPublished: 2023,
            description: "Test description",
            complexityRating: 3.0m);

        typeof(PrivateGame).GetProperty("Id")!.SetValue(existingGame, gameId);

        var query = new GetPrivateGameQuery(gameId, userId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingGame);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(gameId);
        result.OwnerId.Should().Be(userId);
        result.Source.Should().Be("Manual");
        result.Title.Should().Be("Manual Test Game");
        result.MinPlayers.Should().Be(2);
        result.MaxPlayers.Should().Be(4);
        result.YearPublished.Should().Be(2023);
        result.Description.Should().Be("Test description");
        result.ComplexityRating.Should().Be(3.0m);
        result.BggId.Should().BeNull();
        result.CanProposeToCatalog.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_BggGameByOwner_ReturnsDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var existingGame = PrivateGame.CreateFromBgg(
            ownerId: userId,
            bggId: 12345,
            title: "BGG Test Game",
            yearPublished: 2020,
            description: "BGG description",
            minPlayers: 2,
            maxPlayers: 5,
            playingTimeMinutes: 120,
            minAge: 14,
            complexityRating: 4.2m,
            imageUrl: "https://example.com/image.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg");

        typeof(PrivateGame).GetProperty("Id")!.SetValue(existingGame, gameId);

        var query = new GetPrivateGameQuery(gameId, userId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingGame);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Source.Should().Be("BoardGameGeek");
        result.BggId.Should().Be(12345);
        result.ThumbnailUrl.Should().Be("https://example.com/thumb.jpg");
        result.BggSyncedAt.Should().NotBeNull();
        result.CanProposeToCatalog.Should().BeTrue();
    }

    #endregion

    #region Not Found Tests

    [Fact]
    public async Task Handle_GameNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var query = new GetPrivateGameQuery(gameId, Guid.NewGuid());

        _repositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PrivateGame?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.Handle(query, CancellationToken.None));

        exception.Message.Should().Contain(gameId.ToString());
    }

    #endregion

    #region Ownership Verification Tests

    [Fact]
    public async Task Handle_DifferentOwner_ThrowsForbiddenException()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var existingGame = PrivateGame.CreateManual(
            ownerId: ownerId,
            title: "Owner's Private Game",
            minPlayers: 2,
            maxPlayers: 4);

        typeof(PrivateGame).GetProperty("Id")!.SetValue(existingGame, gameId);

        var query = new GetPrivateGameQuery(gameId, viewerId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingGame);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ForbiddenException>(() =>
            _handler.Handle(query, CancellationToken.None));

        exception.Message.Should().Contain("only view your own");
    }

    [Fact]
    public async Task Handle_SameOwner_ReturnsSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var existingGame = PrivateGame.CreateManual(
            ownerId: userId,
            title: "My Game",
            minPlayers: 2,
            maxPlayers: 4);

        typeof(PrivateGame).GetProperty("Id")!.SetValue(existingGame, gameId);

        var query = new GetPrivateGameQuery(gameId, userId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingGame);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("My Game");
    }

    #endregion

    #region Null Query Tests

    [Fact]
    public async Task Handle_NullQuery_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _handler.Handle(null!, CancellationToken.None));
    }

    #endregion

    #region DTO Mapping Tests

    [Fact]
    public async Task Handle_RetrievedGame_MapsAllFieldsToDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var existingGame = PrivateGame.CreateFromBgg(
            ownerId: userId,
            bggId: 55555,
            title: "Complete Mapping Test",
            yearPublished: 2021,
            description: "Full description for mapping",
            minPlayers: 1,
            maxPlayers: 8,
            playingTimeMinutes: 180,
            minAge: 10,
            complexityRating: 2.8m,
            imageUrl: "https://example.com/full.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg");

        typeof(PrivateGame).GetProperty("Id")!.SetValue(existingGame, gameId);

        var query = new GetPrivateGameQuery(gameId, userId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingGame);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(gameId);
        result.OwnerId.Should().Be(userId);
        result.Source.Should().Be("BoardGameGeek");
        result.BggId.Should().Be(55555);
        result.Title.Should().Be("Complete Mapping Test");
        result.YearPublished.Should().Be(2021);
        result.Description.Should().Be("Full description for mapping");
        result.MinPlayers.Should().Be(1);
        result.MaxPlayers.Should().Be(8);
        result.PlayingTimeMinutes.Should().Be(180);
        result.MinAge.Should().Be(10);
        result.ComplexityRating.Should().Be(2.8m);
        result.ImageUrl.Should().Be("https://example.com/full.jpg");
        result.ThumbnailUrl.Should().Be("https://example.com/thumb.jpg");
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.UpdatedAt.Should().BeNull();
        result.BggSyncedAt.Should().NotBeNull();
        result.CanProposeToCatalog.Should().BeTrue();
    }

    #endregion

    #region Query Record Tests

    [Fact]
    public void GetPrivateGameQuery_CreatesWithRequiredProperties()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act
        var query = new GetPrivateGameQuery(gameId, userId);

        // Assert
        query.PrivateGameId.Should().Be(gameId);
        query.UserId.Should().Be(userId);
    }

    #endregion
}
