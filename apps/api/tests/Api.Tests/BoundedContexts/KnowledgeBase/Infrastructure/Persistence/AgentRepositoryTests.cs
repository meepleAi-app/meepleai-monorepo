using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Tests.Infrastructure;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// Integration tests for AgentRepository using Testcontainers.
/// Issue #866: AI Agents Entity & Configuration
/// Issue #2577: Migrated to SharedDatabaseTestBase for connection pool stability.
/// </summary>
[Collection("SharedTestcontainers")]
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
        Assert.NotNull(retrieved);
        Assert.Equal(agent.Name, retrieved.Name);
        Assert.Equal(agent.Type.Value, retrieved.Type.Value);
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
        Assert.NotNull(retrieved);
        Assert.Equal(agent.Id, retrieved.Id);
        Assert.Equal(agent.Name, retrieved.Name);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingAgent_ReturnsNull()
    {
        // Act
        var retrieved = await Repository.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.Null(retrieved);
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
        Assert.NotNull(retrieved);
        Assert.Equal(agent.Id, retrieved.Id);
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
        Assert.Equal(2, activeAgents.Count);
        Assert.All(activeAgents, a => Assert.True(a.IsActive));
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
        Assert.Equal(2, ragAgents.Count);
        Assert.All(ragAgents, a => Assert.Equal("RAG", a.Type.Value));
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
        Assert.Equal("Updated Name", retrieved!.Name);
        Assert.Equal("VectorOnly", retrieved.Strategy.Name);
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
        Assert.Null(retrieved);
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
        Assert.True(exists);
    }

    [Fact]
    public async Task ExistsAsync_NonExistingAgent_ReturnsFalse()
    {
        // Act
        var exists = await Repository.ExistsAsync("Does Not Exist", CancellationToken.None);

        // Assert
        Assert.False(exists);
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