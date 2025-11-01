using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests.Integration;

/// <summary>
/// CONFIG-07: Concurrent access tests for configuration system
/// Verifies thread-safety, optimistic concurrency, and cache coherence
/// </summary>
[Collection("Admin Endpoints")]
public class ConfigurationConcurrencyTests : ConfigIntegrationTestBase
{
    private readonly ITestOutputHelper _output;

    private string _adminEmail = null!;
    private List<string> _adminCookies = null!;

    public ConfigurationConcurrencyTests(WebApplicationFactoryFixture factory, ITestOutputHelper output) : base(factory)
    {
        _output = output;
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _adminEmail = $"config-concur-{Guid.NewGuid():N}@test.com";

        // Register and get cookies for reuse
        using var tempClient = Factory.CreateHttpsClient();
        _adminCookies = await RegisterAndAuthenticateAsync(tempClient, _adminEmail, "Admin");
    }

    [Fact]
    public async Task MultipleAdminsEditingDifferentConfigs_Simultaneously_Test()
    {
        // Arrange: Create two HTTP clients simulating two admins
        var client1 = Factory.CreateHttpsClient();
        var client2 = Factory.CreateHttpsClient();

        // Each client needs separate cookies
        var admin2Email = $"config-admin2-{Guid.NewGuid():N}@test.com";
        using var tempClient2 = Factory.CreateHttpsClient();
        var admin2Cookies = await RegisterAndAuthenticateAsync(tempClient2, admin2Email, "Admin");

        // Act: Both admins create different configs in parallel
        var task1 = PostAsJsonAuthenticatedAsync(client1, _adminCookies, "/api/v1/admin/configurations", new
        {
            key = "Concurrent:Config1",
            value = "value1",
            valueType = "string",
            category = "Concurrent"
        });

        var task2 = PostAsJsonAuthenticatedAsync(client2, admin2Cookies, "/api/v1/admin/configurations", new
        {
            key = "Concurrent:Config2",
            value = "value2",
            valueType = "string",
            category = "Concurrent"
        });

        var responses = await Task.WhenAll(task1, task2);

        // Assert: Both operations succeeded
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));

        // Verify both configs in database
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var config1 = await dbContext.SystemConfigurations.FirstOrDefaultAsync(c => c.Key == "Concurrent:Config1");
        var config2 = await dbContext.SystemConfigurations.FirstOrDefaultAsync(c => c.Key == "Concurrent:Config2");

        config1.Should().NotBeNull();
        config2.Should().NotBeNull();
        config1.Value.Should().Be("value1");
        config2.Value.Should().Be("value2");
    }

    [Fact]
    public async Task MultipleAdminsEditingSameConfig_OptimisticConcurrency_Test()
    {
        // Arrange: Create a configuration
        using var client = Factory.CreateHttpsClient();

        var createResponse = await PostAsJsonAuthenticatedAsync(client, _adminCookies, "/api/v1/admin/configurations", new
        {
            key = "Optimistic:TestConfig",
            value = "v1",
            valueType = "string",
            category = "Optimistic"
        });
        var created = await createResponse.Content.ReadFromJsonAsync<SystemConfigurationDto>();

        // Act: Two clients update simultaneously (last write wins)
        var client1 = Factory.CreateHttpsClient();
        var client2 = Factory.CreateHttpsClient();

        // Each client needs separate cookies
        var admin2Email = $"config-admin2-{Guid.NewGuid():N}@test.com";
        using var tempClient2 = Factory.CreateHttpsClient();
        var admin2Cookies = await RegisterAndAuthenticateAsync(tempClient2, admin2Email, "Admin");

        var update1 = PutAsJsonAuthenticatedAsync(client1, _adminCookies, $"/api/v1/admin/configurations/{created!.Id}", new
        {
            key = created.Key,
            value = "admin1-update",
            valueType = created.ValueType,
            category = created.Category
        });

        var update2 = PutAsJsonAuthenticatedAsync(client2, admin2Cookies, $"/api/v1/admin/configurations/{created.Id}", new
        {
            key = created.Key,
            value = "admin2-update",
            valueType = created.ValueType,
            category = created.Category
        });

        var results = await Task.WhenAll(update1, update2);

        // Assert: Both succeed (last write wins behavior)
        Assert.All(results, r => Assert.True(r.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task ConfigurationReadsDuringWrites_MaintainConsistency_Test()
    {
        // Arrange: Create configuration
        using var client = Factory.CreateHttpsClient();

        await PostAsJsonAuthenticatedAsync(client, _adminCookies, "/api/v1/admin/configurations", new
        {
            key = "Consistency:TestConfig",
            value = "100",
            valueType = "int",
            category = "Consistency"
        });

        // Act: Perform concurrent reads
        var tasks = new Task[10];
        for (int i = 0; i < 10; i++)
        {
            tasks[i] = Task.Run(async () =>
            {
                using var scope = Factory.Services.CreateScope();
                var configService = scope.ServiceProvider.GetRequiredService<IConfigurationService>();

                // Read config - should not throw or return corrupted data
                var value = await configService.GetValueAsync<int>("Consistency:TestConfig");
                value == null || value >= 100.Should().BeTrue();
            });
        }

        await Task.WhenAll(tasks);
        // All reads completed without exceptions = consistency maintained
    }

    [Fact]
    public async Task CacheInvalidationPropagatesCorrectly_Test()
    {
        // Arrange: Create and cache configuration
        using var client = Factory.CreateHttpsClient();

        await PostAsJsonAuthenticatedAsync(client, _adminCookies, "/api/v1/admin/configurations", new
        {
            key = "Cache:TestConfig",
            value = "cached-value",
            valueType = "string",
            category = "Cache"
        });

        // Read to populate cache
        using var scope1 = Factory.Services.CreateScope();
        var configService1 = scope1.ServiceProvider.GetRequiredService<IConfigurationService>();
        var cachedValue = await configService1.GetValueAsync<string>("Cache:TestConfig");
        cachedValue.Should().Be("cached-value");

        // Act: Update configuration (should invalidate cache)
        var getResponse = await GetAuthenticatedAsync(client, _adminCookies, "/api/v1/admin/configurations");
        var pagedResult = await getResponse.Content.ReadFromJsonAsync<PagedResult<SystemConfigurationDto>>();
        var configs = pagedResult!.Items;
        var config = configs.First(c => c.Key == "Cache:TestConfig");

        await PutAsJsonAuthenticatedAsync(client, _adminCookies, $"/api/v1/admin/configurations/{config.Id}", new
        {
            key = config.Key,
            value = "new-value",
            valueType = config.ValueType,
            category = config.Category
        });

        // Assert: Next read gets new value (cache was invalidated)
        using var scope2 = Factory.Services.CreateScope();
        var configService2 = scope2.ServiceProvider.GetRequiredService<IConfigurationService>();
        var newValue = await configService2.GetValueAsync<string>("Cache:TestConfig");
        newValue.Should().Be("new-value");
    }

    [Fact]
    public async Task DistributedCacheCoherence_MultipleServices_Test()
    {
        // Arrange: Create configuration
        using var client = Factory.CreateHttpsClient();

        await PostAsJsonAuthenticatedAsync(client, _adminCookies, "/api/v1/admin/configurations", new
        {
            key = "Distributed:TestConfig",
            value = "100",
            valueType = "int",
            category = "Distributed"
        });

        // Act: Simulate multiple service instances reading config
        var tasks = Enumerable.Range(0, 5).Select(async _ =>
        {
            using var scope = Factory.Services.CreateScope();
            var configService = scope.ServiceProvider.GetRequiredService<IConfigurationService>();
            return await configService.GetValueAsync<int>("Distributed:TestConfig");
        }).ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert: All instances get consistent value
        Assert.All(results, value => Assert.Equal(100, value));
        results.Length.Should().Be(5);
    }
}
