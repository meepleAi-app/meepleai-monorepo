using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.SharedKernel.Application;
using Api.SharedKernel.Domain.ValueObjects;
using Moq;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;
using Api.Infrastructure;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Comprehensive tests for GetGameByIdQueryHandler.
/// Tests single game retrieval and DTO mapping.
/// Issue #1320 (P2c): Migrated from IGameRepository to IGameCoreDataProvider + SharedGames EF.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetGameByIdQueryHandlerTests
{
    private readonly Mock<IGameCoreDataProvider> _gameCoreDataMock;
    private readonly GetGameByIdQueryHandler _handler;

    private static GameCoreData MakeCoreData(
        string title = "Test Game",
        int yearPublished = 1995,
        int minPlayers = 3,
        int maxPlayers = 4,
        int playingTimeMinutes = 60,
        int? bggId = null,
        string? imageUrl = null) =>
        GameCoreData.Create(title, yearPublished, minPlayers, maxPlayers, playingTimeMinutes, 10,
            imageUrl: imageUrl, bggId: bggId);

    public GetGameByIdQueryHandlerTests()
    {
        _gameCoreDataMock = new Mock<IGameCoreDataProvider>();

        // Inject a stub MeepleAiDbContext — not needed by the handler but satisfies DI if constructor requires it.
        // GetGameByIdQueryHandler only uses IGameCoreDataProvider.
        _handler = new GetGameByIdQueryHandler(_gameCoreDataMock.Object);
    }

    [Fact]
    public async Task Handle_ExistingGame_ReturnsMappedDto()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var coreData = MakeCoreData("Catan", 1995, 3, 4, 90);

        _gameCoreDataMock
            .Setup(r => r.GetCoreDataAsync(GameRef.Shared(gameId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(coreData);

        var query = new GetGameByIdQuery(gameId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(gameId);
        result.Title.Should().Be("Catan");
        result.YearPublished.Should().Be(1995);
        result.MinPlayers.Should().Be(3);
        result.MaxPlayers.Should().Be(4);
        result.MinPlayTimeMinutes.Should().Be(90);
        result.MaxPlayTimeMinutes.Should().Be(90);

        _gameCoreDataMock.Verify(
            r => r.GetCoreDataAsync(GameRef.Shared(gameId), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_GameWithBggLink_IncludesBggId()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var coreData = MakeCoreData("Pandemic", bggId: 30549);

        _gameCoreDataMock
            .Setup(r => r.GetCoreDataAsync(GameRef.Shared(gameId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(coreData);

        var query = new GetGameByIdQuery(gameId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.BggId.Should().Be(30549);
    }

    [Fact]
    public async Task Handle_GameWithAllDetails_MapsAllProperties()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var coreData = MakeCoreData("Wingspan", 2019, 1, 5, 70, bggId: 266192);

        _gameCoreDataMock
            .Setup(r => r.GetCoreDataAsync(GameRef.Shared(gameId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(coreData);

        var query = new GetGameByIdQuery(gameId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.YearPublished.Should().NotBeNull();
        result.MinPlayers.Should().NotBeNull();
        result.MaxPlayers.Should().NotBeNull();
        result.MinPlayTimeMinutes.Should().NotBeNull();
        result.MaxPlayTimeMinutes.Should().NotBeNull();
        result.BggId.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_NonExistentGame_ReturnsNull()
    {
        // Arrange
        var gameId = Guid.NewGuid();

        _gameCoreDataMock
            .Setup(r => r.GetCoreDataAsync(GameRef.Shared(gameId), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameCoreData?)null);

        var query = new GetGameByIdQuery(gameId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();

        _gameCoreDataMock.Verify(
            r => r.GetCoreDataAsync(GameRef.Shared(gameId), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_EmptyGuid_ReturnsNull()
    {
        // Arrange — Guid.Empty is rejected by the handler short-circuit (GameRef.Shared(Guid.Empty)
        // would throw ArgumentException), so the provider is never consulted.
        var query = new GetGameByIdQuery(Guid.Empty);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
        _gameCoreDataMock.Verify(
            r => r.GetCoreDataAsync(It.IsAny<GameRef>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PassesToProvider()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var coreData = MakeCoreData("Test Game");

        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        _gameCoreDataMock
            .Setup(r => r.GetCoreDataAsync(GameRef.Shared(gameId), cancellationToken))
            .ReturnsAsync(coreData);

        var query = new GetGameByIdQuery(gameId);

        // Act
        var result = await _handler.Handle(query, cancellationToken);

        // Assert
        result.Should().NotBeNull();
        _gameCoreDataMock.Verify(
            r => r.GetCoreDataAsync(GameRef.Shared(gameId), cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ReturnsCreatedAtNotDefault()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var coreData = MakeCoreData("Old Game");

        _gameCoreDataMock
            .Setup(r => r.GetCoreDataAsync(GameRef.Shared(gameId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(coreData);

        var query = new GetGameByIdQuery(gameId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.CreatedAt.Should().NotBe(default(DateTime));
    }
}
