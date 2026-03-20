using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Tests.Infrastructure;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// Integration tests for AgentRepository using Testcontainers.
/// Issue #866: AI Agents Entity & Configuration
/// Issue #2577: Migrated to SharedDatabaseTestBase for connection pool stability.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
internal class AgentRepositoryTests : SharedDatabaseTestBase<AgentRepository>
{
    public AgentRepositoryTests(SharedTestcontainersFixture fixture) : base(fixture)
    {
    }

    protected override AgentRepository CreateRepository(MeepleAiDbContext dbContext)
        => new AgentRepository(dbContext, MockEventCollector.Object);

    [Fact]
    public async Task AddAsync_ValidAgent_AddsToDatabase()
    {
        // Arrange
        var agent = CreateTestAgent("Test Agent");

        // Act
        await Repository.AddAsync(agent, CancellationToken.None);

        // Assert
        var retrieved = await Repository.GetByIdAsync(agent.Id, CancellationToken.None);
        retrieved.Should().NotBeNull();
        retrieved.Name.Should().Be(agent.Name);
        retrieved.Type.Value.Should().Be(agent.Type.Value);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingAgent_ReturnsAgent()
    {
        // Arrange
        var agent = CreateTestAgent("Test Agent");
        await Repository.AddAsync(agent, CancellationToken.None);

        // Act
        var retrieved = await Repository.GetByIdAsync(agent.Id, CancellationToken.None);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved.Id.Should().Be(agent.Id);
        retrieved.Name.Should().Be(agent.Name);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingAgent_ReturnsNull()
    {
        // Act
        var retrieved = await Repository.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task GetByNameAsync_ExistingAgent_ReturnsAgent()
    {
        // Arrange
        var agent = CreateTestAgent("Unique Agent");
        await Repository.AddAsync(agent, CancellationToken.None);

        // Act
        var retrieved = await Repository.GetByNameAsync("Unique Agent", CancellationToken.None);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved.Id.Should().Be(agent.Id);
    }

    [Fact]
    public async Task GetAllActiveAsync_ReturnsOnlyActiveAgents()
    {
        // Arrange
        var activeAgent1 = CreateTestAgent("Active 1", isActive: true);
        var activeAgent2 = CreateTestAgent("Active 2", isActive: true);
        var inactiveAgent = CreateTestAgent("Inactive", isActive: false);

        await Repository.AddAsync(activeAgent1, CancellationToken.None);
        await Repository.AddAsync(activeAgent2, CancellationToken.None);
        await Repository.AddAsync(inactiveAgent, CancellationToken.None);

        // Act
        var activeAgents = await Repository.GetAllActiveAsync(CancellationToken.None);

        // Assert
        activeAgents.Count.Should().Be(2);
        Assert.All(activeAgents, a => a.IsActive.Should().BeTrue());
    }

    [Fact]
    public async Task GetByTypeAsync_ReturnsAgentsOfSpecifiedType()
    {
        // Arrange
        var ragAgent1 = CreateTestAgent("RAG 1", AgentType.RagAgent);
        var ragAgent2 = CreateTestAgent("RAG 2", AgentType.RagAgent);
        var citationAgent = CreateTestAgent("Citation", AgentType.CitationAgent);

        await Repository.AddAsync(ragAgent1, CancellationToken.None);
        await Repository.AddAsync(ragAgent2, CancellationToken.None);
        await Repository.AddAsync(citationAgent, CancellationToken.None);

        // Act
        var ragAgents = await Repository.GetByTypeAsync(AgentType.RagAgent, CancellationToken.None);

        // Assert
        ragAgents.Count.Should().Be(2);
        Assert.All(ragAgents, a => a.Type.Value.Should().Be("RAG"));
    }

    [Fact]
    public async Task UpdateAsync_ModifiesExistingAgent()
    {
        // Arrange
        var agent = CreateTestAgent("Original Name");
        await Repository.AddAsync(agent, CancellationToken.None);

        agent.Rename("Updated Name");
        agent.Configure(AgentStrategy.VectorOnly());

        // Act
        await Repository.UpdateAsync(agent, CancellationToken.None);

        // Assert
        var retrieved = await Repository.GetByIdAsync(agent.Id, CancellationToken.None);
        retrieved!.Name.Should().Be("Updated Name");
        retrieved.Strategy.Name.Should().Be("VectorOnly");
    }

    [Fact]
    public async Task DeleteAsync_RemovesAgent()
    {
        // Arrange
        var agent = CreateTestAgent("To Delete");
        await Repository.AddAsync(agent, CancellationToken.None);

        // Act
        await Repository.DeleteAsync(agent.Id, CancellationToken.None);

        // Assert
        var retrieved = await Repository.GetByIdAsync(agent.Id, CancellationToken.None);
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task ExistsAsync_ExistingAgent_ReturnsTrue()
    {
        // Arrange
        var agent = CreateTestAgent("Exists");
        await Repository.AddAsync(agent, CancellationToken.None);

        // Act
        var exists = await Repository.ExistsAsync("Exists", CancellationToken.None);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_NonExistingAgent_ReturnsFalse()
    {
        // Act
        var exists = await Repository.ExistsAsync("Does Not Exist", CancellationToken.None);

        // Assert
        exists.Should().BeFalse();
    }

    // Helper methods
    private static Agent CreateTestAgent(
        string name,
        AgentType? type = null,
        bool isActive = true)
    {
        return new Agent(
            id: Guid.NewGuid(),
            name: name,
            type: type ?? AgentType.RagAgent,
            strategy: AgentStrategy.HybridSearch(),
            isActive: isActive
        );
    }
}
