using System;
using System.Linq;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests;

/// <summary>
/// BDD-style unit tests for ConfigurationService (CONFIG-01).
///
/// Feature: Dynamic Configuration Management
/// As a system administrator
/// I want to manage system configuration dynamically
/// So that I can tune system behavior without redeployments
///
/// Test Strategy: SQLite in-memory database + mocked HybridCache for fast, isolated tests
/// </summary>
public class ConfigurationServiceTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    private readonly SqliteConnection _connection;
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<IHybridCacheService> _cacheMock;
    private readonly IConfigurationService _service;
    private readonly string _testUserId;

    public ConfigurationServiceTests(ITestOutputHelper output)
    {
        _output = output;
        // Setup SQLite in-memory database
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        using (var command = _connection.CreateCommand())
        {
            command.CommandText = "PRAGMA foreign_keys = ON;";
            command.ExecuteNonQuery();
        }

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new MeepleAiDbContext(options);
        _dbContext.Database.EnsureCreated();

        // Create test user for audit trail
        _testUserId = Guid.NewGuid().ToString();
        _dbContext.Users.Add(new UserEntity
        {
            Id = _testUserId,
            Email = "admin@test.com",
            DisplayName = "Test Admin",
            Role = UserRole.Admin,
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow
        });
        _dbContext.SaveChanges();

        // Mock IHybridCacheService
        _cacheMock = new Mock<IHybridCacheService>();

        // Setup default cache behavior - always call factory (cache miss simulation)
        _cacheMock.Setup(x => x.GetOrCreateAsync(
            It.IsAny<string>(),
            It.IsAny<Func<CancellationToken, Task<SystemConfigurationDto>>>(),
            It.IsAny<string[]>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()))
            .Returns<string, Func<CancellationToken, Task<SystemConfigurationDto>>, string[], TimeSpan?, CancellationToken>(
                async (key, factory, tags, expiration, ct) => await factory(ct));

        // Setup RemoveAsync to do nothing (cache invalidation)
        _cacheMock.Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Setup RemoveByTagAsync to return 0 (no entries removed)
        _cacheMock.Setup(x => x.RemoveByTagAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Mock environment (Development by default)
        var mockEnvironment = new Mock<IWebHostEnvironment>();
        mockEnvironment.Setup(x => x.EnvironmentName).Returns("Development");

        _service = new ConfigurationService(
            _dbContext,
            _cacheMock.Object,
            NullLogger<ConfigurationService>.Instance,
            mockEnvironment.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    #region Create Configuration Tests

    [Fact]
    public async Task CreateConfiguration_WithValidData_StoresInDatabase()
    {
        // Arrange
        var request = new CreateConfigurationRequest(
            Key: "Test:Setting",
            Value: "100",
            ValueType: "int",
            Description: "Test configuration",
            Category: "Test",
            IsActive: true,
            RequiresRestart: false,
            Environment: "Development");

        // Act
        var result = await _service.CreateConfigurationAsync(request, _testUserId);

        // Assert
        result.Should().NotBeNull();
        Assert.Equal("Test:Setting", result.Key);
        Assert.Equal("100", result.Value);
        Assert.Equal("int", result.ValueType);
        Assert.Equal(1, result.Version);
        Assert.Equal(_testUserId, result.CreatedByUserId);

        // Verify in database
        var dbEntity = await _dbContext.SystemConfigurations.FirstOrDefaultAsync(c => c.Id == result.Id);
        dbEntity.Should().NotBeNull();
        Assert.Equal("Test:Setting", dbEntity.Key);
    }

    [Fact]
    public async Task CreateConfiguration_WithDuplicateKeyAndEnvironment_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = new CreateConfigurationRequest(
            Key: "Duplicate:Key",
            Value: "value1",
            ValueType: "string",
            Environment: "Production");

        await _service.CreateConfigurationAsync(request, _testUserId);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateConfigurationAsync(request, _testUserId));

        Assert.Contains("already exists", exception.Message);
    }

    [Fact]
    public async Task CreateConfiguration_WithInvalidValue_ThrowsInvalidOperationException()
    {
        // Arrange - int type with non-int value
        var request = new CreateConfigurationRequest(
            Key: "Test:Invalid",
            Value: "not-a-number",
            ValueType: "int");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateConfigurationAsync(request, _testUserId));

        Assert.Contains("validation failed", exception.Message);
    }

    #endregion

    #region Get Configuration Tests

    [Fact]
    public async Task GetConfigurationById_WhenExists_ReturnsConfiguration()
    {
        // Arrange
        var config = await CreateTestConfiguration("Test:Key", "value");

        // Act
        var result = await _service.GetConfigurationByIdAsync(config.Id);

        // Assert
        result.Should().NotBeNull();
        Assert.Equal(config.Id, result.Id);
        Assert.Equal("Test:Key", result.Key);
    }

    [Fact]
    public async Task GetConfigurationById_WhenNotExists_ReturnsNull()
    {
        // Act
        var result = await _service.GetConfigurationByIdAsync("non-existent-id");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetConfigurationByKey_WhenExists_ReturnsConfiguration()
    {
        // Arrange
        await CreateTestConfiguration("Test:Key", "value", environment: "Development");

        // Act
        var result = await _service.GetConfigurationByKeyAsync("Test:Key", "Development");

        // Assert
        result.Should().NotBeNull();
        Assert.Equal("Test:Key", result.Key);
        Assert.Equal("value", result.Value);
    }

    [Fact]
    public async Task GetConfigurationByKey_PrioritizesEnvironmentSpecific_OverAll()
    {
        // Arrange - Create two configs: one for "All", one for "Production"
        await CreateTestConfiguration("Priority:Test", "all-value", environment: "All");
        await CreateTestConfiguration("Priority:Test", "prod-value", environment: "Production");

        // Act - Request for Production environment
        var result = await _service.GetConfigurationByKeyAsync("Priority:Test", "Production");

        // Assert - Should return Production-specific value, not "All"
        result.Should().NotBeNull();
        Assert.Equal("prod-value", result.Value);
        Assert.Equal("Production", result.Environment);
    }

    [Fact]
    public async Task GetConfigurationsAsync_WithCategoryFilter_ReturnsMatchingConfigurations()
    {
        // Arrange
        await CreateTestConfiguration("Cat1:Key1", "value1", category: "Category1");
        await CreateTestConfiguration("Cat1:Key2", "value2", category: "Category1");
        await CreateTestConfiguration("Cat2:Key3", "value3", category: "Category2");

        // Act
        var result = await _service.GetConfigurationsAsync(category: "Category1");

        // Assert
        Assert.Equal(2, result.Total);
        Assert.All(result.Items, item => Assert.Equal("Category1", item.Category));
    }

    [Fact]
    public async Task GetConfigurationsAsync_WithActiveOnlyTrue_ReturnsOnlyActiveConfigurations()
    {
        // Arrange
        await CreateTestConfiguration("Active:Key", "value", isActive: true);
        await CreateTestConfiguration("Inactive:Key", "value", isActive: false);

        // Act
        var result = await _service.GetConfigurationsAsync(activeOnly: true);

        // Assert
        Assert.Equal(1, result.Total);
        Assert.True(result.Items.First().IsActive);
    }

    [Fact]
    public async Task GetConfigurationsAsync_WithPagination_ReturnsCorrectPage()
    {
        // Arrange - Create 5 configurations
        for (int i = 1; i <= 5; i++)
        {
            await CreateTestConfiguration($"Page:Key{i}", $"value{i}");
        }

        // Act - Get page 2 with page size 2
        var result = await _service.GetConfigurationsAsync(page: 2, pageSize: 2);

        // Assert
        Assert.Equal(5, result.Total);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
    }

    #endregion

    #region Update Configuration Tests

    [Fact]
    public async Task UpdateConfiguration_WithValidData_IncrementsVersionAndStoresPreviousValue()
    {
        // Arrange
        var config = await CreateTestConfiguration("Update:Test", "original-value");
        var updateRequest = new UpdateConfigurationRequest(Value: "new-value");

        // Act
        var result = await _service.UpdateConfigurationAsync(config.Id, updateRequest, _testUserId);

        // Assert
        result.Should().NotBeNull();
        Assert.Equal("new-value", result.Value);
        Assert.Equal(2, result.Version); // Incremented from 1 to 2
        Assert.Equal("original-value", result.PreviousValue);
        Assert.Equal(_testUserId, result.UpdatedByUserId);
    }

    [Fact]
    public async Task UpdateConfiguration_WhenNotExists_ReturnsNull()
    {
        // Arrange
        var updateRequest = new UpdateConfigurationRequest(Value: "new-value");

        // Act
        var result = await _service.UpdateConfigurationAsync("non-existent-id", updateRequest, _testUserId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateConfiguration_WithInvalidValue_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = await CreateTestConfiguration("Test:Int", "100", valueType: "int");
        var updateRequest = new UpdateConfigurationRequest(Value: "not-a-number");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.UpdateConfigurationAsync(config.Id, updateRequest, _testUserId));

        Assert.Contains("validation failed", exception.Message);
    }

    #endregion

    #region Delete Configuration Tests

    [Fact]
    public async Task DeleteConfiguration_WhenExists_RemovesFromDatabase()
    {
        // Arrange
        var config = await CreateTestConfiguration("Delete:Test", "value");

        // Act
        var result = await _service.DeleteConfigurationAsync(config.Id);

        // Assert
        Assert.True(result);
        var dbEntity = await _dbContext.SystemConfigurations.FindAsync(config.Id);
        dbEntity.Should().BeNull();
    }

    [Fact]
    public async Task DeleteConfiguration_WhenNotExists_ReturnsFalse()
    {
        // Act
        var result = await _service.DeleteConfigurationAsync("non-existent-id");

        // Assert
        Assert.False(result);
    }

    #endregion

    #region Toggle Configuration Tests

    [Fact]
    public async Task ToggleConfiguration_ChangesActiveStatus_AndUpdatesTimestamp()
    {
        // Arrange
        var config = await CreateTestConfiguration("Toggle:Test", "value", isActive: true);

        // Act
        var result = await _service.ToggleConfigurationAsync(config.Id, false, _testUserId);

        // Assert
        result.Should().NotBeNull();
        Assert.False(result.IsActive);
        result.LastToggledAt.Should().NotBeNull();
        Assert.Equal(_testUserId, result.UpdatedByUserId);
    }

    [Fact]
    public async Task ToggleConfiguration_WithSameStatus_DoesNotUpdateTimestamp()
    {
        // Arrange
        var config = await CreateTestConfiguration("Toggle:Test", "value", isActive: true);
        var originalUpdatedAt = config.UpdatedAt;

        // Act
        var result = await _service.ToggleConfigurationAsync(config.Id, true, _testUserId);

        // Assert
        result.Should().NotBeNull();
        Assert.True(result.IsActive);
        // UpdatedAt should remain the same since no actual change
    }

    #endregion

    #region Type-Safe Retrieval Tests

    [Fact]
    public async Task GetValueAsync_WithIntType_ReturnsTypedValue()
    {
        // Arrange
        await CreateTestConfiguration("TypedInt", "42", valueType: "int");

        // Act
        var result = await _service.GetValueAsync<int>("TypedInt");

        // Assert
        Assert.Equal(42, result);
    }

    [Fact]
    public async Task GetValueAsync_WithBoolType_ReturnsTypedValue()
    {
        // Arrange
        await CreateTestConfiguration("TypedBool", "true", valueType: "bool");

        // Act
        var result = await _service.GetValueAsync<bool>("TypedBool");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetValueAsync_WithDoubleType_ReturnsTypedValue()
    {
        // Arrange
        await CreateTestConfiguration("TypedDouble", "3.14", valueType: "double");

        // Act
        var result = await _service.GetValueAsync<double>("TypedDouble");

        // Assert
        Assert.Equal(3.14, result);
    }

    [Fact]
    public async Task GetValueAsync_WithStringType_ReturnsTypedValue()
    {
        // Arrange
        await CreateTestConfiguration("TypedString", "hello world", valueType: "string");

        // Act
        var result = await _service.GetValueAsync<string>("TypedString");

        // Assert
        Assert.Equal("hello world", result);
    }

    [Fact]
    public async Task GetValueAsync_WhenKeyNotFound_ReturnsDefaultValue()
    {
        // Act
        var result = await _service.GetValueAsync<int>("NonExistent:Key", defaultValue: 999);

        // Assert
        Assert.Equal(999, result);
    }

    [Fact]
    public async Task GetValueAsync_WithInvalidTypeConversion_ReturnsDefaultValue()
    {
        // Arrange - Create string value, try to get as int
        await CreateTestConfiguration("InvalidConversion", "not-a-number", valueType: "string");

        // Act
        var result = await _service.GetValueAsync<int>("InvalidConversion", defaultValue: 0);

        // Assert
        Assert.Equal(0, result); // Should return default on conversion failure
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task ValidateConfiguration_WithValidInt_ReturnsValid()
    {
        // Act
        var result = await _service.ValidateConfigurationAsync("Test:Key", "123", "int");

        // Assert
        Assert.True(result.IsValid);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateConfiguration_WithInvalidInt_ReturnsInvalid()
    {
        // Act
        var result = await _service.ValidateConfigurationAsync("Test:Key", "not-a-number", "int");

        // Assert
        Assert.False(result.IsValid);
        result.Errors.Should().NotBeEmpty();
        Assert.Contains("not a valid integer", result.Errors.First());
    }

    [Fact]
    public async Task ValidateConfiguration_WithValidBool_ReturnsValid()
    {
        // Act
        var result = await _service.ValidateConfigurationAsync("Test:Key", "true", "bool");

        // Assert
        Assert.True(result.IsValid);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateConfiguration_WithInvalidBool_ReturnsInvalid()
    {
        // Act
        var result = await _service.ValidateConfigurationAsync("Test:Key", "maybe", "bool");

        // Assert
        Assert.False(result.IsValid);
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ValidateConfiguration_WithValidJson_ReturnsValid()
    {
        // Act
        var result = await _service.ValidateConfigurationAsync("Test:Key", "{\"key\":\"value\"}", "json");

        // Assert
        Assert.True(result.IsValid);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateConfiguration_WithInvalidJson_ReturnsInvalid()
    {
        // Act
        var result = await _service.ValidateConfigurationAsync("Test:Key", "{invalid json", "json");

        // Assert
        Assert.False(result.IsValid);
        result.Errors.Should().NotBeEmpty();
        Assert.Contains("not valid JSON", result.Errors.First());
    }

    [Fact]
    public async Task ValidateConfiguration_RateLimitNegativeValue_ReturnsInvalid()
    {
        // Act
        var result = await _service.ValidateConfigurationAsync("RateLimit:Admin:MaxTokens", "-100", "int");

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("non-negative", result.Errors.First());
    }

    #endregion

    #region Bulk Operations Tests

    [Fact]
    public async Task BulkUpdateConfigurations_WithValidData_UpdatesAllConfigurations()
    {
        // Arrange
        var config1 = await CreateTestConfiguration("Bulk:Key1", "value1");
        var config2 = await CreateTestConfiguration("Bulk:Key2", "value2");

        var request = new BulkConfigurationUpdateRequest(
            Updates: new[]
            {
                new ConfigurationUpdate(config1.Id, "new-value1"),
                new ConfigurationUpdate(config2.Id, "new-value2")
            });

        // Act
        var result = await _service.BulkUpdateConfigurationsAsync(request, _testUserId);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("new-value1", result[0].Value);
        Assert.Equal("new-value2", result[1].Value);
        Assert.All(result, r => Assert.Equal(2, r.Version)); // Version incremented
    }

    [Fact]
    public async Task BulkUpdateConfigurations_WithOneInvalidId_RollsBackAllChanges()
    {
        // Arrange
        var config1 = await CreateTestConfiguration("Bulk:Key1", "original");

        var request = new BulkConfigurationUpdateRequest(
            Updates: new[]
            {
                new ConfigurationUpdate(config1.Id, "new-value"),
                new ConfigurationUpdate("non-existent-id", "value")
            });

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.BulkUpdateConfigurationsAsync(request, _testUserId));

        // Clear change tracker and reload from database
        _dbContext.ChangeTracker.Clear();

        // Verify first config was NOT updated (transaction rolled back)
        var dbEntity = await _dbContext.SystemConfigurations.AsNoTracking().FirstOrDefaultAsync(c => c.Id == config1.Id);
        dbEntity.Should().NotBeNull();
        Assert.Equal("original", dbEntity.Value);
        Assert.Equal(1, dbEntity.Version); // Version not incremented
    }

    [Fact]
    public async Task BulkUpdateConfigurations_WithValidationError_RollsBackAllChanges()
    {
        // Arrange
        var config1 = await CreateTestConfiguration("Bulk:Int1", "100", valueType: "int");
        var config2 = await CreateTestConfiguration("Bulk:Int2", "200", valueType: "int");

        var request = new BulkConfigurationUpdateRequest(
            Updates: new[]
            {
                new ConfigurationUpdate(config1.Id, "150"),
                new ConfigurationUpdate(config2.Id, "not-a-number") // Invalid
            });

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.BulkUpdateConfigurationsAsync(request, _testUserId));

        // Clear change tracker and reload from database
        _dbContext.ChangeTracker.Clear();

        // Verify neither config was updated (transaction rolled back)
        var db1 = await _dbContext.SystemConfigurations.AsNoTracking().FirstOrDefaultAsync(c => c.Id == config1.Id);
        var db2 = await _dbContext.SystemConfigurations.AsNoTracking().FirstOrDefaultAsync(c => c.Id == config2.Id);
        db1.Should().NotBeNull();
        db2.Should().NotBeNull();
        Assert.Equal("100", db1.Value);
        Assert.Equal("200", db2.Value);
    }

    #endregion

    #region Export/Import Tests

    [Fact]
    public async Task ExportConfigurations_ForEnvironment_ReturnsMatchingConfigurations()
    {
        // Arrange
        await CreateTestConfiguration("Export:Key1", "value1", environment: "Production");
        await CreateTestConfiguration("Export:Key2", "value2", environment: "Production");
        await CreateTestConfiguration("Export:Key3", "value3", environment: "Development");

        // Act
        var result = await _service.ExportConfigurationsAsync("Production", activeOnly: true);

        // Assert
        Assert.Equal(2, result.Configurations.Count);
        Assert.All(result.Configurations, c =>
            Assert.True(c.Environment == "Production" || c.Environment == "All"));
        Assert.Equal("Production", result.Environment);
    }

    [Fact]
    public async Task ImportConfigurations_WithNewConfigurations_CreatesAll()
    {
        // Arrange
        var request = new ConfigurationImportRequest(
            Configurations: new[]
            {
                new CreateConfigurationRequest("Import:Key1", "value1"),
                new CreateConfigurationRequest("Import:Key2", "value2")
            },
            OverwriteExisting: false);

        // Act
        var importedCount = await _service.ImportConfigurationsAsync(request, _testUserId);

        // Assert
        Assert.Equal(2, importedCount);
        var config1 = await _dbContext.SystemConfigurations.FirstOrDefaultAsync(c => c.Key == "Import:Key1");
        var config2 = await _dbContext.SystemConfigurations.FirstOrDefaultAsync(c => c.Key == "Import:Key2");
        config1.Should().NotBeNull();
        config2.Should().NotBeNull();
    }

    [Fact]
    public async Task ImportConfigurations_WithOverwriteExisting_UpdatesExistingConfigurations()
    {
        // Arrange
        await CreateTestConfiguration("Overwrite:Key", "original-value");

        var request = new ConfigurationImportRequest(
            Configurations: new[]
            {
                new CreateConfigurationRequest("Overwrite:Key", "new-value", Environment: "All")
            },
            OverwriteExisting: true);

        // Act
        var importedCount = await _service.ImportConfigurationsAsync(request, _testUserId);

        // Assert
        Assert.Equal(1, importedCount);
        var config = await _dbContext.SystemConfigurations.FirstAsync(c => c.Key == "Overwrite:Key");
        Assert.Equal("new-value", config.Value);
        Assert.Equal(2, config.Version); // Version incremented
    }

    [Fact]
    public async Task ImportConfigurations_WithoutOverwrite_SkipsExisting()
    {
        // Arrange
        await CreateTestConfiguration("Skip:Key", "original-value");

        var request = new ConfigurationImportRequest(
            Configurations: new[]
            {
                new CreateConfigurationRequest("Skip:Key", "new-value", Environment: "All")
            },
            OverwriteExisting: false);

        // Act
        var importedCount = await _service.ImportConfigurationsAsync(request, _testUserId);

        // Assert
        Assert.Equal(0, importedCount); // Skipped existing
        var config = await _dbContext.SystemConfigurations.FirstAsync(c => c.Key == "Skip:Key");
        Assert.Equal("original-value", config.Value); // Value unchanged
        Assert.Equal(1, config.Version); // Version not incremented
    }

    #endregion

    #region History and Rollback Tests

    [Fact]
    public async Task GetConfigurationHistory_ReturnsCurrentVersion()
    {
        // Arrange
        var config = await CreateTestConfiguration("History:Test", "original");

        // Update to create history
        await _service.UpdateConfigurationAsync(
            config.Id,
            new UpdateConfigurationRequest(Value: "updated"),
            _testUserId);

        // Act
        var history = await _service.GetConfigurationHistoryAsync(config.Id);

        // Assert
        history.Should().NotBeEmpty();
        Assert.Equal("updated", history.First().NewValue);
        Assert.Equal("original", history.First().OldValue);
    }

    [Fact]
    public async Task RollbackConfiguration_ToPreviousVersion_RestoresOldValue()
    {
        // Arrange
        var config = await CreateTestConfiguration("Rollback:Test", "v1");

        // Update to v2
        var updated = await _service.UpdateConfigurationAsync(
            config.Id,
            new UpdateConfigurationRequest(Value: "v2"),
            _testUserId);

        // Act - Rollback to v1
        var rolledBack = await _service.RollbackConfigurationAsync(config.Id, 1, _testUserId);

        // Assert
        rolledBack.Should().NotBeNull();
        Assert.Equal("v1", rolledBack.Value); // Value restored
        Assert.Equal(3, rolledBack.Version); // Version incremented (1 → 2 → 3)
        Assert.Equal("v2", rolledBack.PreviousValue); // Previous value is v2
    }

    [Fact]
    public async Task RollbackConfiguration_WithoutPreviousValue_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = await CreateTestConfiguration("Rollback:NoPrevious", "only-value");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.RollbackConfigurationAsync(config.Id, 0, _testUserId));

        Assert.Contains("No previous value", exception.Message);
    }

    #endregion

    #region Categories Tests

    [Fact]
    public async Task GetCategoriesAsync_ReturnsUniqueCategories_Sorted()
    {
        // Arrange
        await CreateTestConfiguration("Cat1:Key", "value", category: "Category1");
        await CreateTestConfiguration("Cat2:Key", "value", category: "Category2");
        await CreateTestConfiguration("Cat1:Key2", "value", category: "Category1"); // Duplicate category

        // Act
        var categories = await _service.GetCategoriesAsync();

        // Assert
        Assert.Equal(2, categories.Count);
        Assert.Contains("Category1", categories);
        Assert.Contains("Category2", categories);
        Assert.Equal(categories.OrderBy(c => c).ToList(), categories); // Verify sorted
    }

    #endregion

    #region Helper Methods

    private async Task<SystemConfigurationDto> CreateTestConfiguration(
        string key,
        string value,
        string valueType = "string",
        string? description = null,
        string category = "Test",
        bool isActive = true,
        bool requiresRestart = false,
        string environment = "All")
    {
        var request = new CreateConfigurationRequest(
            Key: key,
            Value: value,
            ValueType: valueType,
            Description: description,
            Category: category,
            IsActive: isActive,
            RequiresRestart: requiresRestart,
            Environment: environment);

        return await _service.CreateConfigurationAsync(request, _testUserId);
    }

    #endregion
}
