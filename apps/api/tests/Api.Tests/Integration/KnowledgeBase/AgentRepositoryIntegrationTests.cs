using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// Integration tests for AgentRepository using SharedTestcontainersFixture.
/// Tests PostgreSQL persistence, agent lifecycle, activation/deactivation, and query methods.
/// Issue #2307: Week 3 - Repository integration testing
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "2307")]
public sealed class AgentRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IAgentRepository? _repository;
    private IUnitOfWork? _unitOfWork;
    private IServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    // Test data constants
    private static readonly Guid TestAgentId1 = new("50000000-0000-0000-0000-000000000001");
    private static readonly Guid TestAgentId2 = new("50000000-0000-0000-0000-000000000002");
    private static readonly Guid TestAgentId3 = new("50000000-0000-0000-0000-000000000003");

    public AgentRepositoryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        // Create isolated database for this test class
        _databaseName = $"test_agentrepo_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(_isolatedDbConnectionString, o => o.UseVector()); // Issue #3547: Enable pgvector
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        services.AddScoped<IAgentRepository, AgentRepository>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();
        services.AddScoped<IDomainEventCollector, DomainEventCollector>();

        // MediatR (required by MeepleAiDbContext)
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        _repository = _serviceProvider.GetRequiredService<IAgentRepository>();
        _unitOfWork = _serviceProvider.GetRequiredService<IUnitOfWork>();

        // Create database schema with Polly retry
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await _dbContext.Database.MigrateAsync(TestCancellationToken);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(TestConstants.Timing.RetryDelay, TestCancellationToken);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _dbContext?.Dispose();

        if (!string.IsNullOrEmpty(_databaseName))
        {
            try
            {
                await _fixture.DropIsolatedDatabaseAsync(_databaseName);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    #region Helper Methods

    private static Agent CreateTestAgent(
        Guid id,
        string name,
        AgentType? type = null,
        bool isActive = true)
    {
        var strategy = AgentStrategy.HybridSearch(vectorWeight: 0.7, topK: 5, minScore: 0.7);

        return new Agent(
            id: id,
            name: name,
            type: type ?? AgentType.RagAgent,
            strategy: strategy,
            isActive: isActive
        );
    }

    private async Task CleanDatabaseAsync()
    {
        if (_dbContext == null) return;

        await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM \"agents\"", TestCancellationToken);
    }

    /// <summary>
    /// Execute action in isolated scope with fresh DbContext to avoid tracking conflicts
    /// </summary>
    private async Task<T> ExecuteInScopeAsync<T>(Func<IAgentRepository, IUnitOfWork, Task<T>> action)
    {
        using var scope = _serviceProvider!.CreateScope();
        var scopedRepo = scope.ServiceProvider.GetRequiredService<IAgentRepository>();
        var scopedUow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        return await action(scopedRepo, scopedUow);
    }

    #endregion

    #region AddAsync Tests

    [Fact]
    public async Task AddAsync_NewAgent_ShouldPersistToDatabase()
    {
        // Arrange
        await CleanDatabaseAsync();
        var agent = CreateTestAgent(TestAgentId1, "Catan Expert");

        // Act
        await _repository!.AddAsync(agent, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Assert
        var persisted = await _repository.GetByIdAsync(TestAgentId1, TestCancellationToken);
        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be("Catan Expert");
        persisted.IsActive.Should().BeTrue();
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_ExistingAgent_ShouldReturnAgent()
    {
        // Arrange
        await CleanDatabaseAsync();
        var agent = CreateTestAgent(TestAgentId1, "Strategy Expert");
        await _repository!.AddAsync(agent, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.GetByIdAsync(TestAgentId1, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(TestAgentId1);
        result.Name.Should().Be("Strategy Expert");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingAgent_ShouldReturnNull()
    {
        // Arrange
        await CleanDatabaseAsync();

        // Act
        var result = await _repository!.GetByIdAsync(Guid.NewGuid(), TestCancellationToken);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_MultipleAgents_ShouldReturnAllOrdered()
    {
        // Arrange
        await CleanDatabaseAsync();
        var agent1 = CreateTestAgent(TestAgentId1, "Alpha Agent");
        var agent2 = CreateTestAgent(TestAgentId2, "Beta Agent");
        var agent3 = CreateTestAgent(TestAgentId3, "Gamma Agent");

        await _repository!.AddAsync(agent1, TestCancellationToken);
        await _repository.AddAsync(agent2, TestCancellationToken);
        await _repository.AddAsync(agent3, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.GetAllAsync(TestCancellationToken);

        // Assert
        result.Should().HaveCount(3);
        result.Should().BeInAscendingOrder(a => a.Name);
    }

    [Fact]
    public async Task GetAllAsync_EmptyDatabase_ShouldReturnEmptyList()
    {
        // Arrange
        await CleanDatabaseAsync();

        // Act
        var result = await _repository!.GetAllAsync(TestCancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_ModifyAgent_ShouldPersistChanges()
    {
        // Arrange
        await CleanDatabaseAsync();
        var agent = CreateTestAgent(TestAgentId1, "Original Name");
        await _repository!.AddAsync(agent, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act - Update in isolated scope
        await ExecuteInScopeAsync(async (repo, uow) =>
        {
            var loadedAgent = await repo.GetByIdAsync(TestAgentId1, TestCancellationToken);
            loadedAgent!.Rename("Updated Name");
            loadedAgent.Deactivate();
            await repo.UpdateAsync(loadedAgent, TestCancellationToken);
            await uow.SaveChangesAsync(TestCancellationToken);
            return true;
        });

        // Assert
        var updated = await _repository.GetByIdAsync(TestAgentId1, TestCancellationToken);
        updated!.Name.Should().Be("Updated Name");
        updated.IsActive.Should().BeFalse();
    }

    #endregion

    #region Complex Scenario Tests

    [Fact]
    public async Task ComplexScenario_AgentLifecycle_ShouldMaintainConsistency()
    {
        // Arrange
        await CleanDatabaseAsync();

        // 1. Create agent
        var agent = CreateTestAgent(TestAgentId1, "Rules Expert");
        await _repository!.AddAsync(agent, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // 2. Deactivate agent
        await ExecuteInScopeAsync(async (repo, uow) =>
        {
            var loadedAgent = await repo.GetByIdAsync(TestAgentId1, TestCancellationToken);
            loadedAgent!.Deactivate();
            await repo.UpdateAsync(loadedAgent, TestCancellationToken);
            await uow.SaveChangesAsync(TestCancellationToken);
            return true;
        });

        // 3. Verify inactive
        var inactive = await _repository.GetByIdAsync(TestAgentId1, TestCancellationToken);
        inactive!.IsActive.Should().BeFalse();

        // 4. Reactivate agent
        await ExecuteInScopeAsync(async (repo, uow) =>
        {
            var loadedAgent = await repo.GetByIdAsync(TestAgentId1, TestCancellationToken);
            loadedAgent!.Activate();
            await repo.UpdateAsync(loadedAgent, TestCancellationToken);
            await uow.SaveChangesAsync(TestCancellationToken);
            return true;
        });

        // Assert final state
        var final = await _repository.GetByIdAsync(TestAgentId1, TestCancellationToken);
        final!.IsActive.Should().BeTrue();
    }

    #endregion
}
