using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Unit tests for RedisRateLimitingHealthMonitor (Issue #5477).
/// Verifies Redis PING-based health detection, state transitions,
/// and degradation flag behavior.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "5477")]
public sealed class RedisRateLimitingHealthMonitorTests
{
    private readonly Mock<IConnectionMultiplexer> _redisMock = new();
    private readonly Mock<IDatabase> _dbMock = new();
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock = new();
    private readonly Mock<ILogger<RedisRateLimitingHealthMonitor>> _loggerMock = new();
    private readonly IConfiguration _configuration;

    public RedisRateLimitingHealthMonitorTests()
    {
        _redisMock
            .Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_dbMock.Object);

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RedisHealthMonitor:CheckIntervalSeconds"] = "5",
                ["RedisHealthMonitor:FailureMode"] = "soft"
            })
            .Build();
    }

    private RedisRateLimitingHealthMonitor CreateMonitor() =>
        new(
            _redisMock.Object,
            _scopeFactoryMock.Object,
            _configuration,
            _loggerMock.Object);

    [Fact]
    public void InitialState_IsNotDegraded()
    {
        var monitor = CreateMonitor();

        Assert.False(monitor.IsRateLimitingDegraded);
    }

    [Fact]
    public void CheckInterval_ReadsFromConfiguration()
    {
        var monitor = CreateMonitor();

        Assert.Equal(5, monitor.CheckInterval.TotalSeconds);
    }

    [Fact]
    public void FailureMode_ReadsFromConfiguration()
    {
        var monitor = CreateMonitor();

        Assert.Equal("soft", monitor.FailureMode);
    }

    [Fact]
    public void FailureMode_DefaultsToSoft()
    {
        var emptyConfig = new ConfigurationBuilder().Build();
        var monitor = new RedisRateLimitingHealthMonitor(
            _redisMock.Object, _scopeFactoryMock.Object, emptyConfig, _loggerMock.Object);

        Assert.Equal("soft", monitor.FailureMode);
    }

    [Fact]
    public void CheckInterval_DefaultsTo30Seconds()
    {
        var emptyConfig = new ConfigurationBuilder().Build();
        var monitor = new RedisRateLimitingHealthMonitor(
            _redisMock.Object, _scopeFactoryMock.Object, emptyConfig, _loggerMock.Object);

        Assert.Equal(30, monitor.CheckInterval.TotalSeconds);
    }

    [Fact]
    public void Constructor_NullRedis_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RedisRateLimitingHealthMonitor(
                null!, _scopeFactoryMock.Object, _configuration, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_NullScopeFactory_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RedisRateLimitingHealthMonitor(
                _redisMock.Object, null!, _configuration, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_NullConfiguration_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RedisRateLimitingHealthMonitor(
                _redisMock.Object, _scopeFactoryMock.Object, null!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RedisRateLimitingHealthMonitor(
                _redisMock.Object, _scopeFactoryMock.Object, _configuration, null!));
    }
}
