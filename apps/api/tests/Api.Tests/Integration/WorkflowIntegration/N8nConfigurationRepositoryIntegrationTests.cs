using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.WorkflowIntegration.Domain.Entities;
using Api.BoundedContexts.WorkflowIntegration.Domain.Repositories;
using Api.BoundedContexts.WorkflowIntegration.Domain.ValueObjects;
using Api.BoundedContexts.WorkflowIntegration.Infrastructure.Persistence;
using Api.Infrastructure;
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
using Xunit;

namespace Api.Tests.Integration.WorkflowIntegration;

/// <summary>
/// Integration tests for N8nConfigurationRepository using SharedTestcontainersFixture.
/// Tests PostgreSQL persistence, CRUD operations, and domain constraints.
/// Issue #2307: Week 3 - WorkflowIntegration repository integration testing
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "WorkflowIntegration")]
[Trait("Issue", "2307")]
public sealed class N8nConfigurationRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IN8NConfigurationRepository? _repository;
    private IUnitOfWork? _unitOfWork;
    private IServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    // Test data constants
    private static readonly Guid TestConfigId1 = new("30000000-0000-0000-0000-000000000001");
    private static readonly Guid TestConfigId2 = new("30000000-0000-0000-0000-000000000002");
    private static readonly Guid TestConfigId3 = new("30000000-0000-0000-0000-000000000003");
    private static readonly Guid TestUserId = new("10000000-0000-0000-0000-000000000001");

    public N8nConfigurationRepositoryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        // Create isolated database for this test class
        _databaseName = $"test_n8nconfig_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(_isolatedDbConnectionString, o => o.UseVector()); // Issue #3547: Enable pgvector
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        services.AddScoped<IN8NConfigurationRepository, N8NConfigurationRepository>();
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
        _repository = _serviceProvider.GetRequiredService<IN8NConfigurationRepository>();
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

        // Seed test user (required by FK constraint)
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

    private async Task SeedTestUserAsync()
    {
        var user = new User(
            id: TestUserId,
            email: new Email("testuser@example.com"),
            displayName: "Test User",
            passwordHash: PasswordHash.Create("TestPassword123!"),
            role: Role.Admin
        );

        _dbContext!.Set<Api.Infrastructure.Entities.UserEntity>().Add(
            new Api.Infrastructure.Entities.UserEntity
            {
                Id = user.Id,
                Email = user.Email.Value,
                DisplayName = user.DisplayName,
                PasswordHash = user.PasswordHash.Value,
                Role = user.Role.Value,
                Tier = user.Tier.Value,
                CreatedAt = user.CreatedAt,
                Language = user.Language,
                EmailNotifications = user.EmailNotifications,
                Theme = user.Theme,
                DataRetentionDays = user.DataRetentionDays,
                IsTwoFactorEnabled = user.IsTwoFactorEnabled,
                TwoFactorEnabledAt = user.TwoFactorEnabledAt,
                IsDemoAccount = user.IsDemoAccount
            });

        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    private static N8NConfiguration CreateTestConfiguration(
        Guid id,
        string name = "Production N8n",
        string baseUrl = "https://n8n.example.com",
        string? webhookUrl = null,
        string? apiKey = null)
    {
        return new N8NConfiguration(
            id: id,
            name: name,
            baseUrl: new WorkflowUrl(baseUrl),
            apiKeyEncrypted: apiKey ?? "encrypted_api_key_12345",
            createdByUserId: TestUserId,
            webhookUrl: webhookUrl != null ? new WorkflowUrl(webhookUrl) : null
        );
    }

    private async Task CleanDatabaseAsync()
    {
        if (_dbContext == null) return;

        _dbContext.Set<Api.Infrastructure.Entities.N8NConfigEntity>().RemoveRange(
            _dbContext.Set<Api.Infrastructure.Entities.N8NConfigEntity>());
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_ExistingConfiguration_ShouldReturnConfiguration()
    {
        // Arrange
        await CleanDatabaseAsync();
        var config = CreateTestConfiguration(
            TestConfigId1,
            "Dev N8n",
            "https://dev.n8n.example.com",
            "https://webhook.example.com/dev");
        await _repository!.AddAsync(config, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(TestConfigId1);
        result.Name.Should().Be("Dev N8n");
        result.BaseUrl.Value.Should().Be("https://dev.n8n.example.com");
        result.WebhookUrl.Should().NotBeNull();
        result.WebhookUrl!.Value.Should().Be("https://webhook.example.com/dev");
        result.IsActive.Should().BeTrue();
        result.CreatedByUserId.Should().Be(TestUserId);
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

    #endregion

    #region GetActiveConfigurationAsync Tests

    [Fact]
    public async Task GetActiveConfigurationAsync_WithActiveConfiguration_ShouldReturnActive()
    {
        // Arrange
        await CleanDatabaseAsync();
        var activeConfig = CreateTestConfiguration(TestConfigId1, "Active N8n");
        var inactiveConfig = CreateTestConfiguration(TestConfigId2, "Inactive N8n");
        inactiveConfig.Deactivate();

        await _repository!.AddAsync(activeConfig, TestCancellationToken);
        await _repository.AddAsync(inactiveConfig, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.GetActiveConfigurationAsync(TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(TestConfigId1);
        result.Name.Should().Be("Active N8n");
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetActiveConfigurationAsync_NoActiveConfiguration_ShouldReturnNull()
    {
        // Arrange
        await CleanDatabaseAsync();
        var inactiveConfig = CreateTestConfiguration(TestConfigId1, "Inactive N8n");
        inactiveConfig.Deactivate();

        await _repository!.AddAsync(inactiveConfig, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.GetActiveConfigurationAsync(TestCancellationToken);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region FindByNameAsync Tests

    [Fact]
    public async Task FindByNameAsync_ExistingName_ShouldReturnConfiguration()
    {
        // Arrange
        await CleanDatabaseAsync();
        var config = CreateTestConfiguration(TestConfigId1, "Staging N8n");
        await _repository!.AddAsync(config, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.FindByNameAsync("Staging N8n", TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Staging N8n");
        result.Id.Should().Be(TestConfigId1);
    }

    [Fact]
    public async Task FindByNameAsync_NonExistingName_ShouldReturnNull()
    {
        // Arrange
        await CleanDatabaseAsync();

        // Act
        var result = await _repository!.FindByNameAsync("NonExistingName", TestCancellationToken);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region AddAsync + SaveChanges Tests

    [Fact]
    public async Task AddAsync_NewConfiguration_ShouldPersistToDatabase()
    {
        // Arrange
        await CleanDatabaseAsync();
        var config = CreateTestConfiguration(
            TestConfigId1,
            "Production N8n",
            "https://prod.n8n.example.com",
            "https://webhook.example.com/prod",
            "encrypted_api_key_production_12345"
        );

        // Act
        await _repository!.AddAsync(config, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Assert
        var persisted = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);
        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be("Production N8n");
        persisted.ApiKeyEncrypted.Should().Be("encrypted_api_key_production_12345");
        persisted.IsActive.Should().BeTrue();
        persisted.LastTestedAt.Should().BeNull();
        persisted.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TestConstants.Timing.AssertionTolerance);
    }

    #endregion

    #region UpdateAsync + SaveChanges Tests

    [Fact]
    public async Task UpdateAsync_UpdateConfiguration_ShouldPersistChanges()
    {
        // Arrange
        await CleanDatabaseAsync();
        var config = CreateTestConfiguration(TestConfigId1, "Original Name");
        await _repository!.AddAsync(config, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Clear tracking to avoid conflicts
        _dbContext!.ChangeTracker.Clear();

        // Act - Update configuration
        var loadedConfig = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);
        loadedConfig!.UpdateConfiguration(
            name: "Updated Name",
            baseUrl: new WorkflowUrl("https://updated.example.com")
        );
        await _repository.UpdateAsync(loadedConfig, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Assert
        var updated = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);
        updated!.Name.Should().Be("Updated Name");
        updated.BaseUrl.Value.Should().Be("https://updated.example.com");
        updated.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TestConstants.Timing.AssertionTolerance);
    }

    [Fact]
    public async Task UpdateAsync_DeactivateConfiguration_ShouldMarkAsInactive()
    {
        // Arrange
        await CleanDatabaseAsync();
        var config = CreateTestConfiguration(TestConfigId1, "Active Config");
        await _repository!.AddAsync(config, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Clear tracking to avoid conflicts
        _dbContext!.ChangeTracker.Clear();

        // Act - Deactivate
        var loadedConfig = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);
        loadedConfig!.Deactivate();
        await _repository.UpdateAsync(loadedConfig, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Assert
        var updated = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);
        updated!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_RecordTestResult_ShouldUpdateLastTested()
    {
        // Arrange
        await CleanDatabaseAsync();
        var config = CreateTestConfiguration(TestConfigId1, "Test Config");
        await _repository!.AddAsync(config, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Clear tracking to avoid conflicts
        _dbContext!.ChangeTracker.Clear();

        // Act - Record test result
        var loadedConfig = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);
        loadedConfig!.RecordTestResult(success: true, result: "Connection successful");
        await _repository.UpdateAsync(loadedConfig, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Assert
        var updated = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);
        updated!.LastTestedAt.Should().NotBeNull();
        updated.LastTestedAt.Should().BeCloseTo(DateTime.UtcNow, TestConstants.Timing.AssertionTolerance);
        updated.LastTestResult.Should().Be("Connection successful");
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_ExistingConfiguration_ShouldRemoveFromDatabase()
    {
        // Arrange
        await CleanDatabaseAsync();
        var config = CreateTestConfiguration(TestConfigId1, "To Delete");
        await _repository!.AddAsync(config, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Clear tracking to avoid identity conflict
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
    public async Task DeleteAsync_NonExistingConfiguration_ShouldThrowDbUpdateConcurrencyException()
    {
        // Arrange
        await CleanDatabaseAsync();
        var config = CreateTestConfiguration(Guid.NewGuid(), "NonExisting");

        // Act
        var act = async () =>
        {
            await _repository!.DeleteAsync(config, TestCancellationToken);
            await _unitOfWork!.SaveChangesAsync(TestCancellationToken);
        };

        // Assert - EF Core throws concurrency exception when deleting non-tracked entities
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    #endregion

    #region ExistsAsync Tests

    [Fact]
    public async Task ExistsAsync_ExistingConfiguration_ShouldReturnTrue()
    {
        // Arrange
        await CleanDatabaseAsync();
        var config = CreateTestConfiguration(TestConfigId1, "Exists Test");
        await _repository!.AddAsync(config, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var exists = await _repository.ExistsAsync(TestConfigId1, TestCancellationToken);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_NonExistingConfiguration_ShouldReturnFalse()
    {
        // Arrange
        await CleanDatabaseAsync();
        var nonExistingId = Guid.NewGuid();

        // Act
        var exists = await _repository!.ExistsAsync(nonExistingId, TestCancellationToken);

        // Assert
        exists.Should().BeFalse();
    }

    #endregion

    #region Domain Constraint Tests

    [Fact]
    public async Task AddAsync_WithEncryptedApiKey_ShouldPersistSecurely()
    {
        // Arrange
        await CleanDatabaseAsync();
        const string encryptedKey = "AES256_ENCRYPTED_KEY_WITH_SPECIAL_CHARS_!@#$%";
        var config = CreateTestConfiguration(
            TestConfigId1,
            "Secure Config",
            apiKey: encryptedKey
        );

        // Act
        await _repository!.AddAsync(config, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Assert
        var persisted = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);
        persisted!.ApiKeyEncrypted.Should().Be(encryptedKey);
    }

    [Fact]
    public async Task GetAllAsync_MultipleConfigurations_ShouldReturnOrderedByCreatedAt()
    {
        // Arrange
        await CleanDatabaseAsync();
        var config1 = CreateTestConfiguration(TestConfigId1, "First");
        var config2 = CreateTestConfiguration(TestConfigId2, "Second");
        var config3 = CreateTestConfiguration(TestConfigId3, "Third");

        await _repository!.AddAsync(config1, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);
        await Task.Delay(TestConstants.Timing.MediumDelay, TestCancellationToken); // Ensure different timestamps

        await _repository.AddAsync(config2, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);
        await Task.Delay(TestConstants.Timing.MediumDelay, TestCancellationToken);

        await _repository.AddAsync(config3, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.GetAllAsync(TestCancellationToken);

        // Assert
        result.Should().HaveCount(3);
        result.Should().BeInDescendingOrder(c => c.CreatedAt);
        result[0].Name.Should().Be("Third");
        result[1].Name.Should().Be("Second");
        result[2].Name.Should().Be("First");
    }

    #endregion

    #region Test Result History Tests (NEW - Week 3 Expansion)

    [Fact]
    public async Task RecordTestResult_MultipleTests_ShouldTrackHistory()
    {
        // Arrange
        await CleanDatabaseAsync();
        var config = CreateTestConfiguration(TestConfigId1, "Test History Config");
        await _repository!.AddAsync(config, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act - Record multiple test results
        for (var i = 1; i <= 3; i++)
        {
            _dbContext!.ChangeTracker.Clear();
            var loaded = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);
            loaded!.RecordTestResult(success: i % 2 == 0, result: $"Test {i} result");
            await _repository.UpdateAsync(loaded, TestCancellationToken);
            await _unitOfWork.SaveChangesAsync(TestCancellationToken);
            await Task.Delay(TestConstants.Timing.TinyDelay, TestCancellationToken); // Ensure distinct timestamps
        }

        // Assert
        var final = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);
        final!.LastTestedAt.Should().NotBeNull();
        final.LastTestResult.Should().Be("Test 3 result");
        final.LastTestedAt.Should().BeCloseTo(DateTime.UtcNow, TestConstants.Timing.AssertionTolerance);
    }

    [Fact]
    public async Task RecordTestResult_SuccessAndFailure_ShouldUpdateCorrectly()
    {
        // Arrange
        await CleanDatabaseAsync();
        var config = CreateTestConfiguration(TestConfigId1, "Test Success/Failure");
        await _repository!.AddAsync(config, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act - Record successful test
        _dbContext!.ChangeTracker.Clear();
        var loaded1 = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);
        loaded1!.RecordTestResult(success: true, result: "Connection successful");
        await _repository.UpdateAsync(loaded1, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        var firstTest = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);
        var firstTestTime = firstTest!.LastTestedAt!.Value;

        await Task.Delay(TestConstants.Timing.SmallDelay, TestCancellationToken);

        // Act - Record failed test
        _dbContext.ChangeTracker.Clear();
        var loaded2 = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);
        loaded2!.RecordTestResult(success: false, result: "Connection timeout");
        await _repository.UpdateAsync(loaded2, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Assert
        var final = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);
        final!.LastTestResult.Should().Be("Connection timeout");
        final.LastTestedAt.Should().BeAfter(firstTestTime);
    }

    #endregion

    #region Active Configuration Switching Tests (NEW - Week 3 Expansion)

    [Fact]
    public async Task SwitchActiveConfiguration_AtomicDeactivateActivate_ShouldComplete()
    {
        // Arrange
        await CleanDatabaseAsync();
        var oldConfig = CreateTestConfiguration(TestConfigId1, "Old Active");
        var newConfig = CreateTestConfiguration(TestConfigId2, "New Active");
        newConfig.Deactivate(); // Start as inactive

        await _repository!.AddAsync(oldConfig, TestCancellationToken);
        await _repository.AddAsync(newConfig, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act - Atomic switch
        _dbContext!.ChangeTracker.Clear();
        var oldLoaded = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);
        var newLoaded = await _repository.GetByIdAsync(TestConfigId2, TestCancellationToken);

        oldLoaded!.Deactivate();
        newLoaded!.Activate();

        await _repository.UpdateAsync(oldLoaded, TestCancellationToken);
        await _repository.UpdateAsync(newLoaded, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Assert
        var activeConfig = await _repository.GetActiveConfigurationAsync(TestCancellationToken);
        activeConfig.Should().NotBeNull();
        activeConfig!.Id.Should().Be(TestConfigId2);
        activeConfig.Name.Should().Be("New Active");

        var deactivated = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);
        deactivated!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ActivateConfiguration_WhenMultipleActive_ShouldAllowMultiple()
    {
        // Arrange
        await CleanDatabaseAsync();
        var config1 = CreateTestConfiguration(TestConfigId1, "Active 1");
        var config2 = CreateTestConfiguration(TestConfigId2, "Active 2");

        await _repository!.AddAsync(config1, TestCancellationToken);
        await _repository.AddAsync(config2, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act - Both remain active (repository doesn't enforce single active)
        var allConfigs = await _repository.GetAllAsync(TestCancellationToken);
        var activeConfigs = allConfigs.Where(c => c.IsActive).ToList();

        // Assert - Domain/application layer handles single-active enforcement
        activeConfigs.Should().HaveCount(2);
    }

    #endregion

    #region URL Validation Edge Cases (NEW - Week 3 Expansion)

    [Fact]
    public async Task UpdateConfiguration_WithNewWebhookUrl_ShouldPersist()
    {
        // Arrange
        await CleanDatabaseAsync();
        var config = CreateTestConfiguration(
            TestConfigId1,
            "Webhook Test",
            webhookUrl: "https://original-webhook.com/hook"
        );
        await _repository!.AddAsync(config, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act - Update webhook URL
        _dbContext!.ChangeTracker.Clear();
        var loaded = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);
        loaded!.UpdateConfiguration(
            name: "Webhook Test",
            baseUrl: loaded.BaseUrl,
            webhookUrl: new WorkflowUrl("https://updated-webhook.com/newhook")
        );
        await _repository.UpdateAsync(loaded, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Assert
        var updated = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);
        updated!.WebhookUrl.Should().NotBeNull();
        updated.WebhookUrl!.Value.Should().Be("https://updated-webhook.com/newhook");
    }

    [Fact]
    public async Task AddAsync_WithNullWebhookUrl_ShouldAllowNull()
    {
        // Arrange
        await CleanDatabaseAsync();
        var config = CreateTestConfiguration(
            TestConfigId1,
            "No Webhook Config",
            webhookUrl: null
        );

        // Act
        await _repository!.AddAsync(config, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Assert
        var persisted = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);
        persisted!.WebhookUrl.Should().BeNull();
    }

    #endregion

    #region Concurrent Configuration Changes Tests (NEW - Week 3 Expansion)

    [Fact]
    public async Task ConcurrentUpdate_DifferentConfigs_ShouldSucceed()
    {
        // Arrange
        await CleanDatabaseAsync();
        var config1 = CreateTestConfiguration(TestConfigId1, "Config 1");
        var config2 = CreateTestConfiguration(TestConfigId2, "Config 2");
        await _repository!.AddAsync(config1, TestCancellationToken);
        await _repository.AddAsync(config2, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act - Update different configurations concurrently
        _dbContext!.ChangeTracker.Clear();
        var loaded1 = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);
        var loaded2 = await _repository.GetByIdAsync(TestConfigId2, TestCancellationToken);

        loaded1!.UpdateConfiguration("Updated Config 1", loaded1.BaseUrl);
        loaded2!.UpdateConfiguration("Updated Config 2", loaded2.BaseUrl);

        await _repository.UpdateAsync(loaded1, TestCancellationToken);
        await _repository.UpdateAsync(loaded2, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Assert
        var updated1 = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);
        var updated2 = await _repository.GetByIdAsync(TestConfigId2, TestCancellationToken);
        updated1!.Name.Should().Be("Updated Config 1");
        updated2!.Name.Should().Be("Updated Config 2");
    }

    [Fact]
    public async Task ConcurrentUpdate_SameConfig_ShouldUseLastWriteWins()
    {
        // Arrange
        await CleanDatabaseAsync();
        var config = CreateTestConfiguration(TestConfigId1, "Concurrent Test");
        await _repository!.AddAsync(config, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Load same entity twice
        _dbContext!.ChangeTracker.Clear();
        var instance1 = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);
        _dbContext.ChangeTracker.Clear();
        var instance2 = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);

        // Act - First update succeeds
        instance1!.UpdateConfiguration("Update 1", instance1.BaseUrl);
        await _repository.UpdateAsync(instance1, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Second update also succeeds (no concurrency token = last-write-wins)
        _dbContext.ChangeTracker.Clear();
        instance2!.UpdateConfiguration("Update 2", instance2.BaseUrl);
        await _repository.UpdateAsync(instance2, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Assert - Last write wins
        var final = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);
        final!.Name.Should().Be("Update 2");
    }

    #endregion

    #region Webhook URL Updates and Validation Tests (NEW - Week 3 Expansion)

    [Fact]
    public async Task UpdateConfiguration_WithNullWebhook_ShouldKeepExistingValue()
    {
        // Arrange
        await CleanDatabaseAsync();
        var config = CreateTestConfiguration(
            TestConfigId1,
            "Keep Webhook",
            webhookUrl: "https://webhook.com/original"
        );
        await _repository!.AddAsync(config, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act - Update with null webhook (domain keeps existing value)
        _dbContext!.ChangeTracker.Clear();
        var loaded = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);
        loaded!.UpdateConfiguration(
            name: "Keep Webhook Updated",
            baseUrl: loaded.BaseUrl,
            webhookUrl: null // Null means "don't change", not "remove"
        );
        await _repository.UpdateAsync(loaded, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Assert - WebhookUrl should remain unchanged
        var updated = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);
        updated!.WebhookUrl.Should().NotBeNull();
        updated.WebhookUrl!.Value.Should().Be("https://webhook.com/original");
        updated.Name.Should().Be("Keep Webhook Updated");
    }

    [Fact]
    public async Task UpdateConfiguration_ChangeBaseUrlAndWebhook_ShouldUpdateBoth()
    {
        // Arrange
        await CleanDatabaseAsync();
        var config = CreateTestConfiguration(
            TestConfigId1,
            "Update Both URLs",
            "https://old-base.com",
            "https://old-webhook.com/hook"
        );
        await _repository!.AddAsync(config, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act - Update both URLs
        _dbContext!.ChangeTracker.Clear();
        var loaded = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);
        loaded!.UpdateConfiguration(
            name: "Update Both URLs",
            baseUrl: new WorkflowUrl("https://new-base.com"),
            webhookUrl: new WorkflowUrl("https://new-webhook.com/newhook")
        );
        await _repository.UpdateAsync(loaded, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Assert
        var updated = await _repository.GetByIdAsync(TestConfigId1, TestCancellationToken);
        updated!.BaseUrl.Value.Should().Be("https://new-base.com");
        updated.WebhookUrl.Should().NotBeNull();
        updated.WebhookUrl!.Value.Should().Be("https://new-webhook.com/newhook");
        updated.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TestConstants.Timing.AssertionTolerance);
    }

    #endregion
}
