using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

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
        Action act = () =>
            new AgentStateCoordinator(null!, _mockGameStateRepo.Object, _mockLogger.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullGameStateRepo_ThrowsArgumentNullException()
    {
        Action act = () =>
            new AgentStateCoordinator(_mockConversationRepo.Object, null!, _mockLogger.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        Action act = () =>
            new AgentStateCoordinator(_mockConversationRepo.Object, _mockGameStateRepo.Object, null!);
        act.Should().Throw<ArgumentNullException>();
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
        context.SessionId.Should().Be(sessionId);
        context.GameId.Should().Be(gameId);
        context.ConversationHistory.Count.Should().Be(3);
        context.ConversationHistory.Should().Contain("[user] Hello");
        context.ConversationHistory.Should().Contain("[assistant] Hi there");
        context.CurrentGameState.Should().Be("{\"board\":\"state\"}");
        context.StateVersion.Should().Be(5);
        context.LastAgentUsed.Should().BeNull();
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
        context.ConversationHistory.Should().BeEmpty();
        context.CurrentGameState.Should().BeNull();
        context.StateVersion.Should().Be(1); // Default version
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
        context.ConversationHistory.Should().ContainSingle();
        context.CurrentGameState.Should().BeNull();
        context.StateVersion.Should().Be(1);
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
        context.ConversationHistory.Should().BeEmpty();
        context.CurrentGameState.Should().BeNull();
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
        context.CurrentGameState.Should().BeNull();
        context.StateVersion.Should().Be(1);
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
        context.ConversationHistory[0].Should().Be("[user] What are the rules?");
        context.ConversationHistory[1].Should().Be("[assistant] Here are the rules...");
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
        context.StateVersion.Should().Be(42);
        context.CurrentGameState.Should().Be("{\"turn\":42}");
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
        result.LastAgentUsed.Should().Be("TutorAgent");
        result.StateVersion.Should().Be(2);
        result.SessionId.Should().Be(context.SessionId);
        result.GameId.Should().Be(context.GameId);
        result.ConversationHistory.Should().BeEquivalentTo(context.ConversationHistory);
        result.CurrentGameState.Should().Be(context.CurrentGameState);
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
        after1.StateVersion.Should().Be(2);
        after2.StateVersion.Should().Be(3);
        after3.StateVersion.Should().Be(4);
        after1.LastAgentUsed.Should().Be("TutorAgent");
        after2.LastAgentUsed.Should().Be("ArbitroAgent");
        after3.LastAgentUsed.Should().Be("DecisoreAgent");
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
        context.LastAgentUsed.Should().BeNull();
        context.StateVersion.Should().Be(5);
        // Result is new
        result.LastAgentUsed.Should().Be("Agent1");
        result.StateVersion.Should().Be(6);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void HandoffContext_EmptyFromAgent_ThrowsArgumentException(string? fromAgent)
    {
        var context = new SharedAgentContext(
            Guid.NewGuid(), Guid.NewGuid(), new List<string>(), null, null, 1);

        Action act = () =>
            _coordinator.HandoffContext(fromAgent!, "TargetAgent", context);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void HandoffContext_EmptyToAgent_ThrowsArgumentException(string? toAgent)
    {
        var context = new SharedAgentContext(
            Guid.NewGuid(), Guid.NewGuid(), new List<string>(), null, null, 1);

        Action act = () =>
            _coordinator.HandoffContext("SourceAgent", toAgent!, context);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void HandoffContext_NullContext_ThrowsArgumentNullException()
    {
        Action act = () =>
            _coordinator.HandoffContext("Agent1", "Agent2", null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region ValidateStateVersion

    [Fact]
    public void ValidateStateVersion_MatchingVersion_ReturnsTrue()
    {
        var context = new SharedAgentContext(
            Guid.NewGuid(), Guid.NewGuid(), new List<string>(), null, null, 5);

        _coordinator.ValidateStateVersion(context, 5).Should().BeTrue();
    }

    [Fact]
    public void ValidateStateVersion_MismatchedVersion_ReturnsFalse()
    {
        var context = new SharedAgentContext(
            Guid.NewGuid(), Guid.NewGuid(), new List<string>(), null, null, 5);

        _coordinator.ValidateStateVersion(context, 3).Should().BeFalse();
    }

    [Fact]
    public void ValidateStateVersion_VersionZero_MatchesCorrectly()
    {
        var context = new SharedAgentContext(
            Guid.NewGuid(), Guid.NewGuid(), new List<string>(), null, null, 0);

        _coordinator.ValidateStateVersion(context, 0).Should().BeTrue();
        _coordinator.ValidateStateVersion(context, 1).Should().BeFalse();
    }

    [Fact]
    public void ValidateStateVersion_AfterHandoff_RequiresNewVersion()
    {
        var context = new SharedAgentContext(
            Guid.NewGuid(), Guid.NewGuid(), new List<string>(), null, null, 1);

        var afterHandoff = _coordinator.HandoffContext("A", "B", context);

        _coordinator.ValidateStateVersion(afterHandoff, 1).Should().BeFalse();
        _coordinator.ValidateStateVersion(afterHandoff, 2).Should().BeTrue();
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

        original.StateVersion.Should().Be(3);
        original.LastAgentUsed.Should().Be("Agent1");
        modified.StateVersion.Should().Be(4);
        modified.LastAgentUsed.Should().Be("Agent2");
        modified.SessionId.Should().Be(original.SessionId);
    }

    [Fact]
    public void SharedAgentContext_Equality_BasedOnValues()
    {
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var history = new List<string>();

        var a = new SharedAgentContext(sessionId, gameId, history, null, null, 1);
        var b = new SharedAgentContext(sessionId, gameId, history, null, null, 1);

        b.Should().Be(a);
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
        context.StateVersion.Should().Be(1);
        _coordinator.ValidateStateVersion(context, 1).Should().BeTrue();

        // Act - Handoff from Tutor to Arbitro
        var afterHandoff = _coordinator.HandoffContext("TutorAgent", "ArbitroAgent", context);
        afterHandoff.StateVersion.Should().Be(2);
        afterHandoff.LastAgentUsed.Should().Be("TutorAgent");

        // Act - Validate with old version fails
        _coordinator.ValidateStateVersion(afterHandoff, 1).Should().BeFalse();
        _coordinator.ValidateStateVersion(afterHandoff, 2).Should().BeTrue();

        // Act - Second handoff
        var afterSecondHandoff = _coordinator.HandoffContext("ArbitroAgent", "DecisoreAgent", afterHandoff);
        afterSecondHandoff.StateVersion.Should().Be(3);
        afterSecondHandoff.LastAgentUsed.Should().Be("ArbitroAgent");

        // Assert - all original data preserved through handoffs
        afterSecondHandoff.ConversationHistory.Should().ContainSingle();
        afterSecondHandoff.CurrentGameState.Should().Be("{\"board\":\"initial\"}");
    }

    #endregion
}
