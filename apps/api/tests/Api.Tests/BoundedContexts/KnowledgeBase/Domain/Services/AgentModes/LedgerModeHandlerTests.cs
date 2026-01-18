using System.Text.Json;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.AgentModes;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services.AgentModes;

/// <summary>
/// Comprehensive unit tests for LedgerModeHandler.
/// Issue #2468 - Ledger Mode Test Suite
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class LedgerModeHandlerTests
{
    private readonly Mock<IGameSessionStateRepository> _mockSessionStateRepo;
    private readonly Mock<IStateParser> _mockParser;
    private readonly Mock<ILogger<LedgerModeHandler>> _mockLogger;
    private readonly LedgerModeHandler _handler;
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public LedgerModeHandlerTests()
    {
        _mockSessionStateRepo = new Mock<IGameSessionStateRepository>();
        _mockParser = new Mock<IStateParser>();
        _mockLogger = new Mock<ILogger<LedgerModeHandler>>();

        _handler = new LedgerModeHandler(
            _mockSessionStateRepo.Object,
            _mockParser.Object,
            _mockLogger.Object);
    }

    #region Basic Handler Tests

    [Fact]
    public void SupportedMode_ReturnsLedger()
    {
        _handler.SupportedMode.Should().Be(AgentMode.Ledger);
    }

    [Fact]
    public async Task HandleAsync_WithNullContext_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _handler.HandleAsync(null!, TestCancellationToken));
    }

    [Fact]
    public void CreateContext_WithNoDocumentsSelected_ThrowsArgumentException()
    {
        // AgentConfiguration.Create() validates that Ledger mode requires at least one document
        // This validation happens at configuration creation time, not handler execution time
        var act = () => CreateTestContext(selectedDocumentIds: new List<Guid>());

        act.Should().Throw<ArgumentException>()
            .WithMessage("*require at least one document*");
    }

    #endregion

    #region Message Processing Tests

    [Fact]
    public async Task HandleAsync_WithValidMessage_ReturnsLedgerResult()
    {
        // Arrange
        var context = CreateTestContext("ho 5 punti");
        var extraction = StateExtractionResult.Create(
            StateChangeType.ScoreChange,
            "ho 5 punti",
            0.9f,
            new Dictionary<string, object> { { "score", 5 } });

        _mockParser.Setup(p => p.ParseAsync(
                It.IsAny<string>(), It.IsAny<JsonDocument?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(extraction);

        // Act
        var result = await _handler.HandleAsync(context, TestCancellationToken);

        // Assert
        result.Mode.Should().Be(AgentMode.Ledger);
        result.Content.Should().Contain("Stato aggiornato rilevato");
        result.Content.Should().Contain("Punti");
        result.Metadata.Should().ContainKey("changeType");
        result.Metadata["changeType"].Should().Be("ScoreChange");
    }

    [Fact]
    public async Task HandleAsync_WithNoStateChanges_ReturnsNoChangeMessage()
    {
        // Arrange
        var context = CreateTestContext("Ciao come stai?");
        var extraction = StateExtractionResult.NoChange("Ciao come stai?");

        _mockParser.Setup(p => p.ParseAsync(
                It.IsAny<string>(), It.IsAny<JsonDocument?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(extraction);

        // Act
        var result = await _handler.HandleAsync(context, TestCancellationToken);

        // Assert
        result.Content.Should().Contain("Nessuna modifica di stato rilevata");
        result.Metadata["hasStateChanges"].Should().Be(false);
    }

    #endregion

    #region Conflict Detection Tests

    [Fact]
    public async Task HandleAsync_WithConflicts_ReturnsConflictsInResponse()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var context = CreateTestContext("ho 10 punti", gameId);
        var sessionState = CreateTestSessionState(gameId, """{"score": 5}""");

        var extraction = StateExtractionResult.Create(
            StateChangeType.ScoreChange,
            "ho 10 punti",
            0.9f,
            new Dictionary<string, object> { { "score", 10 } });

        var conflict = StateConflict.Create(
            propertyName: "score",
            conflictingMessage: "ho 10 punti",
            existingValue: 5,
            newValue: 10,
            lastUpdatedAt: DateTime.UtcNow.AddMinutes(-5),
            severity: ConflictSeverity.Critical);

        _mockSessionStateRepo.Setup(r => r.GetBySessionIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionState);
        _mockParser.Setup(p => p.ParseAsync(
                It.IsAny<string>(), It.IsAny<JsonDocument?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(extraction);
        _mockParser.Setup(p => p.DetectConflictsAsync(
                It.IsAny<StateExtractionResult>(),
                It.IsAny<JsonDocument>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StateConflict> { conflict });

        // Act
        var result = await _handler.HandleAsync(context, TestCancellationToken);

        // Assert
        result.Content.Should().Contain("Conflitto rilevato");
        result.Metadata["conflictCount"].Should().Be(1);
        result.Metadata.Should().ContainKey("conflicts");
    }

    [Fact]
    public async Task HandleAsync_WithNoConflicts_ReturnsConfirmationPrompt()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var context = CreateTestContext("ho 5 punti", gameId);
        var sessionState = CreateTestSessionState(gameId, """{"roads": 2}""");

        var extraction = StateExtractionResult.Create(
            StateChangeType.ScoreChange,
            "ho 5 punti",
            0.85f, // Lower confidence requires confirmation
            new Dictionary<string, object> { { "score", 5 } },
            requiresConfirmation: true);

        _mockSessionStateRepo.Setup(r => r.GetBySessionIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionState);
        _mockParser.Setup(p => p.ParseAsync(
                It.IsAny<string>(), It.IsAny<JsonDocument?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(extraction);
        _mockParser.Setup(p => p.DetectConflictsAsync(
                It.IsAny<StateExtractionResult>(),
                It.IsAny<JsonDocument>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StateConflict>());

        // Act
        var result = await _handler.HandleAsync(context, TestCancellationToken);

        // Assert
        result.Content.Should().Contain("Vuoi confermare questa modifica?");
        result.Metadata["requiresConfirmation"].Should().Be(true);
    }

    #endregion

    #region Confidence Calculation Tests

    [Fact]
    public async Task HandleAsync_WithHighExtractionConfidence_ReturnsHighOverallConfidence()
    {
        // Arrange
        var context = CreateTestContextWithSearchResults("tocca a Marco", searchResultConfidence: 0.9);
        var extraction = StateExtractionResult.Create(
            StateChangeType.TurnChange,
            "tocca a Marco",
            0.95f, // High extraction confidence
            new Dictionary<string, object> { { "currentPlayer", "Marco" } });

        _mockParser.Setup(p => p.ParseAsync(
                It.IsAny<string>(), It.IsAny<JsonDocument?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(extraction);

        // Act
        var result = await _handler.HandleAsync(context, TestCancellationToken);

        // Assert
        // Expected: (0.95 * 0.7) + (0.9 * 0.3) = 0.665 + 0.27 = 0.935
        result.Confidence.Should().BeGreaterThan(0.9);
    }

    [Fact]
    public async Task HandleAsync_WithLowExtractionConfidence_ReturnsLowerOverallConfidence()
    {
        // Arrange
        var context = CreateTestContextWithSearchResults("forse ho 5 punti", searchResultConfidence: 0.5);
        var extraction = StateExtractionResult.Create(
            StateChangeType.ScoreChange,
            "forse ho 5 punti",
            0.5f, // Low extraction confidence
            new Dictionary<string, object> { { "score", 5 } });

        _mockParser.Setup(p => p.ParseAsync(
                It.IsAny<string>(), It.IsAny<JsonDocument?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(extraction);

        // Act
        var result = await _handler.HandleAsync(context, TestCancellationToken);

        // Assert
        // Expected: (0.5 * 0.7) + (0.5 * 0.3) = 0.35 + 0.15 = 0.5
        result.Confidence.Should().BeLessThanOrEqualTo(0.6);
    }

    [Fact]
    public async Task HandleAsync_WithNoSearchResults_UsesDefaultSearchConfidence()
    {
        // Arrange
        var context = CreateTestContext("ho 5 punti"); // No search results = default 0.5
        var extraction = StateExtractionResult.Create(
            StateChangeType.ScoreChange,
            "ho 5 punti",
            0.9f,
            new Dictionary<string, object> { { "score", 5 } });

        _mockParser.Setup(p => p.ParseAsync(
                It.IsAny<string>(), It.IsAny<JsonDocument?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(extraction);

        // Act
        var result = await _handler.HandleAsync(context, TestCancellationToken);

        // Assert
        // Expected: (0.9 * 0.7) + (0.5 * 0.3) = 0.63 + 0.15 = 0.78
        result.Confidence.Should().BeGreaterThan(0.7);
        result.Confidence.Should().BeLessThan(0.85);
    }

    #endregion

    #region Response Formatting Tests

    [Fact]
    public async Task HandleAsync_WithHighConfidence_ShowsGreenCheckEmoji()
    {
        // Arrange
        var context = CreateTestContext("tocca a Marco");
        var extraction = StateExtractionResult.Create(
            StateChangeType.TurnChange,
            "tocca a Marco",
            0.95f,
            new Dictionary<string, object> { { "currentPlayer", "Marco" } });

        _mockParser.Setup(p => p.ParseAsync(
                It.IsAny<string>(), It.IsAny<JsonDocument?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(extraction);

        // Act
        var result = await _handler.HandleAsync(context, TestCancellationToken);

        // Assert
        result.Content.Should().Contain("✅");
    }

    [Fact]
    public async Task HandleAsync_WithMediumConfidence_ShowsWarningEmoji()
    {
        // Arrange
        var context = CreateTestContext("ho costruito una strada");
        var extraction = StateExtractionResult.Create(
            StateChangeType.PlayerAction,
            "ho costruito una strada",
            0.7f, // Medium confidence
            new Dictionary<string, object> { { "buildings.roads", 1 } });

        _mockParser.Setup(p => p.ParseAsync(
                It.IsAny<string>(), It.IsAny<JsonDocument?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(extraction);

        // Act
        var result = await _handler.HandleAsync(context, TestCancellationToken);

        // Assert
        result.Content.Should().Contain("⚠️");
    }

    [Fact]
    public async Task HandleAsync_WithPlayerName_IncludesPlayerInResponse()
    {
        // Arrange
        var context = CreateTestContext("Marco ha 5 punti");
        var extraction = StateExtractionResult.Create(
            StateChangeType.ScoreChange,
            "Marco ha 5 punti",
            0.9f,
            new Dictionary<string, object> { { "score", 5 } },
            playerName: "Marco");

        _mockParser.Setup(p => p.ParseAsync(
                It.IsAny<string>(), It.IsAny<JsonDocument?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(extraction);

        // Act
        var result = await _handler.HandleAsync(context, TestCancellationToken);

        // Assert
        result.Content.Should().Contain("Giocatore: **Marco**");
        result.Metadata["playerName"].Should().Be("Marco");
    }

    [Fact]
    public async Task HandleAsync_WithWarnings_IncludesWarningsInResponse()
    {
        // Arrange
        var context = CreateTestContext("ho costruito una strada e ho 5 punti");
        var extraction = StateExtractionResult.Create(
            StateChangeType.Composite,
            "ho costruito una strada e ho 5 punti",
            0.8f,
            new Dictionary<string, object>
            {
                { "buildings.roads", 1 },
                { "score", 5 }
            },
            warnings: new List<string> { "Rilevate modifiche multiple nello stesso messaggio." });

        _mockParser.Setup(p => p.ParseAsync(
                It.IsAny<string>(), It.IsAny<JsonDocument?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(extraction);

        // Act
        var result = await _handler.HandleAsync(context, TestCancellationToken);

        // Assert
        result.Content.Should().Contain("Avvisi");
        result.Content.Should().Contain("modifiche multiple");
        result.Metadata["warningCount"].Should().Be(1);
    }

    #endregion

    #region Metadata Tests

    [Fact]
    public async Task HandleAsync_WithStateChanges_IncludesExtractedStateInMetadata()
    {
        // Arrange
        var context = CreateTestContext("ho 5 punti");
        var extraction = StateExtractionResult.Create(
            StateChangeType.ScoreChange,
            "ho 5 punti",
            0.9f,
            new Dictionary<string, object> { { "score", 5 } });

        _mockParser.Setup(p => p.ParseAsync(
                It.IsAny<string>(), It.IsAny<JsonDocument?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(extraction);

        // Act
        var result = await _handler.HandleAsync(context, TestCancellationToken);

        // Assert
        result.Metadata.Should().ContainKey("extractedState");
        var extractedState = result.Metadata["extractedState"] as IReadOnlyDictionary<string, object>;
        extractedState.Should().NotBeNull();
        extractedState!["score"].Should().Be(5);
    }

    [Fact]
    public async Task HandleAsync_WithNoStateChanges_DoesNotIncludeExtractedStateInMetadata()
    {
        // Arrange
        var context = CreateTestContext("Ciao!");
        var extraction = StateExtractionResult.NoChange("Ciao!");

        _mockParser.Setup(p => p.ParseAsync(
                It.IsAny<string>(), It.IsAny<JsonDocument?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(extraction);

        // Act
        var result = await _handler.HandleAsync(context, TestCancellationToken);

        // Assert
        result.Metadata.Should().NotContainKey("extractedState");
    }

    #endregion

    #region Session State Integration Tests

    [Fact]
    public async Task HandleAsync_WithGameId_RetrievesSessionState()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var context = CreateTestContext("ho 5 punti", gameId);
        var sessionState = CreateTestSessionState(gameId, """{"score": 0}""");

        var extraction = StateExtractionResult.Create(
            StateChangeType.ScoreChange,
            "ho 5 punti",
            0.9f,
            new Dictionary<string, object> { { "score", 5 } });

        _mockSessionStateRepo.Setup(r => r.GetBySessionIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionState);
        _mockParser.Setup(p => p.ParseAsync(
                It.IsAny<string>(), It.IsAny<JsonDocument?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(extraction);
        _mockParser.Setup(p => p.DetectConflictsAsync(
                It.IsAny<StateExtractionResult>(),
                It.IsAny<JsonDocument>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StateConflict>());

        // Act
        await _handler.HandleAsync(context, TestCancellationToken);

        // Assert
        _mockSessionStateRepo.Verify(
            r => r.GetBySessionIdAsync(gameId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithoutGameId_DoesNotRetrieveSessionState()
    {
        // Arrange
        var context = CreateTestContext("ho 5 punti", gameId: null);
        var extraction = StateExtractionResult.Create(
            StateChangeType.ScoreChange,
            "ho 5 punti",
            0.9f,
            new Dictionary<string, object> { { "score", 5 } });

        _mockParser.Setup(p => p.ParseAsync(
                It.IsAny<string>(), It.IsAny<JsonDocument?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(extraction);

        // Act
        await _handler.HandleAsync(context, TestCancellationToken);

        // Assert
        _mockSessionStateRepo.Verify(
            r => r.GetBySessionIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithNoSessionStateFound_ContinuesWithoutCurrentState()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var context = CreateTestContext("ho 5 punti", gameId);

        var extraction = StateExtractionResult.Create(
            StateChangeType.ScoreChange,
            "ho 5 punti",
            0.9f,
            new Dictionary<string, object> { { "score", 5 } });

        _mockSessionStateRepo.Setup(r => r.GetBySessionIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameSessionState?)null);
        _mockParser.Setup(p => p.ParseAsync(
                It.IsAny<string>(), It.IsAny<JsonDocument?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(extraction);

        // Act
        var result = await _handler.HandleAsync(context, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().NotBeEmpty();
        // Should not call DetectConflictsAsync since no current state
        _mockParser.Verify(
            p => p.DetectConflictsAsync(
                It.IsAny<StateExtractionResult>(),
                It.IsAny<JsonDocument>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Helper Methods

    private static AgentModeContext CreateTestContext(
        string query = "test query",
        Guid? gameId = null,
        List<Guid>? selectedDocumentIds = null)
    {
        var agent = new Agent(
            Guid.NewGuid(),
            "Test Agent",
            AgentType.RagAgent,
            AgentStrategy.HybridSearch());

        var docIds = selectedDocumentIds ?? new List<Guid> { Guid.NewGuid() };
        var config = AgentConfiguration.Create(
            agent.Id,
            LlmProvider.OpenRouter,
            "openai/gpt-4o-mini",
            AgentMode.Ledger,
            docIds,
            0.7m,
            4096,
            Guid.NewGuid());

        return new AgentModeContext(
            agent,
            config,
            query,
            gameId,
            null,
            new List<SearchResult>(),
            Guid.NewGuid());
    }

    private static AgentModeContext CreateTestContextWithSearchResults(
        string query,
        double searchResultConfidence)
    {
        var agent = new Agent(
            Guid.NewGuid(),
            "Test Agent",
            AgentType.RagAgent,
            AgentStrategy.HybridSearch());

        var config = AgentConfiguration.Create(
            agent.Id,
            LlmProvider.OpenRouter,
            "openai/gpt-4o-mini",
            AgentMode.Ledger,
            new List<Guid> { Guid.NewGuid() },
            0.7m,
            4096,
            Guid.NewGuid());

        var searchResults = new List<SearchResult>
        {
            new SearchResult(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Test content",
                1,
                new Confidence(searchResultConfidence),
                1,
                "vector")
        };

        return new AgentModeContext(
            agent,
            config,
            query,
            null,
            null,
            searchResults,
            Guid.NewGuid());
    }

    private static GameSessionState CreateTestSessionState(Guid sessionId, string stateJson)
    {
        var initialState = JsonDocument.Parse(stateJson);
        return GameSessionState.Create(
            id: Guid.NewGuid(),
            gameSessionId: sessionId,
            templateId: Guid.NewGuid(),
            initialState: initialState,
            createdBy: "test-user");
    }

    #endregion
}
