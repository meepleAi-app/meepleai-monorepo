using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
public class StrategyModelMappingRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IStrategyModelMappingRepository? _repository;
    private IServiceProvider? _serviceProvider;

    public StrategyModelMappingRepositoryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        var databaseName = $"test_strategy_mapping_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);
        services.AddScoped<IStrategyModelMappingRepository, StrategyModelMappingRepository>();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        _repository = _serviceProvider.GetRequiredService<IStrategyModelMappingRepository>();

        // Create database schema
        await _dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
            await _dbContext.DisposeAsync();

        if (_serviceProvider is IDisposable disposable)
            disposable.Dispose();
    }

    /// <summary>
    /// Clears all strategy model mappings from the database.
    /// Call this in test Arrange phases to ensure clean state.
    /// </summary>
    private async Task ClearAllMappingsAsync()
    {
        var existing = await _dbContext!.Set<StrategyModelMappingEntity>().ToListAsync();
        if (existing.Any())
        {
            _dbContext.Set<StrategyModelMappingEntity>().RemoveRange(existing);
            await _dbContext.SaveChangesAsync();
            _dbContext.ChangeTracker.Clear();
        }
    }

    [Fact]
    public async Task GetByStrategyAsync_WhenStrategyExists_ReturnsMapping()
    {
        // Arrange
        await ClearAllMappingsAsync();

        var entity = new StrategyModelMappingEntity
        {
            Id = Guid.NewGuid(),
            Strategy = "FAST",
            PrimaryModel = "test-model",
            FallbackModels = new[] { "fallback-1" },
            Provider = "openrouter",
            IsCustomizable = false,
            AdminOnly = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext!.Set<StrategyModelMappingEntity>().Add(entity);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Act
        var result = await _repository!.GetByStrategyAsync(RagStrategy.Fast);

        // Assert
        result.Should().NotBeNull();
        result!.Strategy.Should().Be("FAST");
        result.PrimaryModel.Should().Be("test-model");
        result.FallbackModels.Should().ContainSingle().Which.Should().Be("fallback-1");
        result.Provider.Should().Be("openrouter");
        result.IsCustomizable.Should().BeFalse();
        result.AdminOnly.Should().BeFalse();
    }

    [Fact]
    public async Task GetByStrategyAsync_WhenStrategyDoesNotExist_ReturnsNull()
    {
        // Arrange
        await ClearAllMappingsAsync();

        // Act
        var result = await _repository!.GetByStrategyAsync(RagStrategy.Precise);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_WhenMultipleMappingsExist_ReturnsAll()
    {
        // Arrange
        await ClearAllMappingsAsync();

        var entities = new[]
        {
            new StrategyModelMappingEntity
            {
                Id = Guid.NewGuid(),
                Strategy = "FAST",
                PrimaryModel = "fast-model",
                FallbackModels = Array.Empty<string>(),
                Provider = "openrouter",
                IsCustomizable = false,
                AdminOnly = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new StrategyModelMappingEntity
            {
                Id = Guid.NewGuid(),
                Strategy = "BALANCED",
                PrimaryModel = "balanced-model",
                FallbackModels = new[] { "fallback" },
                Provider = "deepseek",
                IsCustomizable = false,
                AdminOnly = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        _dbContext!.Set<StrategyModelMappingEntity>().AddRange(entities);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Act
        var result = await _repository!.GetAllAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(m => m.Strategy == "FAST");
        result.Should().Contain(m => m.Strategy == "BALANCED");
    }

    [Fact]
    public async Task GetAllAsync_WhenNoMappings_ReturnsEmptyList()
    {
        // Arrange
        await ClearAllMappingsAsync();

        // Act
        var result = await _repository!.GetAllAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task HasMappingAsync_WhenStrategyExists_ReturnsTrue()
    {
        // Arrange
        await ClearAllMappingsAsync();

        var entity = new StrategyModelMappingEntity
        {
            Id = Guid.NewGuid(),
            Strategy = "EXPERT",
            PrimaryModel = "expert-model",
            FallbackModels = Array.Empty<string>(),
            Provider = "anthropic",
            IsCustomizable = false,
            AdminOnly = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext!.Set<StrategyModelMappingEntity>().Add(entity);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Act
        var result = await _repository!.HasMappingAsync(RagStrategy.Expert);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasMappingAsync_WhenStrategyDoesNotExist_ReturnsFalse()
    {
        // Arrange
        await ClearAllMappingsAsync();

        // Act
        var result = await _repository!.HasMappingAsync(RagStrategy.Consensus);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetByStrategyAsync_UsesAsNoTracking()
    {
        // Arrange
        await ClearAllMappingsAsync();

        var entity = new StrategyModelMappingEntity
        {
            Id = Guid.NewGuid(),
            Strategy = "BALANCED",
            PrimaryModel = "test-model",
            FallbackModels = Array.Empty<string>(),
            Provider = "openrouter",
            IsCustomizable = false,
            AdminOnly = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext!.Set<StrategyModelMappingEntity>().Add(entity);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear(); // Clear after save to test AsNoTracking

        // Act
        await _repository!.GetByStrategyAsync(RagStrategy.Balanced);

        // Assert
        var trackedEntities = _dbContext.ChangeTracker.Entries<StrategyModelMappingEntity>().ToList();
        trackedEntities.Should().BeEmpty("because repository should use AsNoTracking()");
    }

    [Fact]
    public async Task GetByStrategyAsync_WithAdminOnlyMapping_IncludesAdminFlag()
    {
        // Arrange
        await ClearAllMappingsAsync();

        var entity = new StrategyModelMappingEntity
        {
            Id = Guid.NewGuid(),
            Strategy = "CUSTOM",
            PrimaryModel = "admin-model",
            FallbackModels = Array.Empty<string>(),
            Provider = "anthropic",
            IsCustomizable = true,
            AdminOnly = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext!.Set<StrategyModelMappingEntity>().Add(entity);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Act
        var result = await _repository!.GetByStrategyAsync(RagStrategy.Custom);

        // Assert
        result.Should().NotBeNull();
        result!.AdminOnly.Should().BeTrue();
    }
}
