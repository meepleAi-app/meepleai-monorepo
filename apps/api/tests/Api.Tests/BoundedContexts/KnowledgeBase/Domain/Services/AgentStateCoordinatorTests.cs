using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Tests for AgentStateCoordinator - shared context management across agents.
/// Issue #4337: Agent State Coordination - Shared Context Management.
/// </summary>
public class AgentStateCoordinatorTests
{
    private readonly Mock<IConversationMemoryRepository> _mockConversationRepo;
    private readonly Mock<IAgentGameStateSnapshotRepository> _mockGameStateRepo;
    private readonly Mock<ILogger<AgentStateCoordinator>> _mockLogger;
    private readonly AgentStateCoordinator _coordinator;

    public AgentStateCoordinatorTests()
    {
        _mockConversationRepo = new Mock<IConversationMemoryRepository>();
        _mockGameStateRepo = new Mock<IAgentGameStateSnapshotRepository>();
        _mockLogger = new Mock<ILogger<AgentStateCoordinator>>();

        _coordinator = new AgentStateCoordinator(
            _mockConversationRepo.Object,
            _mockGameStateRepo.Object,
            _mockLogger.Object);
    }

    #region Constructor

    [Fact]
    public void Constructor_NullConversationRepo_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AgentStateCoordinator(null!, _mockGameStateRepo.Object, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_NullGameStateRepo_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AgentStateCoordinator(_mockConversationRepo.Object, null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AgentStateCoordinator(_mockConversationRepo.Object, _mockGameStateRepo.Object, null!));
    }

    #endregion

    #region GetSharedContextAsync

    [Fact]
    public async Task GetSharedContextAsync_WithConversationAndGameState_ReturnsFullContext()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var memories = new List<ConversationMemory>
        {
            new(Guid.NewGuid(), sessionId, userId, gameId, "Hello", "user"),
            new(Guid.NewGuid(), sessionId, userId, gameId, "Hi there", "assistant"),
            new(Guid.NewGuid(), sessionId, userId, gameId, "How do I play?", "user"),
        };

        var snapshot = new AgentGameStateSnapshot(
            Guid.NewGuid(), gameId, sessionId, "{\"board\":\"state\"}", 5);

        _mockConversationRepo
            .Setup(r => r.GetBySessionIdAsync(sessionId, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(memories);

        _mockGameStateRepo
            .Setup(r => r.GetLatestByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        // Act
        var context = await _coordinator.GetSharedContextAsync(sessionId, gameId);

        // Assert
        Assert.Equal(sessionId, context.SessionId);
        Assert.Equal(gameId, context.GameId);
        Assert.Equal(3, context.ConversationHistory.Count);
        Assert.Contains("[user] Hello", context.ConversationHistory);
        Assert.Contains("[assistant] Hi there", context.ConversationHistory);
        Assert.Equal("{\"board\":\"state\"}", context.CurrentGameState);
        Assert.Equal(5, context.StateVersion);
        Assert.Null(context.LastAgentUsed);
    }

    [Fact]
    public async Task GetSharedContextAsync_NoConversationHistory_ReturnsEmptyHistory()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        _mockConversationRepo
            .Setup(r => r.GetBySessionIdAsync(sessionId, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ConversationMemory>());

        _mockGameStateRepo
            .Setup(r => r.GetLatestByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentGameStateSnapshot?)null);

        // Act
        var context = await _coordinator.GetSharedContextAsync(sessionId, gameId);

        // Assert
        Assert.Empty(context.ConversationHistory);
        Assert.Null(context.CurrentGameState);
        Assert.Equal(1, context.StateVersion); // Default version
    }

    [Fact]
    public async Task GetSharedContextAsync_NoGameState_ReturnsNullGameState()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var memories = new List<ConversationMemory>
        {
            new(Guid.NewGuid(), sessionId, userId, gameId, "Hi", "user"),
        };

        _mockConversationRepo
            .Setup(r => r.GetBySessionIdAsync(sessionId, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(memories);

        _mockGameStateRepo
            .Setup(r => r.GetLatestByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentGameStateSnapshot?)null);

        // Act
        var context = await _coordinator.GetSharedContextAsync(sessionId, gameId);

        // Assert
        Assert.Single(context.ConversationHistory);
        Assert.Null(context.CurrentGameState);
        Assert.Equal(1, context.StateVersion);
    }

    [Fact]
    public async Task GetSharedContextAsync_ConversationRepoThrows_ReturnsEmptyHistoryGracefully()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        _mockConversationRepo
            .Setup(r => r.GetBySessionIdAsync(sessionId, 10, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB connection failed"));

        _mockGameStateRepo
            .Setup(r => r.GetLatestByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentGameStateSnapshot?)null);

        // Act
        var context = await _coordinator.GetSharedContextAsync(sessionId, gameId);

        // Assert - graceful degradation
        Assert.Empty(context.ConversationHistory);
        Assert.Null(context.CurrentGameState);
    }

    [Fact]
    public async Task GetSharedContextAsync_GameStateRepoThrows_ReturnsNullGameStateGracefully()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        _mockConversationRepo
            .Setup(r => r.GetBySessionIdAsync(sessionId, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ConversationMemory>());

        _mockGameStateRepo
            .Setup(r => r.GetLatestByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB connection failed"));

        // Act
        var context = await _coordinator.GetSharedContextAsync(sessionId, gameId);

        // Assert - graceful degradation
        Assert.Null(context.CurrentGameState);
        Assert.Equal(1, context.StateVersion);
    }

    [Fact]
    public async Task GetSharedContextAsync_HistoryFormatsCorrectly()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var memories = new List<ConversationMemory>
        {
            new(Guid.NewGuid(), sessionId, userId, gameId, "What are the rules?", "user"),
            new(Guid.NewGuid(), sessionId, userId, gameId, "Here are the rules...", "assistant"),
        };

        _mockConversationRepo
            .Setup(r => r.GetBySessionIdAsync(sessionId, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(memories);

        _mockGameStateRepo
            .Setup(r => r.GetLatestByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentGameStateSnapshot?)null);

        // Act
        var context = await _coordinator.GetSharedContextAsync(sessionId, gameId);

        // Assert
        Assert.Equal("[user] What are the rules?", context.ConversationHistory[0]);
        Assert.Equal("[assistant] Here are the rules...", context.ConversationHistory[1]);
    }

    [Fact]
    public async Task GetSharedContextAsync_UsesVersionFromSnapshot()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var snapshot = new AgentGameStateSnapshot(
            Guid.NewGuid(), gameId, sessionId, "{\"turn\":42}", 42);

        _mockConversationRepo
            .Setup(r => r.GetBySessionIdAsync(sessionId, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ConversationMemory>());

        _mockGameStateRepo
            .Setup(r => r.GetLatestByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        // Act
        var context = await _coordinator.GetSharedContextAsync(sessionId, gameId);

        // Assert
        Assert.Equal(42, context.StateVersion);
        Assert.Equal("{\"turn\":42}", context.CurrentGameState);
    }

    #endregion

    #region HandoffContext

    [Fact]
    public void HandoffContext_ValidHandoff_ReturnsUpdatedContext()
    {
        // Arrange
        var context = new SharedAgentContext(
            SessionId: Guid.NewGuid(),
            GameId: Guid.NewGuid(),
            ConversationHistory: new List<string> { "[user] hello" },
            CurrentGameState: "{\"board\":\"state\"}",
            LastAgentUsed: null,
            StateVersion: 1
        );

        // Act
        var result = _coordinator.HandoffContext("TutorAgent", "ArbitroAgent", context);

        // Assert
        Assert.Equal("TutorAgent", result.LastAgentUsed);
        Assert.Equal(2, result.StateVersion);
        Assert.Equal(context.SessionId, result.SessionId);
        Assert.Equal(context.GameId, result.GameId);
        Assert.Equal(context.ConversationHistory, result.ConversationHistory);
        Assert.Equal(context.CurrentGameState, result.CurrentGameState);
    }

    [Fact]
    public void HandoffContext_MultipleHandoffs_IncrementsVersionEachTime()
    {
        // Arrange
        var context = new SharedAgentContext(
            SessionId: Guid.NewGuid(),
            GameId: Guid.NewGuid(),
            ConversationHistory: new List<string>(),
            CurrentGameState: null,
            LastAgentUsed: null,
            StateVersion: 1
        );

        // Act
        var after1 = _coordinator.HandoffContext("TutorAgent", "ArbitroAgent", context);
        var after2 = _coordinator.HandoffContext("ArbitroAgent", "DecisoreAgent", after1);
        var after3 = _coordinator.HandoffContext("DecisoreAgent", "TutorAgent", after2);

        // Assert
        Assert.Equal(2, after1.StateVersion);
        Assert.Equal(3, after2.StateVersion);
        Assert.Equal(4, after3.StateVersion);
        Assert.Equal("TutorAgent", after1.LastAgentUsed);
        Assert.Equal("ArbitroAgent", after2.LastAgentUsed);
        Assert.Equal("DecisoreAgent", after3.LastAgentUsed);
    }

    [Fact]
    public void HandoffContext_PreservesOriginalContext()
    {
        // Arrange - verify immutability
        var context = new SharedAgentContext(
            SessionId: Guid.NewGuid(),
            GameId: Guid.NewGuid(),
            ConversationHistory: new List<string> { "[user] test" },
            CurrentGameState: "{\"game\":1}",
            LastAgentUsed: null,
            StateVersion: 5
        );

        // Act
        var result = _coordinator.HandoffContext("Agent1", "Agent2", context);

        // Assert - original unchanged
        Assert.Null(context.LastAgentUsed);
        Assert.Equal(5, context.StateVersion);
        // Result is new
        Assert.Equal("Agent1", result.LastAgentUsed);
        Assert.Equal(6, result.StateVersion);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void HandoffContext_EmptyFromAgent_ThrowsArgumentException(string? fromAgent)
    {
        var context = new SharedAgentContext(
            Guid.NewGuid(), Guid.NewGuid(), new List<string>(), null, null, 1);

        Assert.Throws<ArgumentException>(() =>
            _coordinator.HandoffContext(fromAgent!, "TargetAgent", context));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void HandoffContext_EmptyToAgent_ThrowsArgumentException(string? toAgent)
    {
        var context = new SharedAgentContext(
            Guid.NewGuid(), Guid.NewGuid(), new List<string>(), null, null, 1);

        Assert.Throws<ArgumentException>(() =>
            _coordinator.HandoffContext("SourceAgent", toAgent!, context));
    }

    [Fact]
    public void HandoffContext_NullContext_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _coordinator.HandoffContext("Agent1", "Agent2", null!));
    }

    #endregion

    #region ValidateStateVersion

    [Fact]
    public void ValidateStateVersion_MatchingVersion_ReturnsTrue()
    {
        var context = new SharedAgentContext(
            Guid.NewGuid(), Guid.NewGuid(), new List<string>(), null, null, 5);

        Assert.True(_coordinator.ValidateStateVersion(context, 5));
    }

    [Fact]
    public void ValidateStateVersion_MismatchedVersion_ReturnsFalse()
    {
        var context = new SharedAgentContext(
            Guid.NewGuid(), Guid.NewGuid(), new List<string>(), null, null, 5);

        Assert.False(_coordinator.ValidateStateVersion(context, 3));
    }

    [Fact]
    public void ValidateStateVersion_VersionZero_MatchesCorrectly()
    {
        var context = new SharedAgentContext(
            Guid.NewGuid(), Guid.NewGuid(), new List<string>(), null, null, 0);

        Assert.True(_coordinator.ValidateStateVersion(context, 0));
        Assert.False(_coordinator.ValidateStateVersion(context, 1));
    }

    [Fact]
    public void ValidateStateVersion_AfterHandoff_RequiresNewVersion()
    {
        var context = new SharedAgentContext(
            Guid.NewGuid(), Guid.NewGuid(), new List<string>(), null, null, 1);

        var afterHandoff = _coordinator.HandoffContext("A", "B", context);

        Assert.False(_coordinator.ValidateStateVersion(afterHandoff, 1));
        Assert.True(_coordinator.ValidateStateVersion(afterHandoff, 2));
    }

    #endregion

    #region SharedAgentContext Record

    [Fact]
    public void SharedAgentContext_WithExpression_CreatesNewInstance()
    {
        var original = new SharedAgentContext(
            SessionId: Guid.NewGuid(),
            GameId: Guid.NewGuid(),
            ConversationHistory: new List<string> { "test" },
            CurrentGameState: "{\"state\":1}",
            LastAgentUsed: "Agent1",
            StateVersion: 3
        );

        var modified = original with { StateVersion = 4, LastAgentUsed = "Agent2" };

        Assert.Equal(3, original.StateVersion);
        Assert.Equal("Agent1", original.LastAgentUsed);
        Assert.Equal(4, modified.StateVersion);
        Assert.Equal("Agent2", modified.LastAgentUsed);
        Assert.Equal(original.SessionId, modified.SessionId);
    }

    [Fact]
    public void SharedAgentContext_Equality_BasedOnValues()
    {
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var history = new List<string>();

        var a = new SharedAgentContext(sessionId, gameId, history, null, null, 1);
        var b = new SharedAgentContext(sessionId, gameId, history, null, null, 1);

        Assert.Equal(a, b);
    }

    #endregion

    #region Integration Scenarios

    [Fact]
    public async Task FullWorkflow_LoadContextThenHandoffThenValidate()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var memories = new List<ConversationMemory>
        {
            new(Guid.NewGuid(), sessionId, userId, gameId, "Is this move valid?", "user"),
        };

        var snapshot = new AgentGameStateSnapshot(
            Guid.NewGuid(), gameId, sessionId, "{\"board\":\"initial\"}", 1);

        _mockConversationRepo
            .Setup(r => r.GetBySessionIdAsync(sessionId, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(memories);

        _mockGameStateRepo
            .Setup(r => r.GetLatestByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        // Act - Load context
        var context = await _coordinator.GetSharedContextAsync(sessionId, gameId);
        Assert.Equal(1, context.StateVersion);
        Assert.True(_coordinator.ValidateStateVersion(context, 1));

        // Act - Handoff from Tutor to Arbitro
        var afterHandoff = _coordinator.HandoffContext("TutorAgent", "ArbitroAgent", context);
        Assert.Equal(2, afterHandoff.StateVersion);
        Assert.Equal("TutorAgent", afterHandoff.LastAgentUsed);

        // Act - Validate with old version fails
        Assert.False(_coordinator.ValidateStateVersion(afterHandoff, 1));
        Assert.True(_coordinator.ValidateStateVersion(afterHandoff, 2));

        // Act - Second handoff
        var afterSecondHandoff = _coordinator.HandoffContext("ArbitroAgent", "DecisoreAgent", afterHandoff);
        Assert.Equal(3, afterSecondHandoff.StateVersion);
        Assert.Equal("ArbitroAgent", afterSecondHandoff.LastAgentUsed);

        // Assert - all original data preserved through handoffs
        Assert.Single(afterSecondHandoff.ConversationHistory);
        Assert.Equal("{\"board\":\"initial\"}", afterSecondHandoff.CurrentGameState);
    }

    #endregion
}
