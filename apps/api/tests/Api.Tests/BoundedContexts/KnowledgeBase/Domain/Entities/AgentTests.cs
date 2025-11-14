using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// Unit tests for Agent aggregate root.
/// Issue #866: AI Agents Entity & Configuration
/// </summary>
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
        Assert.Equal(id, agent.Id);
        Assert.Equal(name, agent.Name);
        Assert.Equal(type, agent.Type);
        Assert.Equal(strategy, agent.Strategy);
        Assert.True(agent.IsActive);
        Assert.Equal(0, agent.InvocationCount);
        Assert.Null(agent.LastInvokedAt);
    }

    [Fact]
    public void Constructor_EmptyName_ThrowsArgumentException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var type = AgentType.RagAgent;
        var strategy = AgentStrategy.HybridSearch();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new Agent(id, "", type, strategy));
        Assert.Throws<ArgumentException>(() => new Agent(id, "   ", type, strategy));
    }

    [Fact]
    public void Constructor_NullType_ThrowsArgumentNullException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "Test Agent";
        var strategy = AgentStrategy.HybridSearch();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new Agent(id, name, null!, strategy));
    }

    [Fact]
    public void Constructor_NullStrategy_ThrowsArgumentNullException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "Test Agent";
        var type = AgentType.RagAgent;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new Agent(id, name, type, null!));
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
        Assert.Equal(newStrategy, agent.Strategy);
        Assert.Equal("VectorOnly", agent.Strategy.Name);
    }

    [Fact]
    public void Configure_NullStrategy_ThrowsArgumentNullException()
    {
        // Arrange
        var agent = CreateTestAgent();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => agent.Configure(null!));
    }

    [Fact]
    public void Activate_InactiveAgent_SetsIsActiveTrue()
    {
        // Arrange
        var agent = CreateTestAgent(isActive: false);

        // Act
        agent.Activate();

        // Assert
        Assert.True(agent.IsActive);
    }

    [Fact]
    public void Activate_AlreadyActiveAgent_RemainsActive()
    {
        // Arrange
        var agent = CreateTestAgent(isActive: true);

        // Act
        agent.Activate();

        // Assert
        Assert.True(agent.IsActive);
    }

    [Fact]
    public void Deactivate_ActiveAgent_SetsIsActiveFalse()
    {
        // Arrange
        var agent = CreateTestAgent(isActive: true);

        // Act
        agent.Deactivate();

        // Assert
        Assert.False(agent.IsActive);
    }

    [Fact]
    public void Deactivate_AlreadyInactiveAgent_RemainsInactive()
    {
        // Arrange
        var agent = CreateTestAgent(isActive: false);

        // Act
        agent.Deactivate();

        // Assert
        Assert.False(agent.IsActive);
    }

    [Fact]
    public void RecordInvocation_ActiveAgent_IncrementsCountAndUpdatesTimestamp()
    {
        // Arrange
        var agent = CreateTestAgent();
        var beforeInvocation = DateTime.UtcNow;

        // Act
        agent.RecordInvocation();

        // Assert
        Assert.Equal(1, agent.InvocationCount);
        Assert.NotNull(agent.LastInvokedAt);
        Assert.True(agent.LastInvokedAt >= beforeInvocation);
        Assert.True(agent.LastInvokedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void RecordInvocation_MultipleInvocations_IncrementsCount()
    {
        // Arrange
        var agent = CreateTestAgent();

        // Act
        agent.RecordInvocation();
        agent.RecordInvocation();
        agent.RecordInvocation();

        // Assert
        Assert.Equal(3, agent.InvocationCount);
    }

    [Fact]
    public void RecordInvocation_InactiveAgent_ThrowsInvalidOperationException()
    {
        // Arrange
        var agent = CreateTestAgent(isActive: false);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => agent.RecordInvocation());
        Assert.Contains("inactive agent", exception.Message.ToLower());
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
        Assert.Equal(newName, agent.Name);
    }

    [Fact]
    public void Rename_EmptyName_ThrowsArgumentException()
    {
        // Arrange
        var agent = CreateTestAgent();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => agent.Rename(""));
        Assert.Throws<ArgumentException>(() => agent.Rename("   "));
    }

    [Fact]
    public void Rename_NameTooLong_ThrowsArgumentException()
    {
        // Arrange
        var agent = CreateTestAgent();
        var longName = new string('A', 101); // Max is 100

        // Act & Assert
        Assert.Throws<ArgumentException>(() => agent.Rename(longName));
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
        Assert.Equal("Test Agent", agent.Name);
    }

    [Fact]
    public void IsRecentlyUsed_InvokedWithinLast24Hours_ReturnsTrue()
    {
        // Arrange
        var agent = CreateTestAgent();
        agent.RecordInvocation();

        // Act
        var isRecentlyUsed = agent.IsRecentlyUsed;

        // Assert
        Assert.True(isRecentlyUsed);
    }

    [Fact]
    public void IsRecentlyUsed_NeverInvoked_ReturnsFalse()
    {
        // Arrange
        var agent = CreateTestAgent();

        // Act
        var isRecentlyUsed = agent.IsRecentlyUsed;

        // Assert
        Assert.False(isRecentlyUsed);
    }

    [Fact]
    public void IsIdle_NeverInvoked_ReturnsTrue()
    {
        // Arrange
        var agent = CreateTestAgent();

        // Act
        var isIdle = agent.IsIdle;

        // Assert
        Assert.True(isIdle);
    }

    [Fact]
    public void IsIdle_InvokedRecently_ReturnsFalse()
    {
        // Arrange
        var agent = CreateTestAgent();
        agent.RecordInvocation();

        // Act
        var isIdle = agent.IsIdle;

        // Assert
        Assert.False(isIdle);
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
