using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Integration tests for ParseLedgerMessageCommandHandler.
/// Issue #2405 - Ledger Mode state tracking
/// Issue #2468 - Ledger Mode Test Suite
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class ParseLedgerMessageCommandHandlerTests
{
    private readonly Mock<IGameSessionStateRepository> _mockSessionStateRepo;
    private readonly Mock<IStateParser> _mockParser;
    private readonly Mock<ILogger<ParseLedgerMessageCommandHandler>> _mockLogger;
    private readonly ParseLedgerMessageCommandHandler _handler;
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public ParseLedgerMessageCommandHandlerTests()
    {
        _mockSessionStateRepo = new Mock<IGameSessionStateRepository>();
        _mockParser = new Mock<IStateParser>();
        _mockLogger = new Mock<ILogger<ParseLedgerMessageCommandHandler>>();

        _handler = new ParseLedgerMessageCommandHandler(
            _mockSessionStateRepo.Object,
            _mockParser.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task Handle_WithValidMessage_ReturnsParsedResult()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var message = "ho 5 punti";

        var sessionState = CreateTestSessionState(sessionId);
        var extraction = StateExtractionResult.Create(
            StateChangeType.ScoreChange,
            message,
            0.9f,
            new Dictionary<string, object> { { "score", 5 } });

        _mockSessionStateRepo.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionState);
        _mockParser.Setup(p => p.ParseAsync(message, It.IsAny<JsonDocument?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(extraction);
        _mockParser.Setup(p => p.DetectConflictsAsync(
                It.IsAny<StateExtractionResult>(),
                It.IsAny<JsonDocument>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StateConflict>());

        var command = new ParseLedgerMessageCommand(sessionId, message, userId);

        // Act
        var result = await _handler.Handle(command, TestCancellationToken);

        // Assert
        Assert.Equal("ScoreChange", result.ChangeType);
        Assert.Equal(message, result.OriginalMessage);
        Assert.Equal(0.9f, result.Confidence);
        Assert.Contains("score", result.ExtractedState.Keys);
        Assert.Equal(5, result.ExtractedState["score"]);
        Assert.Empty(result.Conflicts);
    }

    [Fact]
    public async Task Handle_WithConflicts_ReturnsConflictsInResult()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var message = "ho 3 punti";

        var sessionState = CreateTestSessionState(sessionId);
        var extraction = StateExtractionResult.Create(
            StateChangeType.ScoreChange,
            message,
            0.85f,
            new Dictionary<string, object> { { "score", 3 } });

        var conflict = StateConflict.Create(
            propertyName: "score",
            conflictingMessage: message,
            existingValue: 5,
            newValue: 3,
            lastUpdatedAt: DateTime.UtcNow.AddMinutes(-5),
            severity: ConflictSeverity.High);

        _mockSessionStateRepo.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionState);
        _mockParser.Setup(p => p.ParseAsync(message, It.IsAny<JsonDocument?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(extraction);
        _mockParser.Setup(p => p.DetectConflictsAsync(
                It.IsAny<StateExtractionResult>(),
                It.IsAny<JsonDocument>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StateConflict> { conflict });

        var command = new ParseLedgerMessageCommand(sessionId, message, userId);

        // Act
        var result = await _handler.Handle(command, TestCancellationToken);

        // Assert
        Assert.NotEmpty(result.Conflicts);
        Assert.Single(result.Conflicts);
        Assert.Equal("score", result.Conflicts[0].PropertyName);
        Assert.Equal("High", result.Conflicts[0].Severity);
    }

    [Fact]
    public async Task Handle_WithNonExistentSession_ThrowsInvalidOperationException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        _mockSessionStateRepo.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameSessionState?)null);

        var command = new ParseLedgerMessageCommand(sessionId, "test", Guid.NewGuid());

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _handler.Handle(command, TestCancellationToken));
    }

    #region Issue #2468 - Extended Test Suite

    [Fact]
    public async Task Handle_WithNoStateChanges_ReturnsNoChangeResult()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var message = "ciao a tutti!";

        var sessionState = CreateTestSessionState(sessionId);
        var extraction = StateExtractionResult.NoChange(message);

        _mockSessionStateRepo.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionState);
        _mockParser.Setup(p => p.ParseAsync(message, It.IsAny<JsonDocument?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(extraction);

        var command = new ParseLedgerMessageCommand(sessionId, message, userId);

        // Act
        var result = await _handler.Handle(command, TestCancellationToken);

        // Assert
        result.ChangeType.Should().Be("NoChange");
        result.ExtractedState.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithMultipleConflicts_ReturnsAllConflicts()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var message = "ho 3 punti e 5 legno";

        var sessionState = CreateTestSessionState(sessionId);
        var extraction = StateExtractionResult.Create(
            StateChangeType.Composite,
            message,
            0.8f,
            new Dictionary<string, object> { { "score", 3 }, { "wood", 5 } });

        var conflicts = new List<StateConflict>
        {
            StateConflict.Create("score", message, 10, 3, DateTime.UtcNow.AddMinutes(-5), ConflictSeverity.High),
            StateConflict.Create("wood", message, 8, 5, DateTime.UtcNow.AddMinutes(-5), ConflictSeverity.Medium)
        };

        _mockSessionStateRepo.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionState);
        _mockParser.Setup(p => p.ParseAsync(message, It.IsAny<JsonDocument?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(extraction);
        _mockParser.Setup(p => p.DetectConflictsAsync(
                It.IsAny<StateExtractionResult>(),
                It.IsAny<JsonDocument>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(conflicts);

        var command = new ParseLedgerMessageCommand(sessionId, message, userId);

        // Act
        var result = await _handler.Handle(command, TestCancellationToken);

        // Assert
        result.Conflicts.Should().HaveCount(2);
        result.Conflicts.Should().Contain(c => c.PropertyName == "score");
        result.Conflicts.Should().Contain(c => c.PropertyName == "wood");
    }

    [Fact]
    public async Task Handle_WithLowConfidence_SetsRequiresConfirmationFlag()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var message = "forse ho 5 punti";

        var sessionState = CreateTestSessionState(sessionId);
        var extraction = StateExtractionResult.Create(
            StateChangeType.ScoreChange,
            message,
            0.5f,
            new Dictionary<string, object> { { "score", 5 } },
            requiresConfirmation: true);

        _mockSessionStateRepo.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionState);
        _mockParser.Setup(p => p.ParseAsync(message, It.IsAny<JsonDocument?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(extraction);
        _mockParser.Setup(p => p.DetectConflictsAsync(
                It.IsAny<StateExtractionResult>(),
                It.IsAny<JsonDocument>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StateConflict>());

        var command = new ParseLedgerMessageCommand(sessionId, message, userId);

        // Act
        var result = await _handler.Handle(command, TestCancellationToken);

        // Assert
        result.Confidence.Should().Be(0.5f);
        result.RequiresConfirmation.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithResourceChange_ExtractsResourceData()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var message = "ho 3 legno e 2 pietra";

        var sessionState = CreateTestSessionState(sessionId);
        var extraction = StateExtractionResult.Create(
            StateChangeType.ResourceChange,
            message,
            0.9f,
            new Dictionary<string, object> { { "wood", 3 }, { "stone", 2 } });

        _mockSessionStateRepo.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionState);
        _mockParser.Setup(p => p.ParseAsync(message, It.IsAny<JsonDocument?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(extraction);
        _mockParser.Setup(p => p.DetectConflictsAsync(
                It.IsAny<StateExtractionResult>(),
                It.IsAny<JsonDocument>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StateConflict>());

        var command = new ParseLedgerMessageCommand(sessionId, message, userId);

        // Act
        var result = await _handler.Handle(command, TestCancellationToken);

        // Assert
        result.ChangeType.Should().Be("ResourceChange");
        result.ExtractedState.Should().ContainKey("wood");
        result.ExtractedState.Should().ContainKey("stone");
        result.ExtractedState["wood"].Should().Be(3);
        result.ExtractedState["stone"].Should().Be(2);
    }

    [Fact]
    public async Task Handle_WithTurnChange_ExtractsTurnData()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var message = "tocca a Marco";

        var sessionState = CreateTestSessionState(sessionId);
        var extraction = StateExtractionResult.Create(
            StateChangeType.TurnChange,
            message,
            0.95f,
            new Dictionary<string, object> { { "currentPlayer", "Marco" } },
            playerName: "Marco");

        _mockSessionStateRepo.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionState);
        _mockParser.Setup(p => p.ParseAsync(message, It.IsAny<JsonDocument?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(extraction);
        _mockParser.Setup(p => p.DetectConflictsAsync(
                It.IsAny<StateExtractionResult>(),
                It.IsAny<JsonDocument>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StateConflict>());

        var command = new ParseLedgerMessageCommand(sessionId, message, userId);

        // Act
        var result = await _handler.Handle(command, TestCancellationToken);

        // Assert
        result.ChangeType.Should().Be("TurnChange");
        result.ExtractedState.Should().ContainKey("currentPlayer");
        result.ExtractedState["currentPlayer"].Should().Be("Marco");
    }

    [Fact]
    public async Task Handle_WithPhaseChange_ExtractsPhaseData()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var message = "inizia la fase di produzione";

        var sessionState = CreateTestSessionState(sessionId);
        var extraction = StateExtractionResult.Create(
            StateChangeType.PhaseChange,
            message,
            0.9f,
            new Dictionary<string, object> { { "phase", "production" } });

        _mockSessionStateRepo.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionState);
        _mockParser.Setup(p => p.ParseAsync(message, It.IsAny<JsonDocument?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(extraction);
        _mockParser.Setup(p => p.DetectConflictsAsync(
                It.IsAny<StateExtractionResult>(),
                It.IsAny<JsonDocument>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StateConflict>());

        var command = new ParseLedgerMessageCommand(sessionId, message, userId);

        // Act
        var result = await _handler.Handle(command, TestCancellationToken);

        // Assert
        result.ChangeType.Should().Be("PhaseChange");
        result.ExtractedState.Should().ContainKey("phase");
        result.ExtractedState["phase"].Should().Be("production");
    }

    [Fact]
    public async Task Handle_WithPlayerAction_ExtractsActionData()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var message = "costruisco una strada";

        var sessionState = CreateTestSessionState(sessionId);
        var extraction = StateExtractionResult.Create(
            StateChangeType.PlayerAction,
            message,
            0.85f,
            new Dictionary<string, object> { { "action", "build" }, { "target", "road" } });

        _mockSessionStateRepo.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionState);
        _mockParser.Setup(p => p.ParseAsync(message, It.IsAny<JsonDocument?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(extraction);
        _mockParser.Setup(p => p.DetectConflictsAsync(
                It.IsAny<StateExtractionResult>(),
                It.IsAny<JsonDocument>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StateConflict>());

        var command = new ParseLedgerMessageCommand(sessionId, message, userId);

        // Act
        var result = await _handler.Handle(command, TestCancellationToken);

        // Assert
        result.ChangeType.Should().Be("PlayerAction");
        result.ExtractedState.Should().ContainKey("action");
        result.ExtractedState["action"].Should().Be("build");
    }

    [Fact]
    public async Task Handle_WithCompositeChange_ExtractsMultipleChanges()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var message = "costruisco un insediamento e ora ho 3 punti";

        var sessionState = CreateTestSessionState(sessionId);
        var extraction = StateExtractionResult.Create(
            StateChangeType.Composite,
            message,
            0.85f,
            new Dictionary<string, object>
            {
                { "action", "build" },
                { "target", "settlement" },
                { "score", 3 }
            });

        _mockSessionStateRepo.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionState);
        _mockParser.Setup(p => p.ParseAsync(message, It.IsAny<JsonDocument?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(extraction);
        _mockParser.Setup(p => p.DetectConflictsAsync(
                It.IsAny<StateExtractionResult>(),
                It.IsAny<JsonDocument>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StateConflict>());

        var command = new ParseLedgerMessageCommand(sessionId, message, userId);

        // Act
        var result = await _handler.Handle(command, TestCancellationToken);

        // Assert
        result.ChangeType.Should().Be("Composite");
        result.ExtractedState.Should().HaveCountGreaterThanOrEqualTo(3);
        result.ExtractedState.Should().ContainKey("score");
        result.ExtractedState.Should().ContainKey("action");
    }

    [Fact]
    public async Task Handle_WithCriticalConflict_ReturnsAskUserResolution()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var message = "ho vinto la partita";

        var sessionState = CreateTestSessionState(sessionId);
        var extraction = StateExtractionResult.Create(
            StateChangeType.ScoreChange,
            message,
            0.9f,
            new Dictionary<string, object> { { "gameEnded", true }, { "winner", "player1" } });

        var conflict = StateConflict.Create(
            propertyName: "gameEnded",
            conflictingMessage: message,
            existingValue: false,
            newValue: true,
            lastUpdatedAt: DateTime.UtcNow.AddSeconds(-10),
            severity: ConflictSeverity.Critical);

        _mockSessionStateRepo.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionState);
        _mockParser.Setup(p => p.ParseAsync(message, It.IsAny<JsonDocument?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(extraction);
        _mockParser.Setup(p => p.DetectConflictsAsync(
                It.IsAny<StateExtractionResult>(),
                It.IsAny<JsonDocument>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StateConflict> { conflict });

        var command = new ParseLedgerMessageCommand(sessionId, message, userId);

        // Act
        var result = await _handler.Handle(command, TestCancellationToken);

        // Assert
        result.Conflicts.Should().ContainSingle();
        result.Conflicts[0].Severity.Should().Be("Critical");
        result.Conflicts[0].SuggestedResolution.Should().Be("AskUser");
    }

    [Fact]
    public async Task Handle_VerifiesParserCalledWithCorrectParameters()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var message = "ho 5 punti";

        var sessionState = CreateTestSessionState(sessionId);
        var extraction = StateExtractionResult.NoChange(message);

        _mockSessionStateRepo.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionState);
        _mockParser.Setup(p => p.ParseAsync(message, It.IsAny<JsonDocument?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(extraction);

        var command = new ParseLedgerMessageCommand(sessionId, message, userId);

        // Act
        await _handler.Handle(command, TestCancellationToken);

        // Assert
        _mockParser.Verify(p => p.ParseAsync(
            message,
            It.IsAny<JsonDocument?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_VerifiesSessionStateRepositoryCalled()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var message = "test message";

        var sessionState = CreateTestSessionState(sessionId);
        var extraction = StateExtractionResult.NoChange(message);

        _mockSessionStateRepo.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionState);
        _mockParser.Setup(p => p.ParseAsync(message, It.IsAny<JsonDocument?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(extraction);

        var command = new ParseLedgerMessageCommand(sessionId, message, userId);

        // Act
        await _handler.Handle(command, TestCancellationToken);

        // Assert
        _mockSessionStateRepo.Verify(r => r.GetBySessionIdAsync(
            sessionId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithWarnings_ReturnsWarningsInResult()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var message = "messaggio ambiguo con punti";

        var sessionState = CreateTestSessionState(sessionId);
        var warnings = new List<string> { "Ambiguous score reference", "Multiple interpretations possible" };
        var extraction = StateExtractionResult.Create(
            StateChangeType.ScoreChange,
            message,
            0.6f,
            new Dictionary<string, object> { { "score", 0 } },
            warnings: warnings);

        _mockSessionStateRepo.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionState);
        _mockParser.Setup(p => p.ParseAsync(message, It.IsAny<JsonDocument?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(extraction);
        _mockParser.Setup(p => p.DetectConflictsAsync(
                It.IsAny<StateExtractionResult>(),
                It.IsAny<JsonDocument>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StateConflict>());

        var command = new ParseLedgerMessageCommand(sessionId, message, userId);

        // Act
        var result = await _handler.Handle(command, TestCancellationToken);

        // Assert
        result.Warnings.Should().HaveCount(2);
        result.Warnings.Should().Contain("Ambiguous score reference");
    }

    [Theory]
    [InlineData(0.0f)]
    [InlineData(0.5f)]
    [InlineData(1.0f)]
    public async Task Handle_WithVariousConfidenceLevels_ReturnsCorrectConfidence(float confidence)
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var message = "test message";

        var sessionState = CreateTestSessionState(sessionId);
        var extraction = StateExtractionResult.Create(
            StateChangeType.ScoreChange,
            message,
            confidence,
            new Dictionary<string, object> { { "score", 5 } });

        _mockSessionStateRepo.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionState);
        _mockParser.Setup(p => p.ParseAsync(message, It.IsAny<JsonDocument?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(extraction);
        _mockParser.Setup(p => p.DetectConflictsAsync(
                It.IsAny<StateExtractionResult>(),
                It.IsAny<JsonDocument>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StateConflict>());

        var command = new ParseLedgerMessageCommand(sessionId, message, userId);

        // Act
        var result = await _handler.Handle(command, TestCancellationToken);

        // Assert
        result.Confidence.Should().Be(confidence);
    }

    #endregion

    #region Helper Methods

    private static GameSessionState CreateTestSessionState(Guid sessionId)
    {
        var initialState = JsonDocument.Parse("""{"score": 0}""");
        return GameSessionState.Create(
            id: Guid.NewGuid(),
            gameSessionId: sessionId,
            templateId: Guid.NewGuid(),
            initialState: initialState,
            createdBy: "test-user"
        );
    }

    #endregion
}
