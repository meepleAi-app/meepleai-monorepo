using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.Tests.Infrastructure;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// Integration tests for AgentRepository using Testcontainers.
/// Issue #866: AI Agents Entity & Configuration
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class AgentRepositoryTests : IAsyncLifetime
{
    private IContainer? _postgresContainer;
    private MeepleAiDbContext? _dbContext;
    private AgentRepository? _repository;
    private string? _connectionString;

    public async ValueTask InitializeAsync()
    {
        // Issue #2031 fix: Use ContainerBuilder instead of PostgreSqlBuilder
        // to avoid exec-based wait strategy that causes "cannot hijack" errors
        _postgresContainer = new ContainerBuilder()
            .WithImage("postgres:16-alpine")
            .WithEnvironment("POSTGRES_USER", "test")
            .WithEnvironment("POSTGRES_PASSWORD", "test")
            .WithEnvironment("POSTGRES_DB", "meepleai_test")
            .WithPortBinding(5432, assignRandomHostPort: true)
            .WithCleanUp(true)
            .Build();

        await _postgresContainer.StartAsync();

        var postgresPort = _postgresContainer.GetMappedPublicPort(5432);
        _connectionString = $"Host=localhost;Port={postgresPort};Database=meepleai_test;Username=test;Password=test;Ssl Mode=Disable;Trust Server Certificate=true;KeepAlive=30;Pooling=false;";

        // Issue #2031: Wait for PostgreSQL to accept connections with retry
        await TestcontainersWaitHelpers.WaitForPostgresReadyAsync(_connectionString);

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(_connectionString)
            .Options;

        var mockMediator = new Mock<IMediator>();
        var mockEventCollector = new Mock<IDomainEventCollector>();
        mockEventCollector.Setup(x => x.GetAndClearEvents()).Returns(new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>());
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        await _dbContext.Database.MigrateAsync();

        _repository = new AgentRepository(_dbContext, new Mock<IDomainEventCollector>().Object);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }

        if (_postgresContainer != null)
        {
            await _postgresContainer.DisposeAsync();
        }
    }

    [Fact]
    public async Task AddAsync_ValidAgent_AddsToDatabase()
    {
        // Arrange
        var agent = CreateTestAgent("Test Agent");

        // Act
        await _repository!.AddAsync(agent, CancellationToken.None);

        // Assert
        var retrieved = await _repository.GetByIdAsync(agent.Id, CancellationToken.None);
        Assert.NotNull(retrieved);
        Assert.Equal(agent.Name, retrieved.Name);
        Assert.Equal(agent.Type.Value, retrieved.Type.Value);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingAgent_ReturnsAgent()
    {
        // Arrange
        var agent = CreateTestAgent("Test Agent");
        await _repository!.AddAsync(agent, CancellationToken.None);

        // Act
        var retrieved = await _repository.GetByIdAsync(agent.Id, CancellationToken.None);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(agent.Id, retrieved.Id);
        Assert.Equal(agent.Name, retrieved.Name);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingAgent_ReturnsNull()
    {
        // Act
        var retrieved = await _repository!.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task GetByNameAsync_ExistingAgent_ReturnsAgent()
    {
        // Arrange
        var agent = CreateTestAgent("Unique Agent");
        await _repository!.AddAsync(agent, CancellationToken.None);

        // Act
        var retrieved = await _repository.GetByNameAsync("Unique Agent", CancellationToken.None);

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

        await _repository!.AddAsync(activeAgent1, CancellationToken.None);
        await _repository.AddAsync(activeAgent2, CancellationToken.None);
        await _repository.AddAsync(inactiveAgent, CancellationToken.None);

        // Act
        var activeAgents = await _repository.GetAllActiveAsync(CancellationToken.None);

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

        await _repository!.AddAsync(ragAgent1, CancellationToken.None);
        await _repository.AddAsync(ragAgent2, CancellationToken.None);
        await _repository.AddAsync(citationAgent, CancellationToken.None);

        // Act
        var ragAgents = await _repository.GetByTypeAsync(AgentType.RagAgent, CancellationToken.None);

        // Assert
        Assert.Equal(2, ragAgents.Count);
        Assert.All(ragAgents, a => Assert.Equal("RAG", a.Type.Value));
    }

    [Fact]
    public async Task UpdateAsync_ModifiesExistingAgent()
    {
        // Arrange
        var agent = CreateTestAgent("Original Name");
        await _repository!.AddAsync(agent, CancellationToken.None);

        agent.Rename("Updated Name");
        agent.Configure(AgentStrategy.VectorOnly());

        // Act
        await _repository.UpdateAsync(agent, CancellationToken.None);

        // Assert
        var retrieved = await _repository.GetByIdAsync(agent.Id, CancellationToken.None);
        Assert.Equal("Updated Name", retrieved!.Name);
        Assert.Equal("VectorOnly", retrieved.Strategy.Name);
    }

    [Fact]
    public async Task DeleteAsync_RemovesAgent()
    {
        // Arrange
        var agent = CreateTestAgent("To Delete");
        await _repository!.AddAsync(agent, CancellationToken.None);

        // Act
        await _repository.DeleteAsync(agent.Id, CancellationToken.None);

        // Assert
        var retrieved = await _repository.GetByIdAsync(agent.Id, CancellationToken.None);
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task ExistsAsync_ExistingAgent_ReturnsTrue()
    {
        // Arrange
        var agent = CreateTestAgent("Exists");
        await _repository!.AddAsync(agent, CancellationToken.None);

        // Act
        var exists = await _repository.ExistsAsync("Exists", CancellationToken.None);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task ExistsAsync_NonExistingAgent_ReturnsFalse()
    {
        // Act
        var exists = await _repository!.ExistsAsync("Does Not Exist", CancellationToken.None);

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