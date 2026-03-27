using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Tests for ValidateMoveCommandHandler.
/// Issue #3760: Arbitro Agent Move Validation Logic with Game State Analysis.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "3760")]
public class ValidateMoveCommandHandlerTests
{
    private readonly Mock<IGameSessionRepository> _mockGameSessionRepository;
    private readonly Mock<IArbitroAgentService> _mockArbitroService;
    private readonly Mock<ILogger<ValidateMoveCommandHandler>> _mockLogger;
    private readonly TimeProvider _timeProvider;
    private readonly ValidateMoveCommandHandler _handler;

    public ValidateMoveCommandHandlerTests()
    {
        _mockGameSessionRepository = new Mock<IGameSessionRepository>();
        _mockArbitroService = new Mock<IArbitroAgentService>();
        _mockLogger = new Mock<ILogger<ValidateMoveCommandHandler>>();
        _timeProvider = TimeProvider.System;

        _handler = new ValidateMoveCommandHandler(
            _mockGameSessionRepository.Object,
            _mockArbitroService.Object,
            _mockLogger.Object,
            _timeProvider);
    }

    [Fact]
    public async Task Handle_WithValidSession_CallsArbitroService()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var session = CreateMockGameSession(sessionId, gameId, "Alice");

        _mockGameSessionRepository
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _mockArbitroService
            .Setup(s => s.ValidateMoveAsync(
                It.IsAny<GameSession>(),
                It.IsAny<Move>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateValidResult());

        var command = new ValidateMoveCommand
        {
            GameSessionId = sessionId,
            PlayerName = "Alice",
            Action = "roll dice",
            Position = null,
            AdditionalContext = null
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Decision.Should().Be("VALID");
        (result.Confidence > 0.7).Should().BeTrue();

        _mockArbitroService.Verify(
            s => s.ValidateMoveAsync(
                It.Is<GameSession>(gs => gs.Id == sessionId),
                It.Is<Move>(m => m.PlayerName == "Alice" && m.Action == "roll dice"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentSession_ThrowsNotFoundException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        _mockGameSessionRepository
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameSession?)null);

        var command = new ValidateMoveCommand
        {
            GameSessionId = sessionId,
            PlayerName = "Bob",
            Action = "move piece",
            Position = "A5",
            AdditionalContext = null
        };

        // Act & Assert
        Func<Task> act = () => _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WithAdditionalContext_PassesToArbitroService()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var session = CreateMockGameSession(sessionId, gameId, "Charlie");

        var additionalContext = new Dictionary<string, string>
        {
            { "card", "King of Hearts" },
            { "resource", "wood" }
        };

        _mockGameSessionRepository
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _mockArbitroService
            .Setup(s => s.ValidateMoveAsync(
                It.IsAny<GameSession>(),
                It.IsAny<Move>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateValidResult());

        var command = new ValidateMoveCommand
        {
            GameSessionId = sessionId,
            PlayerName = "Charlie",
            Action = "play card",
            Position = "discard pile",
            AdditionalContext = additionalContext
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();

        _mockArbitroService.Verify(
            s => s.ValidateMoveAsync(
                It.IsAny<GameSession>(),
                It.Is<Move>(m =>
                    m.AdditionalContext != null &&
                    m.AdditionalContext.ContainsKey("card") &&
                    m.AdditionalContext["card"] == "King of Hearts"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        Func<Task> act = () => _handler.Handle(null!, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Handle_CreatesCorrectMoveValueObject()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var session = CreateMockGameSession(sessionId, gameId, "Diana");

        Move? capturedMove = null;

        _mockGameSessionRepository
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _mockArbitroService
            .Setup(s => s.ValidateMoveAsync(
                It.IsAny<GameSession>(),
                It.IsAny<Move>(),
                It.IsAny<CancellationToken>()))
            .Callback<GameSession, Move, CancellationToken>((_, move, _) => capturedMove = move)
            .ReturnsAsync(CreateValidResult());

        var command = new ValidateMoveCommand
        {
            GameSessionId = sessionId,
            PlayerName = "Diana",
            Action = "draw card",
            Position = "top of deck",
            AdditionalContext = new Dictionary<string, string> { { "deck", "main" } }
        };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        capturedMove.Should().NotBeNull();
        capturedMove.PlayerName.Should().Be("Diana");
        capturedMove.Action.Should().Be("draw card");
        capturedMove.Position.Should().Be("top of deck");
        capturedMove.AdditionalContext.Should().NotBeNull();
        capturedMove.AdditionalContext["deck"].Should().Be("main");
    }

    private static GameSession CreateMockGameSession(Guid sessionId, Guid gameId, string playerName)
    {
        var players = new List<SessionPlayer>
        {
            new SessionPlayer(playerName, 1) // playerName, playerNumber
        };

        var session = new GameSession(
            id: sessionId,
            gameId: gameId,
            players: players,
            createdByUserId: null);

        return session;
    }

    private static MoveValidationResultDto CreateValidResult()
    {
        return new MoveValidationResultDto
        {
            ValidationId = Guid.NewGuid(),
            Decision = "VALID",
            Confidence = 0.95,
            Reasoning = "Move follows all applicable rules",
            ViolatedRules = new List<string>(),
            Suggestions = null,
            ApplicableRules = new List<ArbitroRuleAtomDto>(),
            TokenUsage = TokenUsageDto.Empty,
            CostBreakdown = CostBreakdownDto.Zero("Test"),
            LatencyMs = 50,
            Timestamp = DateTime.UtcNow
        };
    }
}
