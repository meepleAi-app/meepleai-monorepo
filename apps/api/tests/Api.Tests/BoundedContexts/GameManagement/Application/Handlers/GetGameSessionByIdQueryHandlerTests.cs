using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Moq;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;
using Api.SharedKernel.Application;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.GameManagement.Application.Mappers;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Tests for GetGameSessionByIdQueryHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetGameSessionByIdQueryHandlerTests
{
    private readonly Mock<IGameSessionRepository> _sessionRepositoryMock;
    private readonly Mock<IGameCoreDataProvider> _gameCoreDataMock;
    private readonly Mock<IHistorySessionScoreProvider> _scoreProviderMock;
    private readonly GetGameSessionByIdQueryHandler _handler;

    public GetGameSessionByIdQueryHandlerTests()
    {
        _sessionRepositoryMock = new Mock<IGameSessionRepository>();
        _gameCoreDataMock = new Mock<IGameCoreDataProvider>();
        _scoreProviderMock = new Mock<IHistorySessionScoreProvider>();
        _handler = new GetGameSessionByIdQueryHandler(
            _sessionRepositoryMock.Object, _gameCoreDataMock.Object, _scoreProviderMock.Object);
    }

    private static GameCoreData MakeCoreData(string title = "Catan") =>
        GameCoreData.Create(title, 1995, 3, 4, 90, 10);

    [Fact]
    public async Task Handle_PopulatesSlugNameAndScoreboard()
    {
        var gameId = Guid.NewGuid();
        var session = CreateSession(gameId);
        var pid = Guid.NewGuid();
        _sessionRepositoryMock.Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        _gameCoreDataMock.Setup(p => p.GetCoreDataAsync(GameRef.Shared(gameId), It.IsAny<CancellationToken>())).ReturnsAsync(MakeCoreData("Catan"));
        _scoreProviderMock.Setup(p => p.GetScoreboardAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessionScoreboard("Points", "{\"scores\":[]}",
                new List<ScorePlayerReadModel> { new(pid, "Alice", "Red") }));

        var result = await _handler.Handle(new GetGameSessionByIdQuery(session.Id), TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.GameSlug.Should().Be("catan");
        result.GameName.Should().Be("Catan");
        result.ScoringType.Should().Be("Points");
        result.ScorePlayers.Should().ContainSingle(sp => sp.Id == pid && sp.DisplayName == "Alice" && sp.Color == "Red");
    }

    [Fact]
    public async Task Handle_NoScoreboard_LeavesScoreFieldsNull()
    {
        var gameId = Guid.NewGuid();
        var session = CreateSession(gameId);
        _sessionRepositoryMock.Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        _gameCoreDataMock.Setup(p => p.GetCoreDataAsync(It.IsAny<GameRef>(), It.IsAny<CancellationToken>())).ReturnsAsync((GameCoreData?)null);
        _scoreProviderMock.Setup(p => p.GetScoreboardAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((SessionScoreboard?)null);

        var result = await _handler.Handle(new GetGameSessionByIdQuery(session.Id), TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.GameSlug.Should().BeNull();
        result.GameName.Should().BeNull();
        result.ScoringType.Should().BeNull();
        result.ScorePlayers.Should().BeNull();
    }

    [Fact]
    public void ToDto_LeavesSummaryOnlyFieldsNull()
    {
        var session = CreateSession(Guid.NewGuid());

        var dto = session.ToDto();

        dto.GameSlug.Should().BeNull();
        dto.GameName.Should().BeNull();
        dto.ScorePlayers.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithExistingSession_ReturnsSessionDto()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var session = CreateSession(gameId);
        var query = new GetGameSessionByIdQuery(session.Id);

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(session.Id);
        result.GameId.Should().Be(gameId);
        result.Status.Should().Be("Setup");
        result.Players.Should().NotBeNull();
        result.Players.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_WithNonExistentSession_ReturnsNull()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var query = new GetGameSessionByIdQuery(sessionId);

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameSession?)null);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_MapsPlayersCorrectly()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var players = new List<SessionPlayer>
        {
            new SessionPlayer("Player 1", 1, "Red"),
            new SessionPlayer("Player 2", 2, "Blue"),
            new SessionPlayer("Player 3", 3, "Green")
        };

        var session = new GameSession(
            id: Guid.NewGuid(),
            gameId: gameId,
            players: players
        );

        var query = new GetGameSessionByIdQuery(session.Id);

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Players.Count.Should().Be(3);
        result.Players[0].PlayerName.Should().Be("Player 1");
        result.Players[0].PlayerOrder.Should().Be(1);
        result.Players[0].Color.Should().Be("Red");
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PassesToRepository()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var query = new GetGameSessionByIdQuery(sessionId);
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, token))
            .ReturnsAsync((GameSession?)null);

        // Act
        await _handler.Handle(query, token);

        // Assert
        _sessionRepositoryMock.Verify(r => r.GetByIdAsync(sessionId, token), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNullQuery_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act =
            () => _handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Handle_WithCompletedSession_MapsDurationCorrectly()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var session = CreateSession(gameId);
        session.Start();
        session.Complete("Player 1");
        var query = new GetGameSessionByIdQuery(session.Id);

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("Completed");
        result.WinnerName.Should().Be("Player 1");
        result.CompletedAt.Should().NotBeNull();
    }

    private static GameSession CreateSession(Guid gameId)
    {
        var players = new List<SessionPlayer>
        {
            new SessionPlayer("Player 1", 1, "Red")
        };

        return new GameSession(
            id: Guid.NewGuid(),
            gameId: gameId,
            players: players
        );
    }
}
