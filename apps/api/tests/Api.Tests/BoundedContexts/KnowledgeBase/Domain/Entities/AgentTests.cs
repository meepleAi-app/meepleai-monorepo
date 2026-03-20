using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// Unit tests for Agent aggregate root.
/// Issue #866: AI Agents Entity & Configuration
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class AgentTests
{
    [Fact]
    public void Constructor_ValidParameters_CreatesAgent()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "Test Agent";
        var type = AgentType.RagAgent;
        var strategy = AgentStrategy.HybridSearch();

        // Act
        var agent = new Agent(id, name, type, strategy);

        // Assert
        agent.Id.Should().Be(id);
        agent.Name.Should().Be(name);
        agent.Type.Should().Be(type);
        agent.Strategy.Should().Be(strategy);
        agent.IsActive.Should().BeTrue();
        agent.InvocationCount.Should().Be(0);
        agent.LastInvokedAt.Should().BeNull();
    }

    [Fact]
    public void Constructor_EmptyName_ThrowsArgumentException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var type = AgentType.RagAgent;
        var strategy = AgentStrategy.HybridSearch();

        // Act & Assert
        ((Action)(() => new Agent(id, "", type, strategy))).Should().Throw<ArgumentException>();
        ((Action)(() => new Agent(id, "   ", type, strategy))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_NullType_ThrowsArgumentNullException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "Test Agent";
        var strategy = AgentStrategy.HybridSearch();

        // Act & Assert
        ((Action)(() => new Agent(id, name, null!, strategy))).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullStrategy_ThrowsArgumentNullException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "Test Agent";
        var type = AgentType.RagAgent;

        // Act & Assert
        ((Action)(() => new Agent(id, name, type, null!))).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Configure_ValidStrategy_UpdatesStrategy()
    {
        // Arrange
        var agent = CreateTestAgent();
        var newStrategy = AgentStrategy.VectorOnly(topK: 20);

        // Act
        agent.Configure(newStrategy);

        // Assert
        agent.Strategy.Should().Be(newStrategy);
        agent.Strategy.Name.Should().Be("VectorOnly");
    }

    [Fact]
    public void Configure_NullStrategy_ThrowsArgumentNullException()
    {
        // Arrange
        var agent = CreateTestAgent();

        // Act & Assert
        ((Action)(() => agent.Configure(null!))).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Activate_InactiveAgent_SetsIsActiveTrue()
    {
        // Arrange
        var agent = CreateTestAgent(isActive: false);

        // Act
        agent.Activate();

        // Assert
        agent.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Activate_AlreadyActiveAgent_RemainsActive()
    {
        // Arrange
        var agent = CreateTestAgent(isActive: true);

        // Act
        agent.Activate();

        // Assert
        agent.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Deactivate_ActiveAgent_SetsIsActiveFalse()
    {
        // Arrange
        var agent = CreateTestAgent(isActive: true);

        // Act
        agent.Deactivate();

        // Assert
        agent.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Deactivate_AlreadyInactiveAgent_RemainsInactive()
    {
        // Arrange
        var agent = CreateTestAgent(isActive: false);

        // Act
        agent.Deactivate();

        // Assert
        agent.IsActive.Should().BeFalse();
    }

    [Fact]
    public void RecordInvocation_ActiveAgent_IncrementsCountAndUpdatesTimestamp()
    {
        // Arrange
        var agent = CreateTestAgent();
        var beforeInvocation = DateTime.UtcNow;

        // Act
        agent.RecordInvocation("test query", TokenUsage.Empty);

        // Assert
        agent.InvocationCount.Should().Be(1);
        agent.LastInvokedAt.Should().NotBeNull();
        (agent.LastInvokedAt >= beforeInvocation).Should().BeTrue();
        (agent.LastInvokedAt <= DateTime.UtcNow).Should().BeTrue();
    }

    [Fact]
    public void RecordInvocation_MultipleInvocations_IncrementsCount()
    {
        // Arrange
        var agent = CreateTestAgent();

        // Act
        agent.RecordInvocation("test query 1", TokenUsage.Empty);
        agent.RecordInvocation("test query 2", TokenUsage.Empty);
        agent.RecordInvocation("test query 3", TokenUsage.Empty);

        // Assert
        agent.InvocationCount.Should().Be(3);
    }

    [Fact]
    public void RecordInvocation_InactiveAgent_ThrowsInvalidOperationException()
    {
        // Arrange
        var agent = CreateTestAgent(isActive: false);

        // Act & Assert
        var exception = ((Action)(() => agent.RecordInvocation("test query", TokenUsage.Empty))).Should().Throw<InvalidOperationException>().Which;
        exception.Message.ToLower().Should().Contain("inactive agent");
    }

    [Fact]
    public void Rename_ValidName_UpdatesName()
    {
        // Arrange
        var agent = CreateTestAgent();
        var newName = "Updated Agent Name";

        // Act
        agent.Rename(newName);

        // Assert
        agent.Name.Should().Be(newName);
    }

    [Fact]
    public void Rename_EmptyName_ThrowsArgumentException()
    {
        // Arrange
        var agent = CreateTestAgent();

        // Act & Assert
        ((Action)(() => agent.Rename(""))).Should().Throw<ArgumentException>();
        ((Action)(() => agent.Rename("   "))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Rename_NameTooLong_ThrowsArgumentException()
    {
        // Arrange
        var agent = CreateTestAgent();
        var longName = new string('A', 101); // Max is 100

        // Act & Assert
        ((Action)(() => agent.Rename(longName))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Rename_NameWithWhitespace_TrimsWhitespace()
    {
        // Arrange
        var agent = CreateTestAgent();
        var nameWithWhitespace = "  Test Agent  ";

        // Act
        agent.Rename(nameWithWhitespace);

        // Assert
        agent.Name.Should().Be("Test Agent");
    }

    [Fact]
    public void IsRecentlyUsed_InvokedWithinLast24Hours_ReturnsTrue()
    {
        // Arrange
        var agent = CreateTestAgent();
        agent.RecordInvocation("test query", TokenUsage.Empty);

        // Act
        var isRecentlyUsed = agent.IsRecentlyUsed;

        // Assert
        isRecentlyUsed.Should().BeTrue();
    }

    [Fact]
    public void IsRecentlyUsed_NeverInvoked_ReturnsFalse()
    {
        // Arrange
        var agent = CreateTestAgent();

        // Act
        var isRecentlyUsed = agent.IsRecentlyUsed;

        // Assert
        isRecentlyUsed.Should().BeFalse();
    }

    [Fact]
    public void IsIdle_NeverInvoked_ReturnsTrue()
    {
        // Arrange
        var agent = CreateTestAgent();

        // Act
        var isIdle = agent.IsIdle;

        // Assert
        isIdle.Should().BeTrue();
    }

    [Fact]
    public void IsIdle_InvokedRecently_ReturnsFalse()
    {
        // Arrange
        var agent = CreateTestAgent();
        agent.RecordInvocation("test query", TokenUsage.Empty);

        // Act
        var isIdle = agent.IsIdle;

        // Assert
        isIdle.Should().BeFalse();
    }

    // ── Issue #4682: Agent-Game Association + User Ownership ──

    [Fact]
    public void Constructor_WithGameIdAndUserId_SetsProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act
        var agent = new Agent(
            id: id,
            name: "Game Agent",
            type: AgentType.RagAgent,
            strategy: AgentStrategy.HybridSearch(),
            gameId: gameId,
            createdByUserId: userId
        );

        // Assert
        agent.GameId.Should().Be(gameId);
        agent.CreatedByUserId.Should().Be(userId);
    }

    [Fact]
    public void Constructor_WithoutGameIdAndUserId_SetsNullDefaults()
    {
        // Arrange & Act
        var agent = new Agent(
            id: Guid.NewGuid(),
            name: "System Agent",
            type: AgentType.RagAgent,
            strategy: AgentStrategy.HybridSearch()
        );

        // Assert
        agent.GameId.Should().BeNull();
        agent.CreatedByUserId.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithGameIdOnly_SetsGameIdAndNullUser()
    {
        // Arrange
        var gameId = Guid.NewGuid();

        // Act
        var agent = new Agent(
            id: Guid.NewGuid(),
            name: "Game-Only Agent",
            type: AgentType.RagAgent,
            strategy: AgentStrategy.HybridSearch(),
            gameId: gameId
        );

        // Assert
        agent.GameId.Should().Be(gameId);
        agent.CreatedByUserId.Should().BeNull();
    }

    // ── Issue #97: SetGameId / ClearGameId domain methods ──

    [Fact]
    public void SetGameId_ValidId_SetsGameId()
    {
        // Arrange
        var agent = CreateTestAgent();
        var gameId = Guid.NewGuid();

        // Act
        agent.SetGameId(gameId);

        // Assert
        agent.GameId.Should().Be(gameId);
    }

    [Fact]
    public void SetGameId_EmptyGuid_ThrowsArgumentException()
    {
        // Arrange
        var agent = CreateTestAgent();

        // Act & Assert
        ((Action)(() => agent.SetGameId(Guid.Empty))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetGameId_OverwritesExistingGameId()
    {
        // Arrange
        var agent = CreateTestAgent();
        var firstGameId = Guid.NewGuid();
        var secondGameId = Guid.NewGuid();
        agent.SetGameId(firstGameId);

        // Act
        agent.SetGameId(secondGameId);

        // Assert
        agent.GameId.Should().Be(secondGameId);
    }

    [Fact]
    public void ClearGameId_WithExistingGameId_SetsNull()
    {
        // Arrange
        var agent = CreateTestAgent();
        agent.SetGameId(Guid.NewGuid());

        // Act
        agent.ClearGameId();

        // Assert
        agent.GameId.Should().BeNull();
    }

    [Fact]
    public void ClearGameId_WithNoGameId_RemainsNull()
    {
        // Arrange
        var agent = CreateTestAgent();
        agent.GameId.Should().BeNull();

        // Act
        agent.ClearGameId();

        // Assert
        agent.GameId.Should().BeNull();
    }

    // Helper method
    private static Agent CreateTestAgent(
        string name = "Test Agent",
        bool isActive = true)
    {
        return new Agent(
            id: Guid.NewGuid(),
            name: name,
            type: AgentType.RagAgent,
            strategy: AgentStrategy.HybridSearch(),
            isActive: isActive
        );
    }
}

