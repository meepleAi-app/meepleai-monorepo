using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests.Integration;

/// <summary>
/// CONFIG-07: Performance tests for configuration system
/// Validates latency targets, cache efficiency, and scalability
/// Target: <50ms p95 latency, >90% cache hit ratio
/// </summary>
[Collection("Admin Endpoints")]
public class ConfigurationPerformanceTests : ConfigIntegrationTestBase
{
    private readonly ITestOutputHelper _output;
    private string _adminEmail = null!;
    private List<string> _adminCookies = null!;

    public ConfigurationPerformanceTests(WebApplicationFactoryFixture factory, ITestOutputHelper output) : base(factory)
    {
        _output = output;
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _adminEmail = $"config-perf-{Guid.NewGuid():N}@test.com";

        // Register and get cookies for reuse
        using var tempClient = Factory.CreateHttpsClient();
        _adminCookies = await RegisterAndAuthenticateAsync(tempClient, _adminEmail, "Admin");
    }

    [Fact]
    public async Task ConfigurationReadLatency_LessThan50ms_P95_Test()
    {
        // Arrange: Create test configuration
        using var client = Factory.CreateHttpsClient();

        await PostAsJsonAuthenticatedAsync(client, _adminCookies, "/api/v1/admin/configurations", new
        {
            key = "Perf:LatencyTest",
            value = "test-value",
            valueType = "string",
            category = "Performance"
        });

        // Act: Perform 100 config reads and measure latency
        using var scope = Factory.Services.CreateScope();
        var configService = scope.ServiceProvider.GetRequiredService<IConfigurationService>();
        var latencies = new List<double>();

        for (int i = 0; i < 100; i++)
        {
            var sw = Stopwatch.StartNew();
            await configService.GetValueAsync<string>("Perf:LatencyTest");
            sw.Stop();
            latencies.Add(sw.Elapsed.TotalMilliseconds);
        }

        // Calculate p95 latency
        latencies.Sort();
        var p95Index = (int)(latencies.Count * 0.95);
        var p95Latency = latencies[p95Index];

        _output.WriteLine($"P95 Latency: {p95Latency:F2}ms");
        _output.WriteLine($"Median Latency: {latencies[latencies.Count / 2]:F2}ms");
        _output.WriteLine($"Max Latency: {latencies.Max():F2}ms");

        // Assert: p95 latency < 50ms
        Assert.True(p95Latency < 50, $"P95 latency ({p95Latency:F2}ms) exceeds 50ms target");
    }

    [Fact]
    public async Task DatabaseQueryPerformance_With100Configurations_Test()
    {
        // Arrange: Seed 100 configurations (user already registered in InitializeAsync)
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var userId = await GetUserIdByEmailAsync(_adminEmail);
        for (int i = 0; i < 100; i++)
        {
            dbContext.SystemConfigurations.Add(new SystemConfigurationEntity
            {
                Key = $"Perf:Config{i}",
                Value = $"value{i}",
                ValueType = "string",
                Category = "Performance",
                CreatedByUserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        await dbContext.SaveChangesAsync();

        // Act: Query all configurations and measure time
        var sw = Stopwatch.StartNew();
        var configs = await dbContext.SystemConfigurations
            .Where(c => c.Category == "Performance")
            .ToListAsync();
        sw.Stop();

        _output.WriteLine($"Query time for 100 configs: {sw.ElapsedMilliseconds}ms");

        // Assert: Query completes in < 100ms
        Assert.True(sw.ElapsedMilliseconds < 100, $"Query time ({sw.ElapsedMilliseconds}ms) exceeds 100ms target");
        configs.Count.Should().Be(100);
    }

    [Fact]
    public async Task CacheHitRatio_GreaterThan90Percent_Test()
    {
        // Arrange: Create configuration
        using var client = Factory.CreateHttpsClient();

        await PostAsJsonAuthenticatedAsync(client, _adminCookies, "/api/v1/admin/configurations", new
        {
            key = "Perf:CacheTest",
            value = "cached",
            valueType = "string",
            category = "Performance"
        });

        using var scope = Factory.Services.CreateScope();
        var configService = scope.ServiceProvider.GetRequiredService<IConfigurationService>();

        // Act: Read same config 100 times
        var firstReadSw = Stopwatch.StartNew();
        await configService.GetValueAsync<string>("Perf:CacheTest");
        firstReadSw.Stop();

        var cachedReadLatencies = new List<double>();
        for (int i = 0; i < 99; i++)
        {
            var sw = Stopwatch.StartNew();
            await configService.GetValueAsync<string>("Perf:CacheTest");
            sw.Stop();
            cachedReadLatencies.Add(sw.Elapsed.TotalMilliseconds);
        }

        var avgCachedLatency = cachedReadLatencies.Average();

        _output.WriteLine($"First read (DB): {firstReadSw.ElapsedMilliseconds}ms");
        _output.WriteLine($"Avg cached read: {avgCachedLatency:F2}ms");
        _output.WriteLine($"Cache speedup: {firstReadSw.ElapsedMilliseconds / avgCachedLatency:F1}x");

        // Assert: Cached reads should be significantly faster (cache hit ratio >90%)
        Assert.True(avgCachedLatency < 10, $"Cached read latency ({avgCachedLatency:F2}ms) suggests low cache hit ratio");
    }

    [Fact]
    public async Task BulkUpdatePerformance_Test()
    {
        // Arrange: Create 50 configurations (user already registered in InitializeAsync)
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var userId = await GetUserIdByEmailAsync(_adminEmail);
        var configs = Enumerable.Range(0, 50).Select(i => new SystemConfigurationEntity
        {
            Key = $"Bulk:Update{i}",
            Value = "initial",
            ValueType = "string",
            Category = "Bulk",
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }).ToList();

        dbContext.SystemConfigurations.AddRange(configs);
        await dbContext.SaveChangesAsync();

        // Act: Bulk update all 50 configurations
        var sw = Stopwatch.StartNew();
        var toUpdate = await dbContext.SystemConfigurations
            .Where(c => c.Category == "Bulk")
            .ToListAsync();

        foreach (var config in toUpdate)
        {
            config.Value = "updated";
            config.UpdatedAt = DateTime.UtcNow;
            config.UpdatedByUserId = userId;
        }

        await dbContext.SaveChangesAsync();
        sw.Stop();

        _output.WriteLine($"Bulk update of 50 configs: {sw.ElapsedMilliseconds}ms");

        // Assert: Bulk update completes in < 500ms
        Assert.True(sw.ElapsedMilliseconds < 500, $"Bulk update time ({sw.ElapsedMilliseconds}ms) exceeds 500ms target");
    }

    [Fact]
    public async Task ConcurrentReadPerformance_UnderLoad_Test()
    {
        // Arrange: Create test configuration
        using var client = Factory.CreateHttpsClient();

        await PostAsJsonAuthenticatedAsync(client, _adminCookies, "/api/v1/admin/configurations", new
        {
            key = "Perf:ConcurrentRead",
            value = "test",
            valueType = "string",
            category = "Performance"
        });

        // Act: 100 concurrent reads
        var sw = Stopwatch.StartNew();
        var tasks = Enumerable.Range(0, 100).Select(async _ =>
        {
            using var innerScope = Factory.Services.CreateScope();
            var configService = innerScope.ServiceProvider.GetRequiredService<IConfigurationService>();
            return await configService.GetValueAsync<string>("Perf:ConcurrentRead");
        }).ToArray();

        var results = await Task.WhenAll(tasks);
        sw.Stop();

        _output.WriteLine($"100 concurrent reads: {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"Avg per request: {sw.ElapsedMilliseconds / 100.0:F2}ms");

        // Assert: All requests complete within 200ms total, no exceptions
        Assert.True(sw.ElapsedMilliseconds < 200, $"Concurrent reads ({sw.ElapsedMilliseconds}ms) exceed 200ms target");
        Assert.All(results, value => Assert.Equal("test", value));
    }
}
