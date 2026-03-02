using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using SystemConfigurationAggregate = Api.BoundedContexts.SystemConfiguration.Domain.Entities.SystemConfiguration;
using Api.BoundedContexts.SystemConfiguration.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Infrastructure;

/// <summary>
/// Integration tests for SystemConfiguration feature flag validation and cache invalidation.
/// Week 9: SystemConfiguration infrastructure layer (15 tests)
/// Tests: Feature flag validation, toggle operations, cache invalidation, runtime config updates
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "SystemConfiguration")]
[Trait("Week", "9")]
public sealed class FeatureFlagCacheIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private ConfigurationRepository? _repository;

    private static readonly Guid TestUserId = new("90000000-0000-0000-0000-000000000001");
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public FeatureFlagCacheIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"meepleai_week9_sysconfig_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(connectionString, o => o.UseVector()) // Issue #3547
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        var mockMediator = new Moq.Mock<MediatR.IMediator>();
        var mockEventCollector = new Moq.Mock<Api.SharedKernel.Application.Services.IDomainEventCollector>();
        mockEventCollector.Setup(x => x.GetAndClearEvents())
            .Returns(new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>().AsReadOnly());

        // Fix: Use PostgreSQL DbContext with Testcontainers, not in-memory
        _dbContext = new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);
        await _dbContext.Database.MigrateAsync(TestCancellationToken);

        // Seed required User for FK constraints
        await SeedTestUserAsync();

        _repository = new ConfigurationRepository(_dbContext, mockEventCollector.Object);
    }

    /// <summary>
    /// Seeds a test user to satisfy CreatedByUserId FK constraint.
    /// </summary>
    private async Task SeedTestUserAsync()
    {
        var user = new UserEntity
        {
            Id = TestUserId,
            Email = "test-week9-sysconfig@meepleai.dev",
            DisplayName = "Test User Week 9",
            Role = "admin",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext!.Set<UserEntity>().Add(user);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }

        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    [Fact]
    public async Task FeatureFlag_Toggle_ShouldUpdateIsActiveFlag()
    {
        // Arrange
        var userId = TestUserId;
        var key = new ConfigKey("Features.ExperimentalUI");
        var config = new SystemConfigurationAggregate(
            Guid.NewGuid(),
            key,
            "true",
            "bool",
            userId,
            "Experimental UI feature",
            "FeatureFlags"
        );

        await _repository!.AddAsync(config, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();

        // Act - Deactivate
        var tracked = await _repository.GetByIdAsync(config.Id, TestCancellationToken);
        tracked!.Deactivate();
        await _repository.UpdateAsync(tracked, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var toggled = await _repository.GetByIdAsync(config.Id, TestCancellationToken);
        toggled.Should().NotBeNull();
        toggled!.IsActive.Should().BeFalse();
        toggled.LastToggledAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task FeatureFlag_Toggle_Multiple_ShouldTrackLastToggledAt()
    {
        // Arrange
        var userId = TestUserId;
        var config = new SystemConfigurationAggregate(
            Guid.NewGuid(),
            new ConfigKey("Features.BetaMode"),
            "enabled",
            "string",
            userId,
            category: "FeatureFlags"
        );

        await _repository!.AddAsync(config, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();

        // Act - Toggle OFF
        var tracked = await _repository.GetByIdAsync(config.Id, TestCancellationToken);
        tracked!.Deactivate();
        await _repository.UpdateAsync(tracked, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
        var firstToggle = tracked.LastToggledAt;
        _dbContext.ChangeTracker.Clear();

        await Task.Delay(100);

        // Act - Toggle ON
        tracked = await _repository.GetByIdAsync(config.Id, TestCancellationToken);
        tracked!.Activate();
        await _repository.UpdateAsync(tracked, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var final = await _repository.GetByIdAsync(config.Id, TestCancellationToken);
        final.Should().NotBeNull();
        final!.IsActive.Should().BeTrue();
        final.LastToggledAt.Should().BeAfter(firstToggle!.Value);
    }

    [Fact]
    public async Task RuntimeConfig_Update_ShouldIncrementVersion()
    {
        // Arrange
        var userId = TestUserId;
        var updaterId = TestUserId;
        var config = new SystemConfigurationAggregate(
            Guid.NewGuid(),
            new ConfigKey("Runtime.MaxThreads"),
            "10",
            "int",
            userId,
            "Max concurrent threads"
        );

        await _repository!.AddAsync(config, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();

        // Act - Update runtime value
        var tracked = await _repository.GetByIdAsync(config.Id, TestCancellationToken);
        tracked!.UpdateValue("20", updaterId);
        await _repository.UpdateAsync(tracked, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var updated = await _repository.GetByIdAsync(config.Id, TestCancellationToken);
        updated.Should().NotBeNull();
        updated!.Value.Should().Be("20");
        updated.Version.Should().Be(2);
        updated.PreviousValue.Should().Be("10");
    }

    [Fact]
    public async Task EnvironmentSpecific_Config_ShouldPrioritizeEnvironmentMatch()
    {
        // Arrange
        var userId = TestUserId;

        // Global config
        var globalConfig = new SystemConfigurationAggregate(
            Guid.NewGuid(),
            new ConfigKey("Database.ConnectionTimeout"),
            "30",
            "int",
            userId,
            environment: "All"
        );

        // Production-specific config
        var prodConfig = new SystemConfigurationAggregate(
            Guid.NewGuid(),
            new ConfigKey("Database.ConnectionTimeout"),
            "60",
            "int",
            userId,
            environment: "Production"
        );

        await _repository!.AddAsync(globalConfig, TestCancellationToken);
        await _repository.AddAsync(prodConfig, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.GetByKeyAsync("Database.ConnectionTimeout", "Production", true, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Should().Be("60", "production-specific config should take priority");
        result.Environment.Should().Be("Production");
    }

    [Fact]
    public async Task ConfigValidation_ShouldEnforceRequiresRestart()
    {
        // Arrange
        var userId = TestUserId;
        var config = new SystemConfigurationAggregate(
            Guid.NewGuid(),
            new ConfigKey("Database.Provider"),
            "PostgreSQL",
            "string",
            userId,
            requiresRestart: true
        );

        await _repository!.AddAsync(config, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Act
        var retrieved = await _repository.GetByIdAsync(config.Id, TestCancellationToken);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.RequiresRestart.Should().BeTrue();
    }

    [Fact]
    public async Task FeatureFlag_GetActiveOnly_ShouldFilterInactive()
    {
        // Arrange
        var userId = TestUserId;

        var activeFlag = new SystemConfigurationAggregate(
            Guid.NewGuid(),
            new ConfigKey("Features.Active"),
            "true",
            "bool",
            userId,
            category: "FeatureFlags"
        );

        var inactiveFlag = new SystemConfigurationAggregate(
            Guid.NewGuid(),
            new ConfigKey("Features.Inactive"),
            "false",
            "bool",
            userId,
            category: "FeatureFlags"
        );
        inactiveFlag.Deactivate();

        await _repository!.AddAsync(activeFlag, TestCancellationToken);
        await _repository.AddAsync(inactiveFlag, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Act
        var activeConfigs = await _repository.GetActiveConfigurationsAsync(TestCancellationToken);

        // Assert
        activeConfigs.Should().HaveCount(1);
        activeConfigs.First().Key.Value.Should().Be("Features.Active");
    }

    [Fact]
    public async Task Category_Filter_ShouldReturnOnlyMatchingConfigs()
    {
        // Arrange
        var userId = TestUserId;

        var performanceConfig1 = new SystemConfigurationAggregate(
            Guid.NewGuid(),
            new ConfigKey("Performance.CacheSize"),
            "100",
            "int",
            userId,
            category: "Performance"
        );

        var performanceConfig2 = new SystemConfigurationAggregate(
            Guid.NewGuid(),
            new ConfigKey("Performance.Timeout"),
            "5000",
            "int",
            userId,
            category: "Performance"
        );

        var securityConfig = new SystemConfigurationAggregate(
            Guid.NewGuid(),
            new ConfigKey("Security.MaxAttempts"),
            "3",
            "int",
            userId,
            category: "Security"
        );

        await _repository!.AddAsync(performanceConfig1, TestCancellationToken);
        await _repository.AddAsync(performanceConfig2, TestCancellationToken);
        await _repository.AddAsync(securityConfig, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Act
        var performanceConfigs = await _repository.GetByCategoryAsync("Performance", TestCancellationToken);

        // Assert
        performanceConfigs.Should().HaveCount(2);
        performanceConfigs.Should().OnlyContain(c => c.Category == "Performance");
    }

    [Fact]
    public async Task Config_Rollback_ShouldSwapCurrentAndPrevious()
    {
        // Arrange
        var userId = TestUserId;
        var updaterId = TestUserId;
        var rollbackerId = TestUserId;

        var config = new SystemConfigurationAggregate(
            Guid.NewGuid(),
            new ConfigKey("Feature.Setting"),
            "original",
            "string",
            userId
        );

        await _repository!.AddAsync(config, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();

        // Update
        var tracked = await _repository.GetByIdAsync(config.Id, TestCancellationToken);
        tracked!.UpdateValue("modified", updaterId);
        await _repository.UpdateAsync(tracked, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();

        // Act - Rollback
        tracked = await _repository.GetByIdAsync(config.Id, TestCancellationToken);
        tracked!.Rollback(rollbackerId);
        await _repository.UpdateAsync(tracked, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var rolledBack = await _repository.GetByIdAsync(config.Id, TestCancellationToken);
        rolledBack.Should().NotBeNull();
        rolledBack!.Value.Should().Be("original");
        rolledBack.PreviousValue.Should().Be("modified");
    }

    [Fact]
    public async Task Config_DynamicUpdate_ShouldReflectImmediately()
    {
        // Arrange
        var userId = TestUserId;
        var config = new SystemConfigurationAggregate(
            Guid.NewGuid(),
            new ConfigKey("Dynamic.LogLevel"),
            "Info",
            "string",
            userId
        );

        await _repository!.AddAsync(config, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();

        // Act - Dynamic update
        var tracked = await _repository.GetByIdAsync(config.Id, TestCancellationToken);
        tracked!.UpdateValue("Debug", userId);
        await _repository.UpdateAsync(tracked, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert - Read immediately
        var updated = await _repository.GetByKeyAsync("Dynamic.LogLevel", activeOnly: true, cancellationToken: TestCancellationToken);
        updated.Should().NotBeNull();
        updated!.Value.Should().Be("Debug");
    }

    [Fact]
    public async Task Config_Delete_ShouldRemoveFromDatabase()
    {
        // Arrange
        var userId = TestUserId;
        var config = new SystemConfigurationAggregate(
            Guid.NewGuid(),
            new ConfigKey("Deprecated.Setting"),
            "old",
            "string",
            userId
        );

        await _repository!.AddAsync(config, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();

        // Act
        var tracked = await _repository.GetByIdAsync(config.Id, TestCancellationToken);
        tracked!.MarkAsDeleted();
        await _repository.DeleteAsync(tracked, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var deleted = await _repository.GetByIdAsync(config.Id, TestCancellationToken);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task FeatureFlag_DoubleActivate_ShouldBeIdempotent()
    {
        // Arrange
        var userId = TestUserId;
        var config = new SystemConfigurationAggregate(
            Guid.NewGuid(),
            new ConfigKey("Features.AlreadyActive"),
            "true",
            "bool",
            userId
        );

        await _repository!.AddAsync(config, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();

        // Act - Activate when already active
        var tracked = await _repository.GetByIdAsync(config.Id, TestCancellationToken);
        tracked!.Activate();
        await _repository.UpdateAsync(tracked, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var firstToggle = tracked.LastToggledAt;
        _dbContext.ChangeTracker.Clear();

        tracked = await _repository.GetByIdAsync(config.Id, TestCancellationToken);
        tracked!.Activate();
        await _repository.UpdateAsync(tracked, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var final = await _repository.GetByIdAsync(config.Id, TestCancellationToken);
        final.Should().NotBeNull();
        final!.IsActive.Should().BeTrue();
        final.LastToggledAt.Should().Be(firstToggle, "double activate should be idempotent");
    }

    [Fact]
    public async Task Config_UpdatedBy_ShouldTrackUserChanges()
    {
        // Arrange
        var creatorId = TestUserId;
        var updaterId = TestUserId;

        var config = new SystemConfigurationAggregate(
            Guid.NewGuid(),
            new ConfigKey("Tracked.Config"),
            "initial",
            "string",
            creatorId
        );

        await _repository!.AddAsync(config, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();

        // Act
        var tracked = await _repository.GetByIdAsync(config.Id, TestCancellationToken);
        tracked!.UpdateValue("updated", updaterId);
        await _repository.UpdateAsync(tracked, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var result = await _repository.GetByIdAsync(config.Id, TestCancellationToken);
        result.Should().NotBeNull();
        result!.CreatedByUserId.Should().Be(creatorId);
        result.UpdatedByUserId.Should().Be(updaterId);
    }

    [Fact]
    public async Task EnvironmentFallback_ShouldReturnAllEnvironment_WhenNoSpecificMatch()
    {
        // Arrange
        var userId = TestUserId;

        var globalConfig = new SystemConfigurationAggregate(
            Guid.NewGuid(),
            new ConfigKey("Fallback.Setting"),
            "global_value",
            "string",
            userId,
            environment: "All"
        );

        await _repository!.AddAsync(globalConfig, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.GetByKeyAsync("Fallback.Setting", "Development", true, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Environment.Should().Be("All");
        result.Value.Should().Be("global_value");
    }

    [Fact]
    public async Task ConfigKey_Validation_ShouldEnforceNamingConvention()
    {
        // Arrange
        var validKey = "Category.SubCategory.SettingName";

        // Act
        var configKey = new ConfigKey(validKey);

        // Assert
        configKey.Value.Should().Be(validKey);
    }

    [Fact]
    public async Task Config_ExistsAsync_ShouldReturnCorrectStatus()
    {
        // Arrange
        var userId = TestUserId;
        var existingId = Guid.NewGuid();
        var nonExistingId = Guid.NewGuid();

        var config = new SystemConfigurationAggregate(
            existingId,
            new ConfigKey("Existing.Config"),
            "value",
            "string",
            userId
        );

        await _repository!.AddAsync(config, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Act
        var exists = await _repository.ExistsAsync(existingId, TestCancellationToken);
        var notExists = await _repository.ExistsAsync(nonExistingId, TestCancellationToken);

        // Assert
        exists.Should().BeTrue();
        notExists.Should().BeFalse();
    }
}
