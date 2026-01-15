using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Handlers;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Integration tests for ParseLedgerMessageCommandHandler.
/// Issue #2405 - Ledger Mode state tracking
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
}
