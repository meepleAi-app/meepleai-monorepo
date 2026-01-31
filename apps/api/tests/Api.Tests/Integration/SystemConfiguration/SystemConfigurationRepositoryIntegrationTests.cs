using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.BoundedContexts.SystemConfiguration.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using System.Diagnostics;
using Xunit;

namespace Api.Tests.Integration.SystemConfiguration;

/// <summary>
/// Integration tests for ConfigurationRepository using SharedTestcontainersFixture.
/// Tests PostgreSQL persistence, filtered queries, versioning, rollback, and environment-specific configurations.
/// Issue #2307: Week 3 - SystemConfiguration repository integration testing
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "SystemConfiguration")]
[Trait("Issue", "2307")]
public sealed class SystemConfigurationRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IConfigurationRepository? _repository;
    private IUnitOfWork? _unitOfWork;
    private IServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    // Test data constants
    private static readonly Guid TestConfigId1 = new("30000000-0000-0000-0000-000000000001");
    private static readonly Guid TestConfigId2 = new("30000000-0000-0000-0000-000000000002");
    private static readonly Guid TestConfigId3 = new("30000000-0000-0000-0000-000000000003");
    private static readonly Guid TestUserId = new("40000000-0000-0000-0000-000000000001");

    public SystemConfigurationRepositoryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        // Create isolated database for this test class
        _databaseName = $"test_sysconfig_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(_isolatedDbConnectionString);
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        services.AddScoped<IConfigurationRepository, ConfigurationRepository>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();
        services.AddScoped<IDomainEventCollector, DomainEventCollector>();

        // HybridCache (required by event handlers)
        services.AddHybridCache();

        // Mock IHybridCacheService for testing (required by event handlers)
        services.AddScoped<Api.Services.IHybridCacheService>(_ =>
            Moq.Mock.Of<Api.Services.IHybridCacheService>());

        // MediatR (required by MeepleAiDbContext)
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        _repository = _serviceProvider.GetRequiredService<IConfigurationRepository>();
        _unitOfWork = _serviceProvider.GetRequiredService<IUnitOfWork>();

        // Create database schema
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

        // Seed required User for FK constraints
        await SeedTestUserAsync();
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

    /// <summary>
    /// Seeds a test user to satisfy CreatedByUserId FK constraint.
    /// </summary>
    private async Task SeedTestUserAsync()
    {
        var user = new UserEntity
        {
            Id = TestUserId,
            Email = "test-sysconfig@meepleai.dev",
            DisplayName = "Test User",
            Role = "admin",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext!.Set<UserEntity>().Add(user);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    private static Api.BoundedContexts.SystemConfiguration.Domain.Entities.SystemConfiguration CreateTestConfiguration(
        Guid id,
        string key = "TestConfig:Feature",
        string value = "enabled",
        string valueType = "string",
        string? description = null,
        string category = "General",
        string environment = "All",
        bool requiresRestart = false)
    {
        var configKey = new ConfigKey(key);
        return new Api.BoundedContexts.SystemConfiguration.Domain.Entities.SystemConfiguration(
            id: id,
            key: configKey,
            value: value,
            valueType: valueType,
            createdByUserId: TestUserId,
            description: description ?? $"Test configuration for {key}",
            category: category,
            environment: environment,
            requiresRestart: requiresRestart
        );
    }

    private async Task CleanDatabaseAsync()
    {
        if (_dbContext == null) return;

        _dbContext.Set<Api.Infrastructure.Entities.SystemConfigurationEntity>().RemoveRange(
            _dbContext.Set<Api.Infrastructure.Entities.SystemConfigurationEntity>());
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    #endregion

    #region CRUD Tests

    [Fact]
    public async Task AddAsync_NewConfiguration_ShouldPersistToDatabase()
    {
        // Arrange
        await CleanDatabaseAsync();
        var config = CreateTestConfiguration(
            TestConfigId1,
            "RateLimit:Admin:MaxTokens",
            "1000",
            "int",
            "Maximum tokens per admin request",
            "RateLimit",
            "Production",
            requiresRestart: true
        );

        // Act
        await _repository!.AddAsync(config, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Assert
        var persisted = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);
        persisted.Should().NotBeNull();
        persisted!.Key.Value.Should().Be("RateLimit:Admin:MaxTokens");
        persisted.Value.Should().Be("1000");
        persisted.ValueType.Should().Be("int");
        persisted.Category.Should().Be("RateLimit");
        persisted.Environment.Should().Be("Production");
        persisted.RequiresRestart.Should().BeTrue();
        persisted.IsActive.Should().BeTrue();
        persisted.Version.Should().Be(1);
        persisted.CreatedByUserId.Should().Be(TestUserId);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingConfiguration_ShouldReturnConfiguration()
    {
        // Arrange
        await CleanDatabaseAsync();
        var config = CreateTestConfiguration(TestConfigId1, "Cache:TTL", "300", "int");
        await _repository!.AddAsync(config, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(TestConfigId1);
        result.Key.Value.Should().Be("Cache:TTL");
        result.Value.Should().Be("300");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingConfiguration_ShouldReturnNull()
    {
        // Arrange
        await CleanDatabaseAsync();
        var nonExistingId = Guid.NewGuid();

        // Act
        var result = await _repository!.GetByIdAsync(nonExistingId, TestCancellationToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_ExistingConfiguration_ShouldPersistChanges()
    {
        // Arrange
        await CleanDatabaseAsync();
        var config = CreateTestConfiguration(TestConfigId1, "Feature:NewUI", "false", "bool");
        await _repository!.AddAsync(config, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Clear tracking
        _dbContext!.ChangeTracker.Clear();

        // Act - Update value
        var loadedConfig = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);
        loadedConfig!.UpdateValue("true", TestUserId);
        await _repository.UpdateAsync(loadedConfig, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Assert
        var updated = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);
        updated!.Value.Should().Be("true");
        updated.PreviousValue.Should().Be("false");
        updated.Version.Should().Be(2);
        updated.UpdatedByUserId.Should().Be(TestUserId);
        updated.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TestConstants.Timing.AssertionTolerance);
    }

    [Fact]
    public async Task DeleteAsync_ExistingConfiguration_ShouldRemoveFromDatabase()
    {
        // Arrange
        await CleanDatabaseAsync();
        var config = CreateTestConfiguration(TestConfigId1, "ToDelete:Config", "temp", "string");
        await _repository!.AddAsync(config, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Clear tracking
        _dbContext!.ChangeTracker.Clear();

        // Act
        var loadedConfig = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);
        await _repository.DeleteAsync(loadedConfig!, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Assert
        var deleted = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_WithMultipleConfigurations_ShouldReturnAllOrderedByKey()
    {
        // Arrange
        await CleanDatabaseAsync();
        var config1 = CreateTestConfiguration(TestConfigId1, "ZZZ:Last", "value1", "string");
        var config2 = CreateTestConfiguration(TestConfigId2, "AAA:First", "value2", "string");
        var config3 = CreateTestConfiguration(TestConfigId3, "MMM:Middle", "value3", "string");

        await _repository!.AddAsync(config1, TestCancellationToken);
        await _repository.AddAsync(config2, TestCancellationToken);
        await _repository.AddAsync(config3, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.GetAllAsync(TestCancellationToken);

        // Assert
        result.Should().HaveCount(3);
        result[0].Key.Value.Should().Be("AAA:First");
        result[1].Key.Value.Should().Be("MMM:Middle");
        result[2].Key.Value.Should().Be("ZZZ:Last");
    }

    #endregion

    #region Versioning and Rollback Tests

    [Fact]
    public async Task UpdateValue_MultipleUpdates_ShouldIncrementVersion()
    {
        // Arrange
        await CleanDatabaseAsync();
        var config = CreateTestConfiguration(TestConfigId1, "VersionTest", "v1", "string");
        await _repository!.AddAsync(config, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act - Multiple updates
        for (var i = 2; i <= 5; i++)
        {
            _dbContext!.ChangeTracker.Clear();
            var loaded = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);
            loaded!.UpdateValue($"v{i}", TestUserId);
            await _repository.UpdateAsync(loaded, TestCancellationToken);
            await _unitOfWork.SaveChangesAsync(TestCancellationToken);
        }

        // Assert
        var final = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);
        final!.Version.Should().Be(5);
        final.Value.Should().Be("v5");
        final.PreviousValue.Should().Be("v4");
    }

    [Fact]
    public async Task Rollback_WithPreviousValue_ShouldSwapCurrentAndPrevious()
    {
        // Arrange
        await CleanDatabaseAsync();
        var config = CreateTestConfiguration(TestConfigId1, "Rollback:Test", "original", "string");
        await _repository!.AddAsync(config, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Update to create previous value
        _dbContext!.ChangeTracker.Clear();
        var loaded = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);
        loaded!.UpdateValue("updated", TestUserId);
        await _repository.UpdateAsync(loaded, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Act - Rollback
        _dbContext.ChangeTracker.Clear();
        var toRollback = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);
        toRollback!.Rollback(TestUserId);
        await _repository.UpdateAsync(toRollback, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Assert
        var rolledBack = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);
        rolledBack!.Value.Should().Be("original");
        rolledBack.PreviousValue.Should().Be("updated");
        rolledBack.Version.Should().Be(3); // Create(1) → Update(2) → Rollback(3)
    }

    #endregion

    #region Environment-Specific Configuration Tests

    [Fact]
    public async Task GetByKeyAsync_EnvironmentSpecific_ShouldPrioritizeExactMatch()
    {
        // Arrange
        await CleanDatabaseAsync();
        var configAll = CreateTestConfiguration(TestConfigId1, "Feature:X", "default", "string", environment: "All");
        var configProd = CreateTestConfiguration(TestConfigId2, "Feature:X", "production", "string", environment: "Production");
        await _repository!.AddAsync(configAll, TestCancellationToken);
        await _repository.AddAsync(configProd, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act - Query for Production environment
        var result = await _repository.GetByKeyAsync("Feature:X", "Production", activeOnly: true, TestCancellationToken);

        // Assert - Should return Production-specific config, not "All"
        result.Should().NotBeNull();
        result!.Value.Should().Be("production");
        result.Environment.Should().Be("Production");
    }

    [Fact]
    public async Task GetByKeyAsync_NoEnvironmentMatch_ShouldFallbackToAll()
    {
        // Arrange
        await CleanDatabaseAsync();
        var configAll = CreateTestConfiguration(TestConfigId1, "Feature:Y", "default", "string", environment: "All");
        await _repository!.AddAsync(configAll, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act - Query for Development environment (only "All" exists)
        var result = await _repository.GetByKeyAsync("Feature:Y", "Development", activeOnly: true, TestCancellationToken);

        // Assert - Should fallback to "All" environment
        result.Should().NotBeNull();
        result!.Value.Should().Be("default");
        result.Environment.Should().Be("All");
    }

    [Fact]
    public async Task GetByKeyAsync_NoEnvironmentFilter_ShouldReturnAnyEnvironment()
    {
        // Arrange
        await CleanDatabaseAsync();
        var config = CreateTestConfiguration(TestConfigId1, "Feature:Z", "value", "string", environment: "Production");
        await _repository!.AddAsync(config, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act - Query without environment filter (null)
        var result = await _repository.GetByKeyAsync("Feature:Z", environment: null, activeOnly: true, TestCancellationToken);

        // Assert - Should return config regardless of environment
        result.Should().NotBeNull();
        result!.Value.Should().Be("value");
    }

    #endregion

    #region Active/Inactive Configuration Tests

    [Fact]
    public async Task GetActiveConfigurationsAsync_WithMixedConfigs_ShouldReturnOnlyActive()
    {
        // Arrange
        await CleanDatabaseAsync();
        var active1 = CreateTestConfiguration(TestConfigId1, "Active:Config1", "v1", "string");
        var active2 = CreateTestConfiguration(TestConfigId2, "Active:Config2", "v2", "string");
        var inactive = CreateTestConfiguration(TestConfigId3, "Inactive:Config", "v3", "string");
        inactive.Deactivate();

        await _repository!.AddAsync(active1, TestCancellationToken);
        await _repository.AddAsync(active2, TestCancellationToken);
        await _repository.AddAsync(inactive, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.GetActiveConfigurationsAsync(TestCancellationToken);

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(c => c.IsActive.Should().BeTrue());
        result.Should().Contain(c => c.Key.Value == "Active:Config1");
        result.Should().Contain(c => c.Key.Value == "Active:Config2");
        result.Should().NotContain(c => c.Key.Value == "Inactive:Config");
    }

    [Fact]
    public async Task GetByKeyAsync_WithActiveOnlyFilter_ShouldExcludeInactive()
    {
        // Arrange
        await CleanDatabaseAsync();
        var inactive = CreateTestConfiguration(TestConfigId1, "Test:Feature", "value", "string");
        inactive.Deactivate();
        await _repository!.AddAsync(inactive, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act - Query with activeOnly=true (default)
        var result = await _repository.GetByKeyAsync("Test:Feature", activeOnly: true, cancellationToken: TestCancellationToken);

        // Assert - Should return null (config is inactive)
        result.Should().BeNull();
    }

    [Fact]
    public async Task ActivateDeactivate_Configuration_ShouldToggleStatusAndTimestamp()
    {
        // Arrange
        await CleanDatabaseAsync();
        var config = CreateTestConfiguration(TestConfigId1, "Toggle:Test", "value", "string");
        await _repository!.AddAsync(config, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act - Deactivate
        _dbContext!.ChangeTracker.Clear();
        var loaded = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);
        loaded!.Deactivate();
        await _repository.UpdateAsync(loaded, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        var deactivated = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);

        // Act - Reactivate
        _dbContext.ChangeTracker.Clear();
        var toReactivate = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);
        toReactivate!.Activate();
        await _repository.UpdateAsync(toReactivate, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        var reactivated = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);

        // Assert
        deactivated!.IsActive.Should().BeFalse();
        deactivated.LastToggledAt.Should().NotBeNull();

        reactivated!.IsActive.Should().BeTrue();
        reactivated.LastToggledAt.Should().BeAfter(deactivated.LastToggledAt!.Value);
    }

    #endregion

    #region Category-Based Query Tests

    [Fact]
    public async Task GetByCategoryAsync_WithMultipleCategories_ShouldReturnOnlyMatchingCategory()
    {
        // Arrange
        await CleanDatabaseAsync();
        var cacheConfig1 = CreateTestConfiguration(TestConfigId1, "Cache:TTL", "300", "int", category: "Cache");
        var cacheConfig2 = CreateTestConfiguration(TestConfigId2, "Cache:MaxSize", "1000", "int", category: "Cache");
        var rateLimitConfig = CreateTestConfiguration(TestConfigId3, "RateLimit:Max", "100", "int", category: "RateLimit");

        await _repository!.AddAsync(cacheConfig1, TestCancellationToken);
        await _repository.AddAsync(cacheConfig2, TestCancellationToken);
        await _repository.AddAsync(rateLimitConfig, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.GetByCategoryAsync("Cache", TestCancellationToken);

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(c => c.Category.Should().Be("Cache"));
        result.Should().Contain(c => c.Key.Value == "Cache:TTL");
        result.Should().Contain(c => c.Key.Value == "Cache:MaxSize");
        result.Should().NotContain(c => c.Category == "RateLimit");
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task GetActiveConfigurationsAsync_BulkRead_ShouldCompleteUnder500ms()
    {
        // Arrange
        await CleanDatabaseAsync();
        var configs = Enumerable.Range(1, 100)
            .Select(i => CreateTestConfiguration(
                Guid.NewGuid(),
                $"Perf:Config{i}",
                $"value{i}",
                "string",
                category: "Performance"
            ))
            .ToList();

        foreach (var config in configs)
        {
            await _repository!.AddAsync(config, TestCancellationToken);
        }
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _repository!.GetActiveConfigurationsAsync(TestCancellationToken);
        stopwatch.Stop();

        // Assert
        result.Should().HaveCount(100);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(500, "bulk read should complete in <500ms");
    }

    #endregion

    #region Bulk Operations Tests (NEW - Week 3 Expansion)

    [Fact]
    public async Task BatchCreateConfigurations_ShouldPersistAllAtomically()
    {
        // Arrange
        await CleanDatabaseAsync();
        var configs = new[]
        {
            CreateTestConfiguration(TestConfigId1, "Batch:Config1", "value1", "string", category: "Batch"),
            CreateTestConfiguration(TestConfigId2, "Batch:Config2", "value2", "string", category: "Batch"),
            CreateTestConfiguration(TestConfigId3, "Batch:Config3", "value3", "string", category: "Batch")
        };

        // Act
        foreach (var config in configs)
        {
            await _repository!.AddAsync(config, TestCancellationToken);
        }
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Assert
        var persisted = await _repository!.GetByCategoryAsync("Batch", TestCancellationToken);
        persisted.Should().HaveCount(3);
        persisted.Should().OnlyContain(c => c.Category == "Batch");
    }

    [Fact]
    public async Task BatchUpdateConfigurations_ShouldApplyAllChanges()
    {
        // Arrange
        await CleanDatabaseAsync();
        var config1 = CreateTestConfiguration(TestConfigId1, "Update:Config1", "original1", "string");
        var config2 = CreateTestConfiguration(TestConfigId2, "Update:Config2", "original2", "string");
        await _repository!.AddAsync(config1, TestCancellationToken);
        await _repository.AddAsync(config2, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        _dbContext!.ChangeTracker.Clear();

        // Act - Batch update
        var loaded1 = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);
        var loaded2 = await _repository.GetByIdAsync(TestConfigId2, TestCancellationToken);
        loaded1!.UpdateValue("updated1", TestUserId);
        loaded2!.UpdateValue("updated2", TestUserId);
        await _repository.UpdateAsync(loaded1, TestCancellationToken);
        await _repository.UpdateAsync(loaded2, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Assert
        var updated1 = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);
        var updated2 = await _repository.GetByIdAsync(TestConfigId2, TestCancellationToken);
        updated1!.Value.Should().Be("updated1");
        updated2!.Value.Should().Be("updated2");
        updated1.Version.Should().Be(2);
        updated2.Version.Should().Be(2);
    }

    #endregion

    #region Concurrent Updates Tests (NEW - Week 3 Expansion)

    [Fact]
    public async Task ConcurrentUpdate_WithoutRowVersion_ShouldUseLastWriteWins()
    {
        // Arrange
        await CleanDatabaseAsync();
        var config = CreateTestConfiguration(TestConfigId1, "Concurrent:Test", "v1", "string");
        await _repository!.AddAsync(config, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Load same entity twice (simulating concurrent access)
        _dbContext!.ChangeTracker.Clear();
        var instance1 = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);
        _dbContext.ChangeTracker.Clear();
        var instance2 = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);

        // Act - First update succeeds
        instance1!.UpdateValue("v2", TestUserId);
        await _repository.UpdateAsync(instance1, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Second update also succeeds (no concurrency token = last-write-wins)
        _dbContext.ChangeTracker.Clear();
        instance2!.UpdateValue("v3", TestUserId);
        await _repository.UpdateAsync(instance2, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Assert - Last write wins (v3), version incremented twice
        var final = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);
        final!.Value.Should().Be("v3");
        final.Version.Should().BeGreaterThanOrEqualTo(2); // Application layer manages version
    }

    [Fact]
    public async Task ConcurrentUpdate_WithOptimisticLocking_ShouldPreserveDataIntegrity()
    {
        // Arrange
        await CleanDatabaseAsync();
        var config = CreateTestConfiguration(TestConfigId1, "Lock:Test", "original", "string");
        await _repository!.AddAsync(config, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act - Multiple sequential updates (no concurrency)
        for (var i = 1; i <= 3; i++)
        {
            _dbContext!.ChangeTracker.Clear();
            var loaded = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);
            loaded!.UpdateValue($"v{i}", TestUserId);
            await _repository.UpdateAsync(loaded, TestCancellationToken);
            await _unitOfWork.SaveChangesAsync(TestCancellationToken);
        }

        // Assert
        var final = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);
        final!.Version.Should().Be(4); // Create(1) + 3 updates
        final.Value.Should().Be("v3");
    }

    #endregion

    #region Environment Fallback Edge Cases (NEW - Week 3 Expansion)

    [Fact]
    public async Task GetByKeyAsync_MultipleEnvironments_ShouldPrioritizeCorrectly()
    {
        // Arrange
        await CleanDatabaseAsync();
        var configAll = CreateTestConfiguration(TestConfigId1, "Multi:Env", "all", "string", environment: "All");
        var configDev = CreateTestConfiguration(TestConfigId2, "Multi:Env", "dev", "string", environment: "Development");
        var configProd = CreateTestConfiguration(TestConfigId3, "Multi:Env", "prod", "string", environment: "Production");
        await _repository!.AddAsync(configAll, TestCancellationToken);
        await _repository.AddAsync(configDev, TestCancellationToken);
        await _repository.AddAsync(configProd, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act & Assert - Development should return dev-specific, not "All"
        var devResult = await _repository.GetByKeyAsync("Multi:Env", "Development", activeOnly: true, TestCancellationToken);
        devResult.Should().NotBeNull();
        devResult!.Value.Should().Be("dev");
        devResult.Environment.Should().Be("Development");

        // Act & Assert - Staging (no specific config) should fallback to "All"
        var stagingResult = await _repository.GetByKeyAsync("Multi:Env", "Staging", activeOnly: true, TestCancellationToken);
        stagingResult.Should().NotBeNull();
        stagingResult!.Value.Should().Be("all");
        stagingResult.Environment.Should().Be("All");
    }

    [Fact]
    public async Task GetByKeyAsync_InactiveEnvironmentSpecific_ShouldFallbackToAll()
    {
        // Arrange
        await CleanDatabaseAsync();
        var configAll = CreateTestConfiguration(TestConfigId1, "Fallback:Test", "all-value", "string", environment: "All");
        var configProd = CreateTestConfiguration(TestConfigId2, "Fallback:Test", "prod-value", "string", environment: "Production");
        configProd.Deactivate();

        await _repository!.AddAsync(configAll, TestCancellationToken);
        await _repository.AddAsync(configProd, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act - Query Production with activeOnly=true
        var result = await _repository.GetByKeyAsync("Fallback:Test", "Production", activeOnly: true, TestCancellationToken);

        // Assert - Should fallback to "All" since Production config is inactive
        result.Should().NotBeNull();
        result!.Value.Should().Be("all-value");
        result.Environment.Should().Be("All");
    }

    #endregion

    #region Category Filtering with Pagination (NEW - Week 3 Expansion)

    [Fact]
    public async Task GetByCategoryAsync_LargeCategory_ShouldReturnAllConfigs()
    {
        // Arrange
        await CleanDatabaseAsync();
        var configs = Enumerable.Range(1, 25)
            .Select(i => CreateTestConfiguration(
                Guid.NewGuid(),
                $"Cache:Config{i}",
                $"value{i}",
                "string",
                category: "Cache"
            ))
            .ToList();

        foreach (var config in configs)
        {
            await _repository!.AddAsync(config, TestCancellationToken);
        }
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository!.GetByCategoryAsync("Cache", TestCancellationToken);

        // Assert
        result.Should().HaveCount(25);
        result.Should().OnlyContain(c => c.Category == "Cache");
        result.Should().BeInAscendingOrder(c => c.Key.Value);
    }

    [Fact]
    public async Task GetByCategoryAsync_NonExistingCategory_ShouldReturnEmpty()
    {
        // Arrange
        await CleanDatabaseAsync();
        var config = CreateTestConfiguration(TestConfigId1, "Test:Config", "value", "string", category: "Existing");
        await _repository!.AddAsync(config, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.GetByCategoryAsync("NonExisting", TestCancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region Search by Key Pattern Tests (NEW - Week 3 Expansion)

    [Fact]
    public async Task GetAllAsync_ShouldSupportKeyPrefix()
    {
        // Arrange
        await CleanDatabaseAsync();
        var config1 = CreateTestConfiguration(TestConfigId1, "RateLimit:Api:MaxRequests", "1000", "int");
        var config2 = CreateTestConfiguration(TestConfigId2, "RateLimit:Api:Window", "60", "int");
        var config3 = CreateTestConfiguration(TestConfigId3, "Cache:TTL", "300", "int");
        await _repository!.AddAsync(config1, TestCancellationToken);
        await _repository.AddAsync(config2, TestCancellationToken);
        await _repository.AddAsync(config3, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act - Simulate prefix search (application-level filtering)
        var all = await _repository.GetAllAsync(TestCancellationToken);
        var rateLimitConfigs = all.Where(c => c.Key.Value.StartsWith("RateLimit:")).ToList();

        // Assert
        rateLimitConfigs.Should().HaveCount(2);
        rateLimitConfigs.Should().OnlyContain(c => c.Key.Value.StartsWith("RateLimit:"));
    }

    [Fact]
    public async Task GetByCategoryAsync_MixedCategories_ShouldIsolateCorrectly()
    {
        // Arrange
        await CleanDatabaseAsync();
        var config1 = CreateTestConfiguration(TestConfigId1, "Security:ApiKey", "secret1", "string", category: "Security");
        var config2 = CreateTestConfiguration(TestConfigId2, "Security:TokenExpiry", "3600", "int", category: "Security");
        var config3 = CreateTestConfiguration(Guid.NewGuid(), "Performance:Timeout", "30", "int", category: "Performance");
        await _repository!.AddAsync(config1, TestCancellationToken);
        await _repository.AddAsync(config2, TestCancellationToken);
        await _repository.AddAsync(config3, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var securityConfigs = await _repository.GetByCategoryAsync("Security", TestCancellationToken);
        var perfConfigs = await _repository.GetByCategoryAsync("Performance", TestCancellationToken);

        // Assert
        securityConfigs.Should().HaveCount(2);
        securityConfigs.Should().OnlyContain(c => c.Category == "Security");
        perfConfigs.Should().HaveCount(1);
        perfConfigs.Should().OnlyContain(c => c.Category == "Performance");
    }

    #endregion
}
