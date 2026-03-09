using System.Reflection;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Infrastructure.Health.Checks;
using Api.Tests.Constants;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Api.Tests.Infrastructure.Health.Checks;

/// <summary>
/// Unit tests for RedisRateLimitingHealthCheck (Issue #5477).
/// Verifies health check reports correct status based on monitor state.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Issue", "5477")]
public sealed class RedisRateLimitingHealthCheckTests
{
    private static RedisRateLimitingHealthMonitor CreateMonitor(bool degraded = false)
    {
        var redisMock = new Mock<IConnectionMultiplexer>();
        var dbMock = new Mock<IDatabase>();
        redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(dbMock.Object);

        var config = new ConfigurationBuilder().Build();
        var monitor = new RedisRateLimitingHealthMonitor(
            redisMock.Object,
            new Mock<IServiceScopeFactory>().Object,
            config,
            new Mock<ILogger<RedisRateLimitingHealthMonitor>>().Object);

        if (degraded)
        {
            // Use reflection to set internal property with private setter
            var prop = typeof(RedisRateLimitingHealthMonitor)
                .GetProperty(
                    nameof(RedisRateLimitingHealthMonitor.IsRateLimitingDegraded),
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            prop!.GetSetMethod(nonPublic: true)!.Invoke(monitor, [true]);
        }

        return monitor;
    }

    [Fact]
    public async Task CheckHealthAsync_MonitorHealthy_ReturnsHealthy()
    {
        var monitor = CreateMonitor(degraded: false);
        var services = new List<IHostedService> { monitor };
        var healthCheck = new RedisRateLimitingHealthCheck(services);

        var result = await healthCheck.CheckHealthAsync(
            new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_MonitorDegraded_ReturnsDegraded()
    {
        var monitor = CreateMonitor(degraded: true);
        var services = new List<IHostedService> { monitor };
        var healthCheck = new RedisRateLimitingHealthCheck(services);

        var result = await healthCheck.CheckHealthAsync(
            new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("rate limiting is disabled", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckHealthAsync_NoMonitorRegistered_ReturnsHealthy()
    {
        var services = new List<IHostedService>(); // Empty — monitor not registered
        var healthCheck = new RedisRateLimitingHealthCheck(services);

        var result = await healthCheck.CheckHealthAsync(
            new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }
}
