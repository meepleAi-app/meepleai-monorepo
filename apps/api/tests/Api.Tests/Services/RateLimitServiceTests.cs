using System.Threading;
using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using FluentAssertions;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.Services;

/// <summary>
/// Tests for RateLimitService, focusing on Issue #1663: Dev/Test environment multiplier.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class RateLimitServiceTests
{
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    private readonly Mock<IConnectionMultiplexer> _mockRedis;
    private readonly Mock<ILogger<RateLimitService>> _mockLogger;
    private readonly Mock<IConfigurationService> _mockConfigService;
    private readonly IConfiguration _fallbackConfig;
    private readonly RateLimitConfiguration _config;
    private readonly TimeProvider _timeProvider;

    public RateLimitServiceTests()
    {
        _mockRedis = new Mock<IConnectionMultiplexer>();
        _mockLogger = new Mock<ILogger<RateLimitService>>();
        _mockConfigService = new Mock<IConfigurationService>();
        // Real in-memory configuration is more reliable than mocking IConfigurationSection chains.
        _fallbackConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:MaxTokens:user"] = "100",
                ["RateLimiting:MaxTokens:anonymous"] = "60",
                ["RateLimiting:RefillRate:user"] = "1.0",
                ["RateLimiting:RefillRate:anonymous"] = "1.0"
            })
            .Build();
        _timeProvider = TimeProvider.System;

        _config = new RateLimitConfiguration
        {
            Admin = new RoleLimitConfiguration { MaxTokens = 1000, RefillRate = 10.0 },
            Editor = new RoleLimitConfiguration { MaxTokens = 500, RefillRate = 5.0 },
            User = new RoleLimitConfiguration { MaxTokens = 100, RefillRate = 1.0 },
            Anonymous = new RoleLimitConfiguration { MaxTokens = 60, RefillRate = 1.0 }
        };
    }

    [Fact]
    public async Task GetConfigForRoleAsync_AppliesMultiplierInDevelopment_WhenUsingAppsettings()
    {
        // Arrange
        var mockEnvironment = CreateEnvironment("Development");

        // ConfigurationService returns null (so it falls back to appsettings)
        _mockConfigService.Setup(c => c.GetValueAsync<int?>(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string?>()))
            .ReturnsAsync((int?)null);

        var service = new RateLimitService(
            _mockRedis.Object,
            _mockLogger.Object,
            Options.Create(_config),
            mockEnvironment,
            _timeProvider,
            _mockConfigService.Object,
            _fallbackConfig);

        // Act
        var result = await service.GetConfigForRoleAsync("user", TestCancellationToken);

        // Assert
        // In Development, the multiplier should be applied: 100 * 10 = 1000
        result.MaxTokens.Should().Be(1000);
    }

    [Fact]
    public async Task GetConfigForRoleAsync_AppliesMultiplierInTest_WhenUsingAppsettings()
    {
        // Arrange
        var mockEnvironment = CreateEnvironment("Test");

        _mockConfigService.Setup(c => c.GetValueAsync<int?>(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string?>()))
            .ReturnsAsync((int?)null);

        var service = new RateLimitService(
            _mockRedis.Object,
            _mockLogger.Object,
            Options.Create(_config),
            mockEnvironment,
            _timeProvider,
            _mockConfigService.Object,
            _fallbackConfig);

        // Act
        var result = await service.GetConfigForRoleAsync("anonymous", TestCancellationToken);

        // Assert
        // In Test environment, multiplier should be applied: 60 * 10 = 600
        result.MaxTokens.Should().Be(600);
    }

    [Fact]
    public async Task GetConfigForRoleAsync_DoesNotApplyMultiplierInProduction()
    {
        // Arrange
        var mockEnvironment = CreateEnvironment("Production");

        _mockConfigService.Setup(c => c.GetValueAsync<int?>(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string?>()))
            .ReturnsAsync((int?)null);

        var service = new RateLimitService(
            _mockRedis.Object,
            _mockLogger.Object,
            Options.Create(_config),
            mockEnvironment,
            _timeProvider,
            _mockConfigService.Object,
            _fallbackConfig);

        // Act
        var result = await service.GetConfigForRoleAsync("user", TestCancellationToken);

        // Assert
        // In Production, NO multiplier: should stay 100
        result.MaxTokens.Should().Be(100);
    }

    [Fact]
    public async Task GetConfigForRoleAsync_AppliesMultiplierToDatabaseConfig()
    {
        // Arrange
        var mockEnvironment = CreateEnvironment("Development");

        // Mock database config returning base values
        _mockConfigService.Setup(c => c.GetValueAsync<int?>("RateLimit.MaxTokens.admin", It.IsAny<int?>(), It.IsAny<string?>()))
            .ReturnsAsync(1000);
        _mockConfigService.Setup(c => c.GetValueAsync<double?>("RateLimit.RefillRate.admin", It.IsAny<double?>(), It.IsAny<string?>()))
            .ReturnsAsync(10.0);

        var service = new RateLimitService(
            _mockRedis.Object,
            _mockLogger.Object,
            Options.Create(_config),
            mockEnvironment,
            _timeProvider,
            _mockConfigService.Object,
            _fallbackConfig);

        // Act
        var result = await service.GetConfigForRoleAsync("admin", TestCancellationToken);

        // Assert
        // DB config with multiplier: 1000 * 10 = 10000, 10.0 * 10 = 100.0
        result.MaxTokens.Should().Be(10000);
        result.RefillRate.Should().Be(100.0);
    }

    [Fact]
    public async Task GetConfigForRoleAsync_AppliesMultiplierToRefillRate()
    {
        // Arrange
        var mockEnvironment = CreateEnvironment("Development");

        _mockConfigService.Setup(c => c.GetValueAsync<int?>(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string?>()))
            .ReturnsAsync((int?)null);
        _mockConfigService.Setup(c => c.GetValueAsync<double?>(It.IsAny<string>(), It.IsAny<double?>(), It.IsAny<string?>()))
            .ReturnsAsync((double?)null);

        var service = new RateLimitService(
            _mockRedis.Object,
            _mockLogger.Object,
            Options.Create(_config),
            mockEnvironment,
            _timeProvider,
            _mockConfigService.Object,
            _fallbackConfig);

        // Act
        var result = await service.GetConfigForRoleAsync("user", TestCancellationToken);

        // Assert
        // Multiplier applied to both: 100 * 10 = 1000, 1.0 * 10 = 10.0
        result.MaxTokens.Should().Be(1000);
        result.RefillRate.Should().Be(10.0);
    }

    [Fact]
    public async Task GetConfigForRoleAsync_AllRoles_GetMultiplierInDevelopment()
    {
        // Arrange
        var mockEnvironment = CreateEnvironment("Development");

        _mockConfigService.Setup(c => c.GetValueAsync<int?>(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string?>()))
            .ReturnsAsync((int?)null);
        _mockConfigService.Setup(c => c.GetValueAsync<double?>(It.IsAny<string>(), It.IsAny<double?>(), It.IsAny<string?>()))
            .ReturnsAsync((double?)null);

        var service = new RateLimitService(
            _mockRedis.Object,
            _mockLogger.Object,
            Options.Create(_config),
            mockEnvironment,
            _timeProvider,
            _mockConfigService.Object,
            _fallbackConfig);

        // Act & Assert - Hardcoded defaults with multiplier
        var adminConfig = await service.GetConfigForRoleAsync("admin", TestCancellationToken);
        adminConfig.MaxTokens.Should().Be(10000);
        adminConfig.RefillRate.Should().Be(100.0);

        var editorConfig = await service.GetConfigForRoleAsync("editor", TestCancellationToken);
        editorConfig.MaxTokens.Should().Be(5000);
        editorConfig.RefillRate.Should().Be(50.0);

        var userConfig = await service.GetConfigForRoleAsync("user", TestCancellationToken);
        userConfig.MaxTokens.Should().Be(1000);
        userConfig.RefillRate.Should().Be(10.0);

        var anonymousConfig = await service.GetConfigForRoleAsync("anonymous", TestCancellationToken);
        anonymousConfig.MaxTokens.Should().Be(600);
        anonymousConfig.RefillRate.Should().Be(10.0);
    }

    [Fact]
    public async Task GetConfigForRoleAsync_WithoutConfigService_AppliesMultiplier()
    {
        // Arrange - Test backward compatibility path
        var mockEnvironment = CreateEnvironment("Development");

        var service = new RateLimitService(
            _mockRedis.Object,
            _mockLogger.Object,
            Options.Create(_config),
            mockEnvironment,
            _timeProvider,
            configService: null,  // No ConfigurationService
            fallbackConfig: null);

        // Act
        var result = await service.GetConfigForRoleAsync("user", TestCancellationToken);

        // Assert
        // Should use injected config with multiplier: 100 * 10 = 1000
        result.MaxTokens.Should().Be(1000);
        result.RefillRate.Should().Be(10.0);
    }

    [Fact]
    public async Task GetConfigForRoleAsync_K6Scenario_AnonymousUserGets600Tokens()
    {
        // Arrange - Simulate K6 performance test scenario
        var mockEnvironment = CreateEnvironment("Development");

        _mockConfigService.Setup(c => c.GetValueAsync<int?>(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string?>()))
            .ReturnsAsync((int?)null);

        var service = new RateLimitService(
            _mockRedis.Object,
            _mockLogger.Object,
            Options.Create(_config),
            mockEnvironment,
            _timeProvider,
            _mockConfigService.Object,
            _fallbackConfig);

        // Act
        var result = await service.GetConfigForRoleAsync("anonymous", TestCancellationToken);

        // Assert
        // K6 tests should now get 10x limits: 60 * 10 = 600
        result.MaxTokens.Should().Be(600);
        (result.MaxTokens >= 600).Should().BeTrue("K6 tests need at least 600 tokens to avoid 429s");
    }

    private static IWebHostEnvironment CreateEnvironment(string environmentName)
    {
        var mock = new Mock<IWebHostEnvironment>();
        mock.SetupGet(e => e.EnvironmentName).Returns(environmentName);
        // Allow IsDevelopment() extension to evaluate based on EnvironmentName.
        return mock.Object;
    }
}

