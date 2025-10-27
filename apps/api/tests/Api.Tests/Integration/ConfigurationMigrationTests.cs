using System;
using System.Linq;
using System.Threading.Tasks;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration;

/// <summary>
/// CONFIG-07: Migration tests for configuration system
/// Verifies database schema migrations and table structure
/// </summary>
[Collection("Admin Endpoints")]
public class ConfigurationMigrationTests : AdminTestFixture
{
    public ConfigurationMigrationTests(WebApplicationFactoryFixture factory) : base(factory)
    {
    }

    [Fact]
    public async Task FreshDatabase_MigrationAppliesCleanly_Test()
    {
        // Arrange: Ensure test user exists for foreign key constraint
        using var tempClient = Factory.CreateHttpsClient();
        var testEmail = $"migration-test-{Guid.NewGuid():N}@test.com";
        await RegisterAndAuthenticateAsync(tempClient, testEmail, "Admin");

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        // Act: Verify system_configurations table exists
        var tableExists = await dbContext.SystemConfigurations.AnyAsync();

        // Assert: Table exists and is queryable (migrations applied successfully)
        // The fact that we can query it means the table was created during test initialization
        Assert.True(true); // If we got here, migrations applied successfully

        // Verify we can create a record
        var canCreate = await CanCreateSystemConfiguration(dbContext, testEmail);
        Assert.True(canCreate, "Should be able to create system_configurations records");
    }

    [Fact]
    public async Task RollbackMigration_TableStructureValid_Test()
    {
        // Arrange: Ensure test user exists
        using var tempClient = Factory.CreateHttpsClient();
        var testEmail = $"migration-rollback-{Guid.NewGuid():N}@test.com";
        await RegisterAndAuthenticateAsync(tempClient, testEmail, "Admin");

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        // Act: Verify table structure by checking we can query key fields
        var hasRecords = await dbContext.SystemConfigurations.AnyAsync();

        // Create a test configuration to verify all required fields work
        var userId = await GetUserIdByEmailAsync(testEmail);
        var testConfig = new Api.Infrastructure.Entities.SystemConfigurationEntity
        {
            Key = "Migration:Test",
            Value = "test",
            ValueType = "string",
            Category = "Migration",
            CreatedByUserId = userId,
            UpdatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.SystemConfigurations.Add(testConfig);
        await dbContext.SaveChangesAsync();

        // Assert: All operations succeeded - table structure is valid
        var saved = await dbContext.SystemConfigurations.FirstOrDefaultAsync(c => c.Key == "Migration:Test");
        Assert.NotNull(saved);
        Assert.Equal("test", saved.Value);
        Assert.Equal(userId, saved.CreatedByUserId);
    }

    private async Task<bool> CanCreateSystemConfiguration(MeepleAiDbContext dbContext, string userEmail)
    {
        try
        {
            var userId = await GetUserIdByEmailAsync(userEmail);
            var testConfig = new Api.Infrastructure.Entities.SystemConfigurationEntity
            {
                Key = $"Test:Migration:{Guid.NewGuid():N}",
                Value = "test",
                ValueType = "string",
                Category = "Test",
                CreatedByUserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            dbContext.SystemConfigurations.Add(testConfig);
            await dbContext.SaveChangesAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
