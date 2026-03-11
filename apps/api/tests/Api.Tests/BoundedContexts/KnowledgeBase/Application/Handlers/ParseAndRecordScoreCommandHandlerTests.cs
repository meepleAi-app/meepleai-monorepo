using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.DTOs.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Handlers;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Tests.Constants;
using MediatR;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Tests for ParseAndRecordScoreCommandHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class ParseAndRecordScoreCommandHandlerTests
{
    private readonly Mock<IStateParser> _mockStateParser;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<IPlayerNameResolutionService> _mockPlayerResolution;
    private readonly ParseAndRecordScoreCommandHandler _handler;

    private readonly Guid _sessionId = Guid.NewGuid();
    private readonly Guid _playerId = Guid.NewGuid();

    public ParseAndRecordScoreCommandHandlerTests()
    {
        _mockStateParser = new Mock<IStateParser>();
        _mockMediator = new Mock<IMediator>();
        _mockPlayerResolution = new Mock<IPlayerNameResolutionService>();
        _handler = new ParseAndRecordScoreCommandHandler(
            _mockStateParser.Object,
            _mockMediator.Object,
            _mockPlayerResolution.Object);

        // Default: return active players
        var players = new List<LiveSessionPlayerDto>
        {
            new(_playerId, Guid.NewGuid(), "Marco Rossi", null, PlayerColor.Red, PlayerRole.Player,
                null, 0, 1, DateTime.UtcNow, true)
        };

        _mockMediator
            .Setup(m => m.Send(It.IsAny<GetSessionPlayersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(players.AsReadOnly());

        // Default: return session with turn index 1
        var sessionDto = CreateLiveSessionDto(currentTurnIndex: 1);
        _mockMediator
            .Setup(m => m.Send(It.IsAny<GetLiveSessionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionDto);
    }

    [Fact]
    public async Task Handle_HighConfidenceAutoRecord_RecordsScoreAndReturnsRecorded()
    {
        // Arrange
        var extraction = StateExtractionResult.Create(
            changeType: StateChangeType.ScoreChange,
            originalMessage: "Marco ha 5 punti",
            confidence: 0.95f,
            extractedState: new Dictionary<string, object> { { "score", 5 }, { "dimension", "points" } },
            playerName: "Marco",
            requiresConfirmation: false);

        _mockStateParser
            .Setup(p => p.ParseAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(extraction);

        _mockPlayerResolution
            .Setup(r => r.ResolvePlayer("Marco", It.IsAny<IReadOnlyDictionary<Guid, string>>()))
            .Returns(PlayerResolutionResult.Resolved(_playerId, "Marco Rossi"));

        var command = new ParseAndRecordScoreCommand
        {
            SessionId = _sessionId,
            Message = "Marco ha 5 punti",
            AutoRecord = true
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal("recorded", result.Status);
        Assert.Equal("Marco Rossi", result.PlayerName);
        Assert.Equal(_playerId, result.PlayerId);
        Assert.Equal(5, result.Value);
        Assert.Equal("points", result.Dimension);
        Assert.False(result.RequiresConfirmation);

        _mockMediator.Verify(m => m.Send(
            It.Is<RecordLiveSessionScoreCommand>(c =>
                c.SessionId == _sessionId &&
                c.PlayerId == _playerId &&
                c.Value == 5 &&
                c.Dimension == "points"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_LowConfidence_ReturnsParsedWithConfirmation()
    {
        // Arrange
        var extraction = StateExtractionResult.Create(
            changeType: StateChangeType.ScoreChange,
            originalMessage: "Marco forse 5 punti",
            confidence: 0.5f,
            extractedState: new Dictionary<string, object> { { "score", 5 }, { "dimension", "points" } },
            playerName: "Marco",
            requiresConfirmation: true);

        _mockStateParser
            .Setup(p => p.ParseAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(extraction);

        _mockPlayerResolution
            .Setup(r => r.ResolvePlayer("Marco", It.IsAny<IReadOnlyDictionary<Guid, string>>()))
            .Returns(PlayerResolutionResult.Resolved(_playerId, "Marco Rossi"));

        var command = new ParseAndRecordScoreCommand
        {
            SessionId = _sessionId,
            Message = "Marco forse 5 punti",
            AutoRecord = true
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal("parsed", result.Status);
        Assert.True(result.RequiresConfirmation);
        Assert.Equal(_playerId, result.PlayerId);

        _mockMediator.Verify(m => m.Send(
            It.IsAny<RecordLiveSessionScoreCommand>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NoScoreDetected_ReturnsUnrecognized()
    {
        // Arrange
        var extraction = StateExtractionResult.NoChange("ciao come stai");

        _mockStateParser
            .Setup(p => p.ParseAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(extraction);

        var command = new ParseAndRecordScoreCommand
        {
            SessionId = _sessionId,
            Message = "ciao come stai"
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal("unrecognized", result.Status);
        Assert.Contains("No score information", result.Message);
    }

    [Fact]
    public async Task Handle_AmbiguousPlayer_ReturnsAmbiguousWithCandidates()
    {
        // Arrange
        var extraction = StateExtractionResult.Create(
            changeType: StateChangeType.ScoreChange,
            originalMessage: "Marco ha 5 punti",
            confidence: 0.9f,
            extractedState: new Dictionary<string, object> { { "score", 5 }, { "dimension", "points" } },
            playerName: "Marco",
            requiresConfirmation: false);

        _mockStateParser
            .Setup(p => p.ParseAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(extraction);

        var candidates = new List<(Guid, string)>
        {
            (_playerId, "Marco Rossi"),
            (Guid.NewGuid(), "Marco Bianchi")
        };

        _mockPlayerResolution
            .Setup(r => r.ResolvePlayer("Marco", It.IsAny<IReadOnlyDictionary<Guid, string>>()))
            .Returns(PlayerResolutionResult.Ambiguous(candidates));

        var command = new ParseAndRecordScoreCommand
        {
            SessionId = _sessionId,
            Message = "Marco ha 5 punti",
            AutoRecord = true
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal("ambiguous", result.Status);
        Assert.True(result.RequiresConfirmation);
        Assert.Equal(2, result.AmbiguousCandidates.Count);
        Assert.Contains("Marco Rossi", result.AmbiguousCandidates);
        Assert.Contains("Marco Bianchi", result.AmbiguousCandidates);
    }

    [Fact]
    public async Task Handle_PlayerNotFound_ReturnsParsedWithMessage()
    {
        // Arrange
        var extraction = StateExtractionResult.Create(
            changeType: StateChangeType.ScoreChange,
            originalMessage: "Giovanni ha 5 punti",
            confidence: 0.9f,
            extractedState: new Dictionary<string, object> { { "score", 5 }, { "dimension", "points" } },
            playerName: "Giovanni",
            requiresConfirmation: false);

        _mockStateParser
            .Setup(p => p.ParseAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(extraction);

        _mockPlayerResolution
            .Setup(r => r.ResolvePlayer("Giovanni", It.IsAny<IReadOnlyDictionary<Guid, string>>()))
            .Returns(PlayerResolutionResult.NotFound());

        var command = new ParseAndRecordScoreCommand
        {
            SessionId = _sessionId,
            Message = "Giovanni ha 5 punti",
            AutoRecord = true
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal("parsed", result.Status);
        Assert.True(result.RequiresConfirmation);
        Assert.Contains("Giovanni", result.Message);
        Assert.Contains("not found", result.Message);
    }

    private LiveSessionDto CreateLiveSessionDto(int currentTurnIndex = 1)
    {
        return new LiveSessionDto(
            Id: _sessionId,
            SessionCode: "ABC123",
            GameId: null,
            GameName: "Test Game",
            CreatedByUserId: Guid.NewGuid(),
            Status: LiveSessionStatus.InProgress,
            Visibility: PlayRecordVisibility.Private,
            GroupId: null,
            CreatedAt: DateTime.UtcNow,
            StartedAt: DateTime.UtcNow,
            PausedAt: null,
            CompletedAt: null,
            UpdatedAt: DateTime.UtcNow,
            LastSavedAt: null,
            CurrentTurnIndex: currentTurnIndex,
            CurrentTurnPlayerId: null,
            AgentMode: AgentSessionMode.None,
            ChatSessionId: null,
            Notes: null,
            Players: new List<LiveSessionPlayerDto>(),
            Teams: new List<LiveSessionTeamDto>(),
            RoundScores: new List<LiveSessionRoundScoreDto>(),
            ScoringConfig: new LiveSessionScoringConfigDto(
                new List<string> { "points" },
                new Dictionary<string, string>())
        );
    }
}
