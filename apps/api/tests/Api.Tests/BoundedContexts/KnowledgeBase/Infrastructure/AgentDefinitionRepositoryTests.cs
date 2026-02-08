using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure;

/// <summary>
/// Integration tests for AgentDefinitionRepository (Issue #3808, Epic #3687)
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Epic", "3687")]
public sealed class AgentDefinitionRepositoryTests : IClassFixture<SharedTestcontainersFixture>, IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _databaseName;
    private string? _connectionString;

    public AgentDefinitionRepositoryTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _databaseName = $"test_agent_definition_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        using var dbContext = _fixture.CreateDbContext(_connectionString);
        await dbContext.Database.EnsureCreatedAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (!string.IsNullOrEmpty(_databaseName))
        {
            await _fixture.DropIsolatedDatabaseAsync(_databaseName);
        }
    }

    [Fact]
    public async Task AddAsync_WithValidAgentDefinition_ShouldPersist()
    {
        // Arrange
        using var dbContext = _fixture.CreateDbContext(_connectionString!);
        var repository = new AgentDefinitionRepository(dbContext);
        var agent = AgentDefinition.Create(
            "TestAgent",
            "Test description",
            AgentDefinitionConfig.Default());

        // Act
        await repository.AddAsync(agent);

        // Assert
        var retrieved = await repository.GetByIdAsync(agent.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("TestAgent");
        retrieved.Description.Should().Be("Test description");
    }

    [Fact]
    public async Task GetByNameAsync_WithExistingName_ShouldReturnAgent()
    {
        // Arrange
        using var dbContext = _fixture.CreateDbContext(_connectionString!);
        var repository = new AgentDefinitionRepository(dbContext);
        var agent = AgentDefinition.Create("UniqueAgent", "Desc", AgentDefinitionConfig.Default());
        await repository.AddAsync(agent);

        // Act
        var retrieved = await repository.GetByNameAsync("UniqueAgent");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(agent.Id);
    }

    [Fact]
    public async Task GetAllAsync_WithMultipleAgents_ShouldReturnAll()
    {
        // Arrange
        using var dbContext = _fixture.CreateDbContext(_connectionString!);
        var repository = new AgentDefinitionRepository(dbContext);
        await repository.AddAsync(AgentDefinition.Create("Agent1", "Desc1", AgentDefinitionConfig.Default()));
        await repository.AddAsync(AgentDefinition.Create("Agent2", "Desc2", AgentDefinitionConfig.Default()));

        // Act
        var agents = await repository.GetAllAsync();

        // Assert
        agents.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetAllActiveAsync_WithInactiveAgent_ShouldReturnOnlyActive()
    {
        // Arrange
        using var dbContext = _fixture.CreateDbContext(_connectionString!);
        var repository = new AgentDefinitionRepository(dbContext);
        var activeAgent = AgentDefinition.Create("ActiveAgent", "Desc", AgentDefinitionConfig.Default());
        var inactiveAgent = AgentDefinition.Create("InactiveAgent", "Desc", AgentDefinitionConfig.Default());
        inactiveAgent.Deactivate();

        await repository.AddAsync(activeAgent);
        await repository.AddAsync(inactiveAgent);

        // Act
        var agents = await repository.GetAllActiveAsync();

        // Assert
        agents.Should().Contain(a => a.Name == "ActiveAgent");
        agents.Should().NotContain(a => a.Name == "InactiveAgent");
    }

    [Fact]
    public async Task SearchAsync_WithMatchingName_ShouldReturnMatches()
    {
        // Arrange
        using var dbContext = _fixture.CreateDbContext(_connectionString!);
        var repository = new AgentDefinitionRepository(dbContext);
        await repository.AddAsync(AgentDefinition.Create("SearchableAgent", "Desc", AgentDefinitionConfig.Default()));
        await repository.AddAsync(AgentDefinition.Create("OtherAgent", "Desc", AgentDefinitionConfig.Default()));

        // Act
        var results = await repository.SearchAsync("searchable");

        // Assert
        results.Should().HaveCount(1);
        results[0].Name.Should().Be("SearchableAgent");
    }

    [Fact]
    public async Task UpdateAsync_WithModifiedAgent_ShouldPersistChanges()
    {
        // Arrange
        using var dbContext = _fixture.CreateDbContext(_connectionString!);
        var repository = new AgentDefinitionRepository(dbContext);
        var agent = AgentDefinition.Create("OriginalName", "Desc", AgentDefinitionConfig.Default());
        await repository.AddAsync(agent);

        // Act
        agent.UpdateNameAndDescription("UpdatedName", "Updated description");
        await repository.UpdateAsync(agent);

        // Assert
        var retrieved = await repository.GetByIdAsync(agent.Id);
        retrieved!.Name.Should().Be("UpdatedName");
        retrieved.Description.Should().Be("Updated description");
    }

    [Fact]
    public async Task DeleteAsync_WithExistingAgent_ShouldRemove()
    {
        // Arrange
        using var dbContext = _fixture.CreateDbContext(_connectionString!);
        var repository = new AgentDefinitionRepository(dbContext);
        var agent = AgentDefinition.Create("ToDelete", "Desc", AgentDefinitionConfig.Default());
        await repository.AddAsync(agent);

        // Act
        await repository.DeleteAsync(agent.Id);

        // Assert
        var retrieved = await repository.GetByIdAsync(agent.Id);
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task ExistsAsync_WithExistingName_ShouldReturnTrue()
    {
        // Arrange
        using var dbContext = _fixture.CreateDbContext(_connectionString!);
        var repository = new AgentDefinitionRepository(dbContext);
        await repository.AddAsync(AgentDefinition.Create("ExistingAgent", "Desc", AgentDefinitionConfig.Default()));

        // Act
        var exists = await repository.ExistsAsync("ExistingAgent");

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WithNonExistingName_ShouldReturnFalse()
    {
        // Arrange
        using var dbContext = _fixture.CreateDbContext(_connectionString!);
        var repository = new AgentDefinitionRepository(dbContext);

        // Act
        var exists = await repository.ExistsAsync("NonExistentAgent");

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task AddAsync_WithPromptsAndTools_ShouldPersistCorrectly()
    {
        // Arrange
        using var dbContext = _fixture.CreateDbContext(_connectionString!);
        var repository = new AgentDefinitionRepository(dbContext);
        var prompts = new List<AgentPromptTemplate>
        {
            AgentPromptTemplate.Create("system", "System prompt")
        };
        var tools = new List<AgentToolConfig>
        {
            AgentToolConfig.Create("web_search", new Dictionary<string, object> { ["max"] = 10 })
        };
        var agent = AgentDefinition.Create("ComplexAgent", "Desc", AgentDefinitionConfig.Default(), prompts, tools);

        // Act
        await repository.AddAsync(agent);

        // Assert
        var retrieved = await repository.GetByIdAsync(agent.Id);
        retrieved!.Prompts.Should().HaveCount(1);
        retrieved.Tools.Should().HaveCount(1);
        retrieved.Tools[0].Name.Should().Be("web_search");
    }
}
