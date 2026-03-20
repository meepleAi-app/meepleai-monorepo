using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.Authentication;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.Integration.SystemConfiguration;

/// <summary>
/// Integration tests for SystemConfiguration foreign key constraints.
/// Tests DeleteBehavior.Restrict for CreatedBy and UpdatedBy user references.
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Collection("Integration-GroupD")]
public class SystemConfigurationForeignKeyConstraintsTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private MeepleAiDbContext? _dbContext;
    private string? _connectionString;
    private readonly string _databaseName = $"test_syscfg_fk_{Guid.NewGuid():N}";

    public SystemConfigurationForeignKeyConstraintsTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        await TestcontainersWaitHelpers.WaitForPostgresReadyAsync(_connectionString);

        _dbContext = await Api.Tests.Infrastructure.TestHelpers.CreateDbContextAndMigrateAsync(_connectionString);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    /// <summary>
    /// Issue #2708: EF Core may throw InvalidOperationException instead of DbUpdateException
    /// when tracked entities have FK constraint violations detected in the change tracker.
    /// </summary>
    [Fact]
    public async Task DeleteUser_WithSystemConfigurationCreatedByReference_ThrowsFKConstraintException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
            Email = "creator@test.com",
            DisplayName = "Creator User",
            Role = "User"
        };
        _dbContext!.Users.Add(user);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var configId = Guid.NewGuid();
        var config = new SystemConfigurationEntity
        {
            Id = configId,
            Key = "test.config.key",
            Value = "test_value",
            ValueType = "string",
            Category = "General",
            Environment = "All",
            IsActive = true,
            RequiresRestart = false,
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedByUserId = userId
        };
        _dbContext.SystemConfigurations.Add(config);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert - FK Restrict prevents user deletion
        // Issue #2708: EF Core may throw either DbUpdateException (from database) or
        // InvalidOperationException (from change tracker at Remove() or SaveChangesAsync())
        Exception? exception = null;
        try
        {
            _dbContext.Users.Remove(user);
            await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        exception.Should().NotBeNull();
        Assert.True(exception is DbUpdateException or InvalidOperationException,
            $"Expected DbUpdateException or InvalidOperationException but got {exception.GetType().Name}: {exception.Message}");
    }

    /// <summary>
    /// Issue #2708: EF Core may throw InvalidOperationException instead of DbUpdateException
    /// when tracked entities have FK constraint violations detected in the change tracker.
    /// </summary>
    [Fact]
    public async Task DeleteUser_WithSystemConfigurationUpdatedByReference_ThrowsFKConstraintException()
    {
        // Arrange
        var creatorId = Guid.NewGuid();
        var creator = new UserEntity
        {
            Id = creatorId,
            Email = "creator@test.com",
            DisplayName = "Creator User",
            Role = "User"
        };

        var updaterId = Guid.NewGuid();
        var updater = new UserEntity
        {
            Id = updaterId,
            Email = "updater@test.com",
            DisplayName = "Updater User",
            Role = "User"
        };

        _dbContext!.Users.AddRange(creator, updater);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var configId = Guid.NewGuid();
        var config = new SystemConfigurationEntity
        {
            Id = configId,
            Key = "test.config.key",
            Value = "updated_value",
            ValueType = "string",
            Category = "General",
            Environment = "All",
            IsActive = true,
            RequiresRestart = false,
            Version = 2,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            UpdatedAt = DateTime.UtcNow,
            CreatedByUserId = creatorId,
            UpdatedByUserId = updaterId
        };
        _dbContext.SystemConfigurations.Add(config);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert - FK Restrict prevents creator deletion
        // Issue #2708: EF Core may throw either DbUpdateException (from database) or
        // InvalidOperationException (from change tracker) depending on entity tracking state
        Exception? exception = null;
        try
        {
            _dbContext.Users.Remove(creator);
            await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        exception.Should().NotBeNull();
        Assert.True(exception is DbUpdateException or InvalidOperationException,
            $"Expected DbUpdateException or InvalidOperationException but got {exception.GetType().Name}: {exception.Message}");
    }

    [Fact]
    public async Task DeleteUser_WithNoSystemConfigurationReferences_Succeeds()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
            Email = "standalone@test.com",
            DisplayName = "Standalone User",
            Role = "User"
        };
        _dbContext!.Users.Add(user);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act - Delete user without references
        _dbContext.Users.Remove(user);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert - User deleted successfully
        var deletedUser = await _dbContext.Users.FindAsync(userId);
        deletedUser.Should().BeNull();
    }

    [Fact]
    public async Task DeleteSystemConfiguration_WithUserReference_Succeeds()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
            Email = "owner@test.com",
            DisplayName = "Owner User",
            Role = "User"
        };
        _dbContext!.Users.Add(user);

        var configId = Guid.NewGuid();
        var config = new SystemConfigurationEntity
        {
            Id = configId,
            Key = "test.config.delete",
            Value = "deletable_value",
            ValueType = "string",
            Category = "General",
            Environment = "All",
            IsActive = true,
            RequiresRestart = false,
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedByUserId = userId
        };
        _dbContext.SystemConfigurations.Add(config);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act - Delete config (not user)
        _dbContext.SystemConfigurations.Remove(config);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert - Config deleted, user remains
        var deletedConfig = await _dbContext.SystemConfigurations.FindAsync(configId);
        deletedConfig.Should().BeNull();

        var remainingUser = await _dbContext.Users.FindAsync(userId);
        remainingUser.Should().NotBeNull();
    }
}
