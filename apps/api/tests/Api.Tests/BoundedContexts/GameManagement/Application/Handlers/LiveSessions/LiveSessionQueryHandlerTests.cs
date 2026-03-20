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
using FluentAssertions;

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
        result.Should().NotBeNull();
        result.Id.Should().Be(sessionId);
        result.GameName.Should().Be("Catan");
        result.CreatedByUserId.Should().Be(userId);
        result.Status.Should().Be(LiveSessionStatus.Created);
        result.SessionCode.Should().NotBeNull();
        result.SessionCode.Length.Should().Be(6);
        result.CurrentTurnIndex.Should().Be(0);
        result.Players.Should().BeEmpty();
        result.Teams.Should().BeEmpty();
        result.RoundScores.Should().BeEmpty();
        result.ScoringConfig.Should().NotBeNull();
        result.ScoringConfig.EnabledDimensions.Should().Contain("points");

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
        result.Should().NotBeNull();
        result.Players.Count.Should().Be(2);

        var player1 = result.Players.First(p => p.DisplayName == "Player 1");
        player1.Color.Should().Be(PlayerColor.Red);
        player1.Role.Should().Be(PlayerRole.Host);
        (player1.IsActive).Should().BeTrue();
        player1.UserId.Should().BeNull();

        var player2 = result.Players.First(p => p.DisplayName == "Player 2");
        player2.Color.Should().Be(PlayerColor.Blue);
        player2.Role.Should().Be(PlayerRole.Player);
        (player2.IsActive).Should().BeTrue();
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
        var act = 
            () => handler.Handle(query, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<NotFoundException>()).Which;

        exception.ResourceType.Should().Be("LiveGameSession");
        exception.ResourceId.Should().Be(sessionId.ToString());
    }

    [Fact]
    public async Task GetLiveSession_NullQuery_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new GetLiveSessionQueryHandler(_sessionRepositoryMock.Object);

        // Act & Assert
        var act = 
            () => handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
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
        result.Should().NotBeNull();
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
        result.Should().NotBeNull();
        result.RoundScores.Count.Should().Be(3);
        result.RoundScores.Should().AllSatisfy(score =>
        {
            score.Dimension.Should().Be("points");
            (score.Round >= 1).Should().BeTrue();
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
        result.Should().NotBeNull();
        result.Id.Should().Be(session.Id);
        result.SessionCode.Should().Be(sessionCode);
        result.GameName.Should().Be("Ticket to Ride");
        result.Status.Should().Be(LiveSessionStatus.Created);

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
        var act = 
            () => handler.Handle(query, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<NotFoundException>()).Which;

        exception.ResourceType.Should().Be("LiveGameSession");
        exception.ResourceId.Should().Be($"code:{sessionCode}");
    }

    [Fact]
    public async Task GetLiveSessionByCode_NullQuery_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new GetLiveSessionByCodeQueryHandler(_sessionRepositoryMock.Object);

        // Act & Assert
        var act = 
            () => handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
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
        result.Should().NotBeNull();
        result.Players.Count.Should().Be(3);
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
        result.Should().NotBeNull();
        result.Count.Should().Be(2);

        var summary1 = result.First(s => s.GameName == "Catan");
        summary1.Id.Should().Be(session1.Id);
        summary1.SessionCode.Should().Be(session1.SessionCode);
        summary1.Status.Should().Be(LiveSessionStatus.Created);
        summary1.PlayerCount.Should().Be(0);
        summary1.CurrentTurnIndex.Should().Be(0);

        var summary2 = result.First(s => s.GameName == "Pandemic");
        summary2.Id.Should().Be(session2.Id);

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
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserActiveSessions_NullQuery_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new GetUserActiveSessionsQueryHandler(_sessionRepositoryMock.Object);

        // Act & Assert
        var act = 
            () => handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
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
        result.Should().ContainSingle();
        result[0].PlayerCount.Should().Be(3);
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
        result.Should().ContainSingle();
        result[0].CreatedAt.Should().Be(session.CreatedAt);
        result[0].UpdatedAt.Should().Be(session.UpdatedAt);
        result[0].LastSavedAt.Should().Be(session.LastSavedAt);
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
        result.Should().NotBeNull();
        result.Count.Should().Be(3);

        // Verify score values are mapped correctly
        result.Should().Contain(s => s.Value == 10 && s.Round == 1);
        result.Should().Contain(s => s.Value == 15 && s.Round == 1);
        result.Should().Contain(s => s.Value == 20 && s.Round == 2);

        result.Should().AllSatisfy(score =>
        {
            score.Dimension.Should().Be("points");
            score.PlayerId.Should().NotBe(Guid.Empty);
            score.RecordedAt.Should().NotBe(default);
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
        result.Should().NotBeNull();
        result.Should().BeEmpty();
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
        var act = 
            () => handler.Handle(query, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<NotFoundException>()).Which;

        exception.ResourceType.Should().Be("LiveGameSession");
        exception.ResourceId.Should().Be(sessionId.ToString());
    }

    [Fact]
    public async Task GetSessionScores_NullQuery_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new GetSessionScoresQueryHandler(_sessionRepositoryMock.Object);

        // Act & Assert
        var act = 
            () => handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
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
        result.Should().NotBeNull();
        result.Count.Should().Be(3);

        // First player should be Host (auto-assigned)
        var host = result.First(p => p.DisplayName == "Player 1");
        host.Role.Should().Be(PlayerRole.Host);
        host.Color.Should().Be(PlayerColor.Red);
        (host.IsActive).Should().BeTrue();
        host.UserId.Should().BeNull();
        host.Id.Should().NotBe(Guid.Empty);
        host.JoinedAt.Should().NotBe(default);

        // Subsequent players should be Player role
        var player2 = result.First(p => p.DisplayName == "Player 2");
        player2.Role.Should().Be(PlayerRole.Player);
        player2.Color.Should().Be(PlayerColor.Blue);

        var player3 = result.First(p => p.DisplayName == "Player 3");
        player3.Role.Should().Be(PlayerRole.Player);
        player3.Color.Should().Be(PlayerColor.Green);

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
        result.Should().NotBeNull();
        result.Should().BeEmpty();
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
        var act = 
            () => handler.Handle(query, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<NotFoundException>()).Which;

        exception.ResourceType.Should().Be("LiveGameSession");
        exception.ResourceId.Should().Be(sessionId.ToString());
    }

    [Fact]
    public async Task GetSessionPlayers_NullQuery_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new GetSessionPlayersQueryHandler(_sessionRepositoryMock.Object);

        // Act & Assert
        var act = 
            () => handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
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
        result.Should().ContainSingle();
        result[0].UserId.Should().Be(registeredUserId);
        result[0].DisplayName.Should().Be("Registered User");
    }

    // ========================================================================
    // Constructor Null Guard Tests
    // ========================================================================

    [Fact]
    public void GetLiveSessionQueryHandler_NullRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = 
            () => new GetLiveSessionQueryHandler(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetLiveSessionByCodeQueryHandler_NullRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = 
            () => new GetLiveSessionByCodeQueryHandler(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetUserActiveSessionsQueryHandler_NullRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = 
            () => new GetUserActiveSessionsQueryHandler(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetSessionScoresQueryHandler_NullRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = 
            () => new GetSessionScoresQueryHandler(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetSessionPlayersQueryHandler_NullRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = 
            () => new GetSessionPlayersQueryHandler(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
