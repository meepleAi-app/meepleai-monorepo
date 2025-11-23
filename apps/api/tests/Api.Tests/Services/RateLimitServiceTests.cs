using Api.Models;
using Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Api.Tests.Services;

/// <summary>
/// Tests for RateLimitService, focusing on Issue #1663: Dev/Test environment multiplier.
/// </summary>
public class RateLimitServiceTests
{
    private readonly Mock<IConnectionMultiplexer> _mockRedis;
    private readonly Mock<ILogger<RateLimitService>> _mockLogger;
    private readonly Mock<IConfigurationService> _mockConfigService;
    private readonly Mock<IConfiguration> _mockFallbackConfig;
    private readonly RateLimitConfiguration _config;
    private readonly TimeProvider _timeProvider;

    public RateLimitServiceTests()
    {
        _mockRedis = new Mock<IConnectionMultiplexer>();
        _mockLogger = new Mock<ILogger<RateLimitService>>();
        _mockConfigService = new Mock<IConfigurationService>();
        _mockFallbackConfig = new Mock<IConfiguration>();
        _timeProvider = TimeProvider.System;

        _config = new RateLimitConfiguration
        {
            Admin = new RateLimitConfig(1000, 10.0),
            Editor = new RateLimitConfig(500, 5.0),
            User = new RateLimitConfig(100, 1.0),
            Anonymous = new RateLimitConfig(60, 1.0)
        };
    }

    [Fact]
    public async Task GetConfigForRoleAsync_AppliesMultiplierInDevelopment_WhenUsingAppsettings()
    {
        // Arrange
        var mockEnvironment = new Mock<IWebHostEnvironment>();
        mockEnvironment.Setup(e => e.EnvironmentName).Returns("Development");

        // Mock appsettings config to return base values
        var mockConfigSection = new Mock<IConfigurationSection>();
        mockConfigSection.Setup(c => c.Value).Returns("100");
        _mockFallbackConfig.Setup(c => c.GetSection("RateLimiting:MaxTokens:user"))
            .Returns(mockConfigSection.Object);

        // ConfigurationService returns null (so it falls back to appsettings)
        _mockConfigService.Setup(c => c.GetValueAsync<int?>(It.IsAny<string>()))
            .ReturnsAsync((int?)null);

        var service = new RateLimitService(
            _mockRedis.Object,
            _mockLogger.Object,
            Options.Create(_config),
            mockEnvironment.Object,
            _timeProvider,
            _mockConfigService.Object,
            _mockFallbackConfig.Object);

        // Act
        var result = await service.GetConfigForRoleAsync("user");

        // Assert
        // In Development, the multiplier should be applied: 100 * 10 = 1000
        Assert.Equal(1000, result.MaxTokens);
    }

    [Fact]
    public async Task GetConfigForRoleAsync_AppliesMultiplierInTest_WhenUsingAppsettings()
    {
        // Arrange
        var mockEnvironment = new Mock<IWebHostEnvironment>();
        mockEnvironment.Setup(e => e.EnvironmentName).Returns("Test");
        mockEnvironment.Setup(e => e.IsDevelopment()).Returns(false);

        // Mock appsettings config
        var mockConfigSection = new Mock<IConfigurationSection>();
        mockConfigSection.Setup(c => c.Value).Returns("60");
        _mockFallbackConfig.Setup(c => c.GetSection("RateLimiting:MaxTokens:anonymous"))
            .Returns(mockConfigSection.Object);

        _mockConfigService.Setup(c => c.GetValueAsync<int?>(It.IsAny<string>()))
            .ReturnsAsync((int?)null);

        var service = new RateLimitService(
            _mockRedis.Object,
            _mockLogger.Object,
            Options.Create(_config),
            mockEnvironment.Object,
            _timeProvider,
            _mockConfigService.Object,
            _mockFallbackConfig.Object);

        // Act
        var result = await service.GetConfigForRoleAsync("anonymous");

        // Assert
        // In Test environment, multiplier should be applied: 60 * 10 = 600
        Assert.Equal(600, result.MaxTokens);
    }

    [Fact]
    public async Task GetConfigForRoleAsync_DoesNotApplyMultiplierInProduction()
    {
        // Arrange
        var mockEnvironment = new Mock<IWebHostEnvironment>();
        mockEnvironment.Setup(e => e.EnvironmentName).Returns("Production");
        mockEnvironment.Setup(e => e.IsDevelopment()).Returns(false);

        // Mock appsettings config
        var mockConfigSection = new Mock<IConfigurationSection>();
        mockConfigSection.Setup(c => c.Value).Returns("100");
        _mockFallbackConfig.Setup(c => c.GetSection("RateLimiting:MaxTokens:user"))
            .Returns(mockConfigSection.Object);

        _mockConfigService.Setup(c => c.GetValueAsync<int?>(It.IsAny<string>()))
            .ReturnsAsync((int?)null);

        var service = new RateLimitService(
            _mockRedis.Object,
            _mockLogger.Object,
            Options.Create(_config),
            mockEnvironment.Object,
            _timeProvider,
            _mockConfigService.Object,
            _mockFallbackConfig.Object);

        // Act
        var result = await service.GetConfigForRoleAsync("user");

        // Assert
        // In Production, NO multiplier: should stay 100
        Assert.Equal(100, result.MaxTokens);
    }

    [Fact]
    public async Task GetConfigForRoleAsync_AppliesMultiplierToDatabaseConfig()
    {
        // Arrange
        var mockEnvironment = new Mock<IWebHostEnvironment>();
        mockEnvironment.Setup(e => e.EnvironmentName).Returns("Development");

        // Mock database config returning base values
        _mockConfigService.Setup(c => c.GetValueAsync<int?>("RateLimit.MaxTokens.admin"))
            .ReturnsAsync(1000);
        _mockConfigService.Setup(c => c.GetValueAsync<double?>("RateLimit.RefillRate.admin"))
            .ReturnsAsync(10.0);

        var service = new RateLimitService(
            _mockRedis.Object,
            _mockLogger.Object,
            Options.Create(_config),
            mockEnvironment.Object,
            _timeProvider,
            _mockConfigService.Object,
            _mockFallbackConfig.Object);

        // Act
        var result = await service.GetConfigForRoleAsync("admin");

        // Assert
        // DB config with multiplier: 1000 * 10 = 10000, 10.0 * 10 = 100.0
        Assert.Equal(10000, result.MaxTokens);
        Assert.Equal(100.0, result.RefillRate);
    }

    [Fact]
    public async Task GetConfigForRoleAsync_AppliesMultiplierToRefillRate()
    {
        // Arrange
        var mockEnvironment = new Mock<IWebHostEnvironment>();
        mockEnvironment.Setup(e => e.EnvironmentName).Returns("Development");

        // Mock appsettings for refill rate
        var mockMaxTokensSection = new Mock<IConfigurationSection>();
        mockMaxTokensSection.Setup(c => c.Value).Returns("100");
        _mockFallbackConfig.Setup(c => c.GetSection("RateLimiting:MaxTokens:user"))
            .Returns(mockMaxTokensSection.Object);

        var mockRefillRateSection = new Mock<IConfigurationSection>();
        mockRefillRateSection.Setup(c => c.Value).Returns("1.0");
        _mockFallbackConfig.Setup(c => c.GetSection("RateLimiting:RefillRate:user"))
            .Returns(mockRefillRateSection.Object);

        _mockConfigService.Setup(c => c.GetValueAsync<int?>(It.IsAny<string>()))
            .ReturnsAsync((int?)null);
        _mockConfigService.Setup(c => c.GetValueAsync<double?>(It.IsAny<string>()))
            .ReturnsAsync((double?)null);

        var service = new RateLimitService(
            _mockRedis.Object,
            _mockLogger.Object,
            Options.Create(_config),
            mockEnvironment.Object,
            _timeProvider,
            _mockConfigService.Object,
            _mockFallbackConfig.Object);

        // Act
        var result = await service.GetConfigForRoleAsync("user");

        // Assert
        // Multiplier applied to both: 100 * 10 = 1000, 1.0 * 10 = 10.0
        Assert.Equal(1000, result.MaxTokens);
        Assert.Equal(10.0, result.RefillRate);
    }

    [Fact]
    public async Task GetConfigForRoleAsync_AllRoles_GetMultiplierInDevelopment()
    {
        // Arrange
        var mockEnvironment = new Mock<IWebHostEnvironment>();
        mockEnvironment.Setup(e => e.EnvironmentName).Returns("Development");

        _mockConfigService.Setup(c => c.GetValueAsync<int?>(It.IsAny<string>()))
            .ReturnsAsync((int?)null);
        _mockConfigService.Setup(c => c.GetValueAsync<double?>(It.IsAny<string>()))
            .ReturnsAsync((double?)null);

        var service = new RateLimitService(
            _mockRedis.Object,
            _mockLogger.Object,
            Options.Create(_config),
            mockEnvironment.Object,
            _timeProvider,
            _mockConfigService.Object,
            _mockFallbackConfig.Object);

        // Act & Assert - Hardcoded defaults with multiplier
        var adminConfig = await service.GetConfigForRoleAsync("admin");
        Assert.Equal(10000, adminConfig.MaxTokens); // 1000 * 10
        Assert.Equal(100.0, adminConfig.RefillRate); // 10.0 * 10

        var editorConfig = await service.GetConfigForRoleAsync("editor");
        Assert.Equal(5000, editorConfig.MaxTokens); // 500 * 10
        Assert.Equal(50.0, editorConfig.RefillRate); // 5.0 * 10

        var userConfig = await service.GetConfigForRoleAsync("user");
        Assert.Equal(1000, userConfig.MaxTokens); // 100 * 10
        Assert.Equal(10.0, userConfig.RefillRate); // 1.0 * 10

        var anonymousConfig = await service.GetConfigForRoleAsync("anonymous");
        Assert.Equal(600, anonymousConfig.MaxTokens); // 60 * 10
        Assert.Equal(10.0, anonymousConfig.RefillRate); // 1.0 * 10
    }

    [Fact]
    public async Task GetConfigForRoleAsync_WithoutConfigService_AppliesMultiplier()
    {
        // Arrange - Test backward compatibility path
        var mockEnvironment = new Mock<IWebHostEnvironment>();
        mockEnvironment.Setup(e => e.EnvironmentName).Returns("Development");

        var service = new RateLimitService(
            _mockRedis.Object,
            _mockLogger.Object,
            Options.Create(_config),
            mockEnvironment.Object,
            _timeProvider,
            configService: null,  // No ConfigurationService
            fallbackConfig: null);

        // Act
        var result = await service.GetConfigForRoleAsync("user");

        // Assert
        // Should use injected config with multiplier: 100 * 10 = 1000
        Assert.Equal(1000, result.MaxTokens);
        Assert.Equal(10.0, result.RefillRate);
    }

    [Fact]
    public async Task GetConfigForRoleAsync_K6Scenario_AnonymousUserGets600Tokens()
    {
        // Arrange - Simulate K6 performance test scenario
        var mockEnvironment = new Mock<IWebHostEnvironment>();
        mockEnvironment.Setup(e => e.EnvironmentName).Returns("Development");

        // Simulate appsettings.Development.json with base value 60
        var mockConfigSection = new Mock<IConfigurationSection>();
        mockConfigSection.Setup(c => c.Value).Returns("60");
        _mockFallbackConfig.Setup(c => c.GetSection("RateLimiting:MaxTokens:anonymous"))
            .Returns(mockConfigSection.Object);

        _mockConfigService.Setup(c => c.GetValueAsync<int?>(It.IsAny<string>()))
            .ReturnsAsync((int?)null);

        var service = new RateLimitService(
            _mockRedis.Object,
            _mockLogger.Object,
            Options.Create(_config),
            mockEnvironment.Object,
            _timeProvider,
            _mockConfigService.Object,
            _mockFallbackConfig.Object);

        // Act
        var result = await service.GetConfigForRoleAsync("anonymous");

        // Assert
        // K6 tests should now get 10x limits: 60 * 10 = 600
        Assert.Equal(600, result.MaxTokens);
        Assert.True(result.MaxTokens >= 600, "K6 tests need at least 600 tokens to avoid 429s");
    }
}
