using Api.Extensions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Regression tests to ensure IDistributedCache is always available in the DI container.
/// This prevents the startup failure that occurred when EnableL2Cache=false but services
/// still required IDistributedCache (e.g., EditorLockService, ShareLink handlers).
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class DistributedCacheRegistrationTests
{
    private static IWebHostEnvironment CreateTestEnvironment()
    {
        return new TestWebHostEnvironment();
    }

    [Fact]
    public void AddInfrastructureServices_WithL2CacheDisabled_RegistersDistributedMemoryCache()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["HybridCache:EnableL2Cache"] = "false",
                ["HybridCache:MaximumPayloadBytes"] = "1048576",
                ["HybridCache:DefaultExpiration"] = "00:05:00",
                ["ConnectionStrings:Redis"] = "localhost:6379" // Required but not used when L2 disabled
            })
            .Build();
        var environment = CreateTestEnvironment();

        // Add logging services required by the extension
        services.AddLogging();

        // Act
        services.AddInfrastructureServices(configuration, environment);
        var serviceProvider = services.BuildServiceProvider();

        // Assert - IDistributedCache should be resolved without throwing
        var cache = serviceProvider.GetService<IDistributedCache>();
        cache.Should().NotBeNull();
        cache.Should().BeOfType<Microsoft.Extensions.Caching.Distributed.MemoryDistributedCache>();
    }

    [Fact]
    public void AddInfrastructureServices_WithL2CacheEnabled_RegistersRedisCache()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["HybridCache:EnableL2Cache"] = "true",
                ["HybridCache:MaximumPayloadBytes"] = "1048576",
                ["HybridCache:DefaultExpiration"] = "00:05:00",
                ["ConnectionStrings:Redis"] = "localhost:6379"
            })
            .Build();
        var environment = CreateTestEnvironment();

        // Add logging services required by the extension
        services.AddLogging();

        // Act
        services.AddInfrastructureServices(configuration, environment);
        var serviceProvider = services.BuildServiceProvider();

        // Assert - IDistributedCache should be resolved without throwing
        var cache = serviceProvider.GetService<IDistributedCache>();
        cache.Should().NotBeNull();
        // When Redis is configured, it uses RedisCache implementation
        cache.GetType().Name.Should().Contain("Redis");
    }

    [Fact]
    public void AddInfrastructureServices_WithDefaultConfiguration_RegistersDistributedMemoryCache()
    {
        // Arrange - No HybridCache configuration at all (uses defaults)
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Redis"] = "localhost:6379"
            })
            .Build();
        var environment = CreateTestEnvironment();

        // Add logging services required by the extension
        services.AddLogging();

        // Act
        services.AddInfrastructureServices(configuration, environment);
        var serviceProvider = services.BuildServiceProvider();

        // Assert - IDistributedCache should be resolved without throwing
        // Default EnableL2Cache is false, so we should get MemoryDistributedCache
        var cache = serviceProvider.GetService<IDistributedCache>();
        cache.Should().NotBeNull();
        cache.Should().BeOfType<Microsoft.Extensions.Caching.Distributed.MemoryDistributedCache>();
    }

    /// <summary>
    /// Minimal test implementation of IWebHostEnvironment for DI testing.
    /// Uses "Testing" environment name to skip database configuration.
    /// </summary>
    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "Api.Tests";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
