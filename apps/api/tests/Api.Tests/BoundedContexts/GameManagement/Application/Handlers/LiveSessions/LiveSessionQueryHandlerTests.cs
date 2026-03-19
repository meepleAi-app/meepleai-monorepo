using Api.BoundedContexts.GameManagement.Application.DTOs.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers.LiveSessions;

/// <summary>
/// Unit tests for all live session query handlers.
/// Covers GetLiveSession, GetLiveSessionByCode, GetUserActiveSessions,
/// GetSessionScores, and GetSessionPlayers query handlers.
/// Issue #4749: CQRS queries for live sessions.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class LiveSessionQueryHandlerTests
{
    private readonly Mock<ILiveSessionRepository> _sessionRepositoryMock;

    public LiveSessionQueryHandlerTests()
    {
        _sessionRepositoryMock = new Mock<ILiveSessionRepository>();
    }

    // ========================================================================
    // Helper Methods
    // ========================================================================

    private static LiveGameSession CreateTestSession(
        Guid? id = null,
        Guid? userId = null,
        string gameName = "Test Game")
    {
        return LiveGameSession.Create(
            id ?? Guid.NewGuid(),
            userId ?? Guid.NewGuid(),
            gameName,
            TimeProvider.System);
    }

    private static LiveGameSession CreateSessionWithPlayers(
        Guid? id = null,
        Guid? userId = null,
        int playerCount = 2,
        bool start = false)
    {
        var session = CreateTestSession(id, userId);

        var colors = new[] { PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green, PlayerColor.Yellow };
        for (var i = 0; i < playerCount && i < colors.Length; i++)
        {
            session.AddPlayer(null, $"Player {i + 1}", colors[i], TimeProvider.System);
        }

        if (start)
        {
            session.MoveToSetup(TimeProvider.System);
            session.Start(TimeProvider.System);
        }

        return session;
    }

    private static LiveGameSession CreateSessionWithScores(
        Guid? id = null,
        Guid? userId = null)
    {
        var session = CreateTestSession(id, userId);

        var player1 = session.AddPlayer(null, "Player 1", PlayerColor.Red, TimeProvider.System);
        var player2 = session.AddPlayer(null, "Player 2", PlayerColor.Blue, TimeProvider.System);

        // Must start the session to record scores
        session.MoveToSetup(TimeProvider.System);
        session.Start(TimeProvider.System);

        session.RecordScore(player1.Id, 1, "points", 10, TimeProvider.System);
        session.RecordScore(player2.Id, 1, "points", 15, TimeProvider.System);
        session.RecordScore(player1.Id, 2, "points", 20, TimeProvider.System);

        return session;
    }

    // ========================================================================
    // GetLiveSessionQueryHandler Tests
    // ========================================================================

    [Fact]
    public async Task GetLiveSession_ExistingSession_ReturnsMappedDto()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var session = CreateTestSession(sessionId, userId, "Catan");

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = new GetLiveSessionQueryHandler(_sessionRepositoryMock.Object);
        var query = new GetLiveSessionQuery(sessionId);

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(sessionId, result.Id);
        Assert.Equal("Catan", result.GameName);
        Assert.Equal(userId, result.CreatedByUserId);
        Assert.Equal(LiveSessionStatus.Created, result.Status);
        Assert.NotNull(result.SessionCode);
        Assert.Equal(6, result.SessionCode.Length);
        Assert.Equal(0, result.CurrentTurnIndex);
        Assert.Empty(result.Players);
        Assert.Empty(result.Teams);
        Assert.Empty(result.RoundScores);
        Assert.NotNull(result.ScoringConfig);
        Assert.Contains("points", result.ScoringConfig.EnabledDimensions);

        _sessionRepositoryMock.Verify(
            r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetLiveSession_SessionWithPlayers_MapsPlayersDtoCorrectly()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        // Must start the session so GetCurrentTurnPlayerId() in MapToDto can resolve turn index
        var session = CreateSessionWithPlayers(sessionId, playerCount: 2, start: true);

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = new GetLiveSessionQueryHandler(_sessionRepositoryMock.Object);
        var query = new GetLiveSessionQuery(sessionId);

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Players.Count);

        var player1 = result.Players.First(p => p.DisplayName == "Player 1");
        Assert.Equal(PlayerColor.Red, player1.Color);
        Assert.Equal(PlayerRole.Host, player1.Role);
        Assert.True(player1.IsActive);
        Assert.Null(player1.UserId);

        var player2 = result.Players.First(p => p.DisplayName == "Player 2");
        Assert.Equal(PlayerColor.Blue, player2.Color);
        Assert.Equal(PlayerRole.Player, player2.Role);
        Assert.True(player2.IsActive);
    }

    [Fact]
    public async Task GetLiveSession_SessionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LiveGameSession?)null);

        var handler = new GetLiveSessionQueryHandler(_sessionRepositoryMock.Object);
        var query = new GetLiveSessionQuery(sessionId);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(query, TestContext.Current.CancellationToken));

        Assert.Equal("LiveGameSession", exception.ResourceType);
        Assert.Equal(sessionId.ToString(), exception.ResourceId);
    }

    [Fact]
    public async Task GetLiveSession_NullQuery_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new GetLiveSessionQueryHandler(_sessionRepositoryMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => handler.Handle(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetLiveSession_WithCancellationToken_PassesToRepository()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateTestSession(sessionId);

        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, cancellationToken))
            .ReturnsAsync(session);

        var handler = new GetLiveSessionQueryHandler(_sessionRepositoryMock.Object);
        var query = new GetLiveSessionQuery(sessionId);

        // Act
        var result = await handler.Handle(query, cancellationToken);

        // Assert
        Assert.NotNull(result);
        _sessionRepositoryMock.Verify(
            r => r.GetByIdAsync(sessionId, cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task GetLiveSession_SessionWithScores_MapsScoringDataCorrectly()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateSessionWithScores(sessionId);

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = new GetLiveSessionQueryHandler(_sessionRepositoryMock.Object);
        var query = new GetLiveSessionQuery(sessionId);

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.RoundScores.Count);
        Assert.All(result.RoundScores, score =>
        {
            Assert.Equal("points", score.Dimension);
            Assert.True(score.Round >= 1);
        });
    }

    // ========================================================================
    // GetLiveSessionByCodeQueryHandler Tests
    // ========================================================================

    [Fact]
    public async Task GetLiveSessionByCode_ExistingSession_ReturnsMappedDto()
    {
        // Arrange
        var session = CreateTestSession(gameName: "Ticket to Ride");
        var sessionCode = session.SessionCode;

        _sessionRepositoryMock
            .Setup(r => r.GetByCodeAsync(sessionCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = new GetLiveSessionByCodeQueryHandler(_sessionRepositoryMock.Object);
        var query = new GetLiveSessionByCodeQuery(sessionCode);

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(session.Id, result.Id);
        Assert.Equal(sessionCode, result.SessionCode);
        Assert.Equal("Ticket to Ride", result.GameName);
        Assert.Equal(LiveSessionStatus.Created, result.Status);

        _sessionRepositoryMock.Verify(
            r => r.GetByCodeAsync(sessionCode, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetLiveSessionByCode_SessionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var sessionCode = "ABCDEF";

        _sessionRepositoryMock
            .Setup(r => r.GetByCodeAsync(sessionCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LiveGameSession?)null);

        var handler = new GetLiveSessionByCodeQueryHandler(_sessionRepositoryMock.Object);
        var query = new GetLiveSessionByCodeQuery(sessionCode);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(query, TestContext.Current.CancellationToken));

        Assert.Equal("LiveGameSession", exception.ResourceType);
        Assert.Equal($"code:{sessionCode}", exception.ResourceId);
    }

    [Fact]
    public async Task GetLiveSessionByCode_NullQuery_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new GetLiveSessionByCodeQueryHandler(_sessionRepositoryMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => handler.Handle(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetLiveSessionByCode_SessionWithPlayers_MapsPlayersCorrectly()
    {
        // Arrange
        // Must start the session so GetCurrentTurnPlayerId() in MapToDto can resolve turn index
        var session = CreateSessionWithPlayers(playerCount: 3, start: true);
        var sessionCode = session.SessionCode;

        _sessionRepositoryMock
            .Setup(r => r.GetByCodeAsync(sessionCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = new GetLiveSessionByCodeQueryHandler(_sessionRepositoryMock.Object);
        var query = new GetLiveSessionByCodeQuery(sessionCode);

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Players.Count);
    }

    // ========================================================================
    // GetUserActiveSessionsQueryHandler Tests
    // ========================================================================

    [Fact]
    public async Task GetUserActiveSessions_WithActiveSessions_ReturnsSummaryDtoList()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var session1 = CreateTestSession(userId: userId, gameName: "Catan");
        var session2 = CreateTestSession(userId: userId, gameName: "Pandemic");

        _sessionRepositoryMock
            .Setup(r => r.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LiveGameSession> { session1, session2 });

        var handler = new GetUserActiveSessionsQueryHandler(_sessionRepositoryMock.Object);
        var query = new GetUserActiveSessionsQuery(userId);

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);

        var summary1 = result.First(s => s.GameName == "Catan");
        Assert.Equal(session1.Id, summary1.Id);
        Assert.Equal(session1.SessionCode, summary1.SessionCode);
        Assert.Equal(LiveSessionStatus.Created, summary1.Status);
        Assert.Equal(0, summary1.PlayerCount);
        Assert.Equal(0, summary1.CurrentTurnIndex);

        var summary2 = result.First(s => s.GameName == "Pandemic");
        Assert.Equal(session2.Id, summary2.Id);

        _sessionRepositoryMock.Verify(
            r => r.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetUserActiveSessions_NoActiveSessions_ReturnsEmptyList()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _sessionRepositoryMock
            .Setup(r => r.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LiveGameSession>());

        var handler = new GetUserActiveSessionsQueryHandler(_sessionRepositoryMock.Object);
        var query = new GetUserActiveSessionsQuery(userId);

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetUserActiveSessions_NullQuery_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new GetUserActiveSessionsQueryHandler(_sessionRepositoryMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => handler.Handle(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetUserActiveSessions_SessionsWithPlayers_MapsPlayerCountCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var session = CreateSessionWithPlayers(userId: userId, playerCount: 3);

        _sessionRepositoryMock
            .Setup(r => r.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LiveGameSession> { session });

        var handler = new GetUserActiveSessionsQueryHandler(_sessionRepositoryMock.Object);
        var query = new GetUserActiveSessionsQuery(userId);

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(result);
        Assert.Equal(3, result[0].PlayerCount);
    }

    [Fact]
    public async Task GetUserActiveSessions_MapsTimestampsCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var session = CreateTestSession(userId: userId);

        _sessionRepositoryMock
            .Setup(r => r.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LiveGameSession> { session });

        var handler = new GetUserActiveSessionsQueryHandler(_sessionRepositoryMock.Object);
        var query = new GetUserActiveSessionsQuery(userId);

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(result);
        Assert.Equal(session.CreatedAt, result[0].CreatedAt);
        Assert.Equal(session.UpdatedAt, result[0].UpdatedAt);
        Assert.Equal(session.LastSavedAt, result[0].LastSavedAt);
    }

    // ========================================================================
    // GetSessionScoresQueryHandler Tests
    // ========================================================================

    [Fact]
    public async Task GetSessionScores_SessionWithScores_ReturnsMappedScoreDtoList()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateSessionWithScores(sessionId);

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = new GetSessionScoresQueryHandler(_sessionRepositoryMock.Object);
        var query = new GetSessionScoresQuery(sessionId);

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);

        // Verify score values are mapped correctly
        Assert.Contains(result, s => s.Value == 10 && s.Round == 1);
        Assert.Contains(result, s => s.Value == 15 && s.Round == 1);
        Assert.Contains(result, s => s.Value == 20 && s.Round == 2);

        Assert.All(result, score =>
        {
            Assert.Equal("points", score.Dimension);
            Assert.NotEqual(Guid.Empty, score.PlayerId);
            Assert.NotEqual(default, score.RecordedAt);
        });

        _sessionRepositoryMock.Verify(
            r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetSessionScores_SessionWithNoScores_ReturnsEmptyList()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateTestSession(sessionId);

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = new GetSessionScoresQueryHandler(_sessionRepositoryMock.Object);
        var query = new GetSessionScoresQuery(sessionId);

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetSessionScores_SessionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LiveGameSession?)null);

        var handler = new GetSessionScoresQueryHandler(_sessionRepositoryMock.Object);
        var query = new GetSessionScoresQuery(sessionId);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(query, TestContext.Current.CancellationToken));

        Assert.Equal("LiveGameSession", exception.ResourceType);
        Assert.Equal(sessionId.ToString(), exception.ResourceId);
    }

    [Fact]
    public async Task GetSessionScores_NullQuery_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new GetSessionScoresQueryHandler(_sessionRepositoryMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => handler.Handle(null!, TestContext.Current.CancellationToken));
    }

    // ========================================================================
    // GetSessionPlayersQueryHandler Tests
    // ========================================================================

    [Fact]
    public async Task GetSessionPlayers_SessionWithPlayers_ReturnsMappedPlayerDtoList()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateSessionWithPlayers(sessionId, playerCount: 3);

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = new GetSessionPlayersQueryHandler(_sessionRepositoryMock.Object);
        var query = new GetSessionPlayersQuery(sessionId);

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);

        // First player should be Host (auto-assigned)
        var host = result.First(p => p.DisplayName == "Player 1");
        Assert.Equal(PlayerRole.Host, host.Role);
        Assert.Equal(PlayerColor.Red, host.Color);
        Assert.True(host.IsActive);
        Assert.Null(host.UserId);
        Assert.NotEqual(Guid.Empty, host.Id);
        Assert.NotEqual(default, host.JoinedAt);

        // Subsequent players should be Player role
        var player2 = result.First(p => p.DisplayName == "Player 2");
        Assert.Equal(PlayerRole.Player, player2.Role);
        Assert.Equal(PlayerColor.Blue, player2.Color);

        var player3 = result.First(p => p.DisplayName == "Player 3");
        Assert.Equal(PlayerRole.Player, player3.Role);
        Assert.Equal(PlayerColor.Green, player3.Color);

        _sessionRepositoryMock.Verify(
            r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetSessionPlayers_SessionWithNoPlayers_ReturnsEmptyList()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateTestSession(sessionId);

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = new GetSessionPlayersQueryHandler(_sessionRepositoryMock.Object);
        var query = new GetSessionPlayersQuery(sessionId);

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetSessionPlayers_SessionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LiveGameSession?)null);

        var handler = new GetSessionPlayersQueryHandler(_sessionRepositoryMock.Object);
        var query = new GetSessionPlayersQuery(sessionId);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(query, TestContext.Current.CancellationToken));

        Assert.Equal("LiveGameSession", exception.ResourceType);
        Assert.Equal(sessionId.ToString(), exception.ResourceId);
    }

    [Fact]
    public async Task GetSessionPlayers_NullQuery_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new GetSessionPlayersQueryHandler(_sessionRepositoryMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => handler.Handle(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetSessionPlayers_PlayerWithRegisteredUser_MapsUserIdCorrectly()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var registeredUserId = Guid.NewGuid();
        var session = CreateTestSession(sessionId);
        session.AddPlayer(registeredUserId, "Registered User", PlayerColor.Red, TimeProvider.System);

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = new GetSessionPlayersQueryHandler(_sessionRepositoryMock.Object);
        var query = new GetSessionPlayersQuery(sessionId);

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(result);
        Assert.Equal(registeredUserId, result[0].UserId);
        Assert.Equal("Registered User", result[0].DisplayName);
    }

    // ========================================================================
    // Constructor Null Guard Tests
    // ========================================================================

    [Fact]
    public void GetLiveSessionQueryHandler_NullRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new GetLiveSessionQueryHandler(null!));
    }

    [Fact]
    public void GetLiveSessionByCodeQueryHandler_NullRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new GetLiveSessionByCodeQueryHandler(null!));
    }

    [Fact]
    public void GetUserActiveSessionsQueryHandler_NullRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new GetUserActiveSessionsQueryHandler(null!));
    }

    [Fact]
    public void GetSessionScoresQueryHandler_NullRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new GetSessionScoresQueryHandler(null!));
    }

    [Fact]
    public void GetSessionPlayersQueryHandler_NullRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new GetSessionPlayersQueryHandler(null!));
    }
}
