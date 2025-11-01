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
        result.Key.Should().Be("Test:Setting");
        result.Value.Should().Be("100");
        result.ValueType.Should().Be("int");
        result.Version.Should().Be(1);
        result.CreatedByUserId.Should().Be(_testUserId);

        // Verify in database
        var dbEntity = await _dbContext.SystemConfigurations.FirstOrDefaultAsync(c => c.Id == result.Id);
        dbEntity.Should().NotBeNull();
        dbEntity.Key.Should().Be("Test:Setting");
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
        var act = async () => await _service.CreateConfigurationAsync(request, _testUserId);
        var exception = await act.Should().ThrowAsync<InvalidOperationException>();

        exception.Which.Message.Should().Contain("already exists");
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
        var act = async () => await _service.CreateConfigurationAsync(request, _testUserId);
        var exception = await act.Should().ThrowAsync<InvalidOperationException>();

        exception.Which.Message.Should().Contain("validation failed");
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
        result.Id.Should().Be(config.Id);
        result.Key.Should().Be("Test:Key");
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
        result.Key.Should().Be("Test:Key");
        result.Value.Should().Be("value");
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
        result.Value.Should().Be("prod-value");
        result.Environment.Should().Be("Production");
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
        result.Total.Should().Be(2);
        result.Items.Should().OnlyContain(item => item.Category == "Category1");
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
        result.Total.Should().Be(1);
        result.Items.First().IsActive.Should().BeTrue();
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
        result.Total.Should().Be(5);
        result.Items.Count.Should().Be(2);
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(2);
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
        result.Value.Should().Be("new-value");
        result.Version.Should().Be(2); // Incremented from 1 to 2
        result.PreviousValue.Should().Be("original-value");
        result.UpdatedByUserId.Should().Be(_testUserId);
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
        var act = async () => await _service.UpdateConfigurationAsync(config.Id, updateRequest, _testUserId);
        var exception = await act.Should().ThrowAsync<InvalidOperationException>();

        exception.Which.Message.Should().Contain("validation failed");
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
        result.Should().BeTrue();
        var dbEntity = await _dbContext.SystemConfigurations.FindAsync(config.Id);
        dbEntity.Should().BeNull();
    }

    [Fact]
    public async Task DeleteConfiguration_WhenNotExists_ReturnsFalse()
    {
        // Act
        var result = await _service.DeleteConfigurationAsync("non-existent-id");

        // Assert
        result.Should().BeFalse();
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
        result.IsActive.Should().BeFalse();
        result.LastToggledAt.Should().NotBeNull();
        result.UpdatedByUserId.Should().Be(_testUserId);
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
        result.IsActive.Should().BeTrue();
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
        result.Should().Be(42);
    }

    [Fact]
    public async Task GetValueAsync_WithBoolType_ReturnsTypedValue()
    {
        // Arrange
        await CreateTestConfiguration("TypedBool", "true", valueType: "bool");

        // Act
        var result = await _service.GetValueAsync<bool>("TypedBool");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task GetValueAsync_WithDoubleType_ReturnsTypedValue()
    {
        // Arrange
        await CreateTestConfiguration("TypedDouble", "3.14", valueType: "double");

        // Act
        var result = await _service.GetValueAsync<double>("TypedDouble");

        // Assert
        result.Should().Be(3.14);
    }

    [Fact]
    public async Task GetValueAsync_WithStringType_ReturnsTypedValue()
    {
        // Arrange
        await CreateTestConfiguration("TypedString", "hello world", valueType: "string");

        // Act
        var result = await _service.GetValueAsync<string>("TypedString");

        // Assert
        result.Should().Be("hello world");
    }

    [Fact]
    public async Task GetValueAsync_WhenKeyNotFound_ReturnsDefaultValue()
    {
        // Act
        var result = await _service.GetValueAsync<int>("NonExistent:Key", defaultValue: 999);

        // Assert
        result.Should().Be(999);
    }

    [Fact]
    public async Task GetValueAsync_WithInvalidTypeConversion_ReturnsDefaultValue()
    {
        // Arrange - Create string value, try to get as int
        await CreateTestConfiguration("InvalidConversion", "not-a-number", valueType: "string");

        // Act
        var result = await _service.GetValueAsync<int>("InvalidConversion", defaultValue: 0);

        // Assert
        result.Should().Be(0); // Should return default on conversion failure
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task ValidateConfiguration_WithValidInt_ReturnsValid()
    {
        // Act
        var result = await _service.ValidateConfigurationAsync("Test:Key", "123", "int");

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateConfiguration_WithInvalidInt_ReturnsInvalid()
    {
        // Act
        var result = await _service.ValidateConfigurationAsync("Test:Key", "not-a-number", "int");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
        result.Errors.First().Should().Contain("not a valid integer");
    }

    [Fact]
    public async Task ValidateConfiguration_WithValidBool_ReturnsValid()
    {
        // Act
        var result = await _service.ValidateConfigurationAsync("Test:Key", "true", "bool");

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateConfiguration_WithInvalidBool_ReturnsInvalid()
    {
        // Act
        var result = await _service.ValidateConfigurationAsync("Test:Key", "maybe", "bool");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ValidateConfiguration_WithValidJson_ReturnsValid()
    {
        // Act
        var result = await _service.ValidateConfigurationAsync("Test:Key", "{\"key\":\"value\"}", "json");

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateConfiguration_WithInvalidJson_ReturnsInvalid()
    {
        // Act
        var result = await _service.ValidateConfigurationAsync("Test:Key", "{invalid json", "json");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
        result.Errors.First().Should().Contain("not valid JSON");
    }

    [Fact]
    public async Task ValidateConfiguration_RateLimitNegativeValue_ReturnsInvalid()
    {
        // Act
        var result = await _service.ValidateConfigurationAsync("RateLimit:Admin:MaxTokens", "-100", "int");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.First().Should().Contain("non-negative");
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
        result.Count.Should().Be(2);
        result[0].Value.Should().Be("new-value1");
        result[1].Value.Should().Be("new-value2");
        result.Should().OnlyContain(r => r.Version == 2); // Version incremented
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
        var act = async () => await _service.BulkUpdateConfigurationsAsync(request, _testUserId);
        await act.Should().ThrowAsync<InvalidOperationException>();

        // Clear change tracker and reload from database
        _dbContext.ChangeTracker.Clear();

        // Verify first config was NOT updated (transaction rolled back)
        var dbEntity = await _dbContext.SystemConfigurations.AsNoTracking().FirstOrDefaultAsync(c => c.Id == config1.Id);
        dbEntity.Should().NotBeNull();
        dbEntity.Value.Should().Be("original");
        dbEntity.Version.Should().Be(1); // Version not incremented
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
        var act = async () => await _service.BulkUpdateConfigurationsAsync(request, _testUserId);
        await act.Should().ThrowAsync<InvalidOperationException>();

        // Clear change tracker and reload from database
        _dbContext.ChangeTracker.Clear();

        // Verify neither config was updated (transaction rolled back)
        var db1 = await _dbContext.SystemConfigurations.AsNoTracking().FirstOrDefaultAsync(c => c.Id == config1.Id);
        var db2 = await _dbContext.SystemConfigurations.AsNoTracking().FirstOrDefaultAsync(c => c.Id == config2.Id);
        db1.Should().NotBeNull();
        db2.Should().NotBeNull();
        db1.Value.Should().Be("100");
        db2.Value.Should().Be("200");
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
        result.Configurations.Count.Should().Be(2);
        result.Configurations.Should().OnlyContain(c =>
            c.Environment == "Production" || c.Environment == "All");
        result.Environment.Should().Be("Production");
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
        importedCount.Should().Be(2);
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
        importedCount.Should().Be(1);
        var config = await _dbContext.SystemConfigurations.FirstAsync(c => c.Key == "Overwrite:Key");
        config.Value.Should().Be("new-value");
        config.Version.Should().Be(2); // Version incremented
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
        importedCount.Should().Be(0); // Skipped existing
        var config = await _dbContext.SystemConfigurations.FirstAsync(c => c.Key == "Skip:Key");
        config.Value.Should().Be("original-value"); // Value unchanged
        config.Version.Should().Be(1); // Version not incremented
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
        history.First().NewValue.Should().Be("updated");
        history.First().OldValue.Should().Be("original");
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
        rolledBack.Value.Should().Be("v1"); // Value restored
        rolledBack.Version.Should().Be(3); // Version incremented (1 → 2 → 3)
        rolledBack.PreviousValue.Should().Be("v2"); // Previous value is v2
    }

    [Fact]
    public async Task RollbackConfiguration_WithoutPreviousValue_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = await CreateTestConfiguration("Rollback:NoPrevious", "only-value");

        // Act & Assert
        var act = async () => await _service.RollbackConfigurationAsync(config.Id, 0, _testUserId);
        var exception = await act.Should().ThrowAsync<InvalidOperationException>();

        exception.Which.Message.Should().Contain("No previous value");
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
        categories.Count.Should().Be(2);
        categories.Should().Contain("Category1");
        categories.Should().Contain("Category2");
        categories.Should().Be(categories.OrderBy(c => c).ToList()); // Verify sorted
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
