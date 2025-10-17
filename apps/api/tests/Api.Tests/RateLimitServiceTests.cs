using Api.Models;
using Api.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Api.Tests;

public class RateLimitServiceTests
{
    [Fact]
    public async Task CheckRateLimit_AllowsRequestWhenUnderLimit()
    {
        // Arrange
        var mockRedis = CreateMockRedis(allowRequest: true, tokensRemaining: 99, retryAfter: 0);
        var logger = Mock.Of<ILogger<RateLimitService>>();
        var config = CreateDefaultRateLimitConfig();
        var service = new RateLimitService(mockRedis.Object, logger, config);

        // Act
        var result = await service.CheckRateLimitAsync("test-key", 100, 1.0);

        // Assert
        Assert.True(result.Allowed);
        Assert.Equal(99, result.TokensRemaining);
        Assert.Equal(0, result.RetryAfterSeconds);
    }

    [Fact]
    public async Task CheckRateLimit_DeniesRequestWhenOverLimit()
    {
        // Arrange
        var mockRedis = CreateMockRedis(allowRequest: false, tokensRemaining: 0, retryAfter: 5);
        var logger = Mock.Of<ILogger<RateLimitService>>();
        var config = CreateDefaultRateLimitConfig();
        var service = new RateLimitService(mockRedis.Object, logger, config);

        // Act
        var result = await service.CheckRateLimitAsync("test-key", 100, 1.0);

        // Assert
        Assert.False(result.Allowed);
        Assert.Equal(0, result.TokensRemaining);
        Assert.Equal(5, result.RetryAfterSeconds);
    }

    [Fact]
    public async Task CheckRateLimit_FailsOpenWhenRedisUnavailable()
    {
        // Arrange
        var mockDatabase = new Mock<IDatabase>();
        mockDatabase
            .Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed"));

        var mockRedis = new Mock<IConnectionMultiplexer>();
        mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDatabase.Object);

        var logger = Mock.Of<ILogger<RateLimitService>>();
        var config = CreateDefaultRateLimitConfig();
        var service = new RateLimitService(mockRedis.Object, logger, config);

        // Act
        var result = await service.CheckRateLimitAsync("test-key", 100, 1.0);

        // Assert - should fail-open and allow request
        Assert.True(result.Allowed);
    }

    [Theory]
    [InlineData("Admin", 1000, 10.0)]
    [InlineData("Editor", 500, 5.0)]
    [InlineData("User", 100, 1.0)]
    [InlineData(null, 60, 1.0)]
    [InlineData("Unknown", 60, 1.0)]
    public void GetConfigForRole_ReturnsCorrectLimits(string? role, int expectedTokens, double expectedRate)
    {
        // Arrange
        var mockRedis = Mock.Of<IConnectionMultiplexer>();
        var logger = Mock.Of<ILogger<RateLimitService>>();
        var config = CreateDefaultRateLimitConfig();
        var service = new RateLimitService(mockRedis, logger, config);

        // Act
        var result = service.GetConfigForRole(role);

        // Assert
        Assert.Equal(expectedTokens, result.MaxTokens);
        Assert.Equal(expectedRate, result.RefillRate);
    }

    private static Mock<IConnectionMultiplexer> CreateMockRedis(bool allowRequest, int tokensRemaining, int retryAfter)
    {
        var resultArray = new RedisResult[]
        {
            RedisResult.Create(allowRequest ? 1 : 0),
            RedisResult.Create(tokensRemaining),
            RedisResult.Create(retryAfter)
        };

        var mockDatabase = new Mock<IDatabase>();
        mockDatabase
            .Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(resultArray));

        var mockRedis = new Mock<IConnectionMultiplexer>();
        mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDatabase.Object);

        return mockRedis;
    }

    private static IOptions<RateLimitConfiguration> CreateDefaultRateLimitConfig()
    {
        var config = new RateLimitConfiguration
        {
            Admin = new RoleLimitConfiguration { MaxTokens = 1000, RefillRate = 10.0 },
            Editor = new RoleLimitConfiguration { MaxTokens = 500, RefillRate = 5.0 },
            User = new RoleLimitConfiguration { MaxTokens = 100, RefillRate = 1.0 },
            Anonymous = new RoleLimitConfiguration { MaxTokens = 60, RefillRate = 1.0 }
        };

        return Options.Create(config);
    }

    #region BDD Scenarios - TEST-02: Additional Coverage Tests

    /// <summary>
    /// BDD Scenario: Rate limit check logs warning when limit exceeded
    /// Given: Rate limit is exceeded for a key
    /// When: CheckRateLimitAsync is called
    /// Then: Warning is logged with key and retry-after information
    /// </summary>
    [Fact]
    public async Task CheckRateLimit_WhenLimitExceeded_LogsWarning()
    {
        // Arrange - Rate limit exceeded scenario
        var mockRedis = CreateMockRedis(allowRequest: false, tokensRemaining: 0, retryAfter: 10);
        var mockLogger = new Mock<ILogger<RateLimitService>>();
        var config = CreateDefaultRateLimitConfig();
        var service = new RateLimitService(mockRedis.Object, mockLogger.Object, config);

        // Act
        var result = await service.CheckRateLimitAsync("test-key-123", 100, 1.0);

        // Assert - Verify rate limit denied
        Assert.False(result.Allowed);
        Assert.Equal(10, result.RetryAfterSeconds);

        // Assert - Verify warning was logged
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains("Rate limit exceeded") &&
                    state.ToString()!.Contains("test-key-123") &&
                    state.ToString()!.Contains("10")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// BDD Scenario: Rate limit check logs error when Redis fails
    /// Given: Redis is unavailable
    /// When: CheckRateLimitAsync is called
    /// Then: Error is logged with exception details and request is allowed (fail-open)
    /// </summary>
    [Fact]
    public async Task CheckRateLimit_WhenRedisThrowsException_LogsErrorAndFailsOpen()
    {
        // Arrange - Redis connection failure
        var mockDatabase = new Mock<IDatabase>();
        var expectedException = new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis unavailable");
        mockDatabase
            .Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ThrowsAsync(expectedException);

        var mockRedis = new Mock<IConnectionMultiplexer>();
        mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDatabase.Object);

        var mockLogger = new Mock<ILogger<RateLimitService>>();
        var config = CreateDefaultRateLimitConfig();
        var service = new RateLimitService(mockRedis.Object, mockLogger.Object, config);

        // Act
        var result = await service.CheckRateLimitAsync("test-key", 100, 1.0);

        // Assert - Verify fail-open behavior
        Assert.True(result.Allowed);
        Assert.Equal(100, result.TokensRemaining); // Should return maxTokens
        Assert.Equal(0, result.RetryAfterSeconds);

        // Assert - Verify error was logged
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains("Rate limit check failed") &&
                    state.ToString()!.Contains("test-key") &&
                    state.ToString()!.Contains("fail-open")),
                expectedException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// BDD Scenario: GetConfigForRole handles case-insensitive role names
    /// Given: Role names with different casing (ADMIN, admin, AdMiN)
    /// When: GetConfigForRole is called
    /// Then: Correct configuration is returned regardless of case
    /// </summary>
    [Theory]
    [InlineData("ADMIN", 1000, 10.0)]
    [InlineData("admin", 1000, 10.0)]
    [InlineData("AdMiN", 1000, 10.0)]
    [InlineData("EDITOR", 500, 5.0)]
    [InlineData("editor", 500, 5.0)]
    [InlineData("USER", 100, 1.0)]
    [InlineData("user", 100, 1.0)]
    public void GetConfigForRole_WithDifferentCasing_ReturnsSameConfig(string role, int expectedTokens, double expectedRate)
    {
        // Arrange
        var mockRedis = Mock.Of<IConnectionMultiplexer>();
        var logger = Mock.Of<ILogger<RateLimitService>>();
        var config = CreateDefaultRateLimitConfig();
        var service = new RateLimitService(mockRedis, logger, config);

        // Act
        var result = service.GetConfigForRole(role);

        // Assert - Configuration should be case-insensitive
        Assert.Equal(expectedTokens, result.MaxTokens);
        Assert.Equal(expectedRate, result.RefillRate);
    }

    /// <summary>
    /// BDD Scenario: Rate limit check handles different Redis result with string values
    /// Given: Redis returns string representations of numbers
    /// When: Results are converted to integers
    /// Then: Conversion succeeds and correct values are returned
    /// </summary>
    [Fact]
    public async Task CheckRateLimit_WithStringRedisResults_ConvertsCorrectly()
    {
        // Arrange - Redis returns string values instead of integers
        var resultArray = new RedisResult[]
        {
            RedisResult.Create((RedisValue)"1"),  // allowed as string
            RedisResult.Create((RedisValue)"42"), // tokensRemaining as string
            RedisResult.Create((RedisValue)"0")   // retryAfter as string
        };

        var mockDatabase = new Mock<IDatabase>();
        mockDatabase
            .Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(resultArray));

        var mockRedis = new Mock<IConnectionMultiplexer>();
        mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDatabase.Object);

        var logger = Mock.Of<ILogger<RateLimitService>>();
        var config = CreateDefaultRateLimitConfig();
        var service = new RateLimitService(mockRedis.Object, logger, config);

        // Act
        var result = await service.CheckRateLimitAsync("test-key", 100, 1.0);

        // Assert - String values should be converted correctly
        Assert.True(result.Allowed);
        Assert.Equal(42, result.TokensRemaining);
        Assert.Equal(0, result.RetryAfterSeconds);
    }

    /// <summary>
    /// BDD Scenario: Rate limit check isolates different keys
    /// Given: Two different rate limit keys
    /// When: Both keys are rate limited independently
    /// Then: Each key has its own token bucket
    /// </summary>
    [Fact]
    public async Task CheckRateLimit_WithDifferentKeys_IsolatesRateLimits()
    {
        // Arrange - Two different keys with different states
        var mockDatabase = new Mock<IDatabase>();

        // Setup: key1 has tokens, key2 is rate limited
        mockDatabase
            .Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.Is<RedisKey[]>(keys => keys[0].ToString().Contains("key1")),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(new RedisResult[]
            {
                RedisResult.Create(1),  // allowed
                RedisResult.Create(50), // tokens remaining
                RedisResult.Create(0)   // no retry needed
            }));

        mockDatabase
            .Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.Is<RedisKey[]>(keys => keys[0].ToString().Contains("key2")),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(new RedisResult[]
            {
                RedisResult.Create(0),  // denied
                RedisResult.Create(0),  // no tokens
                RedisResult.Create(5)   // retry after 5s
            }));

        var mockRedis = new Mock<IConnectionMultiplexer>();
        mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDatabase.Object);

        var logger = Mock.Of<ILogger<RateLimitService>>();
        var config = CreateDefaultRateLimitConfig();
        var service = new RateLimitService(mockRedis.Object, logger, config);

        // Act - Check both keys
        var result1 = await service.CheckRateLimitAsync("user:key1", 100, 1.0);
        var result2 = await service.CheckRateLimitAsync("user:key2", 100, 1.0);

        // Assert - Keys have independent rate limits
        Assert.True(result1.Allowed);
        Assert.Equal(50, result1.TokensRemaining);

        Assert.False(result2.Allowed);
        Assert.Equal(0, result2.TokensRemaining);
        Assert.Equal(5, result2.RetryAfterSeconds);
    }

    /// <summary>
    /// BDD Scenario: GetConfigForRole with empty string returns anonymous config
    /// Given: Empty string role
    /// When: GetConfigForRole is called
    /// Then: Anonymous configuration is returned
    /// </summary>
    [Fact]
    public void GetConfigForRole_WithEmptyString_ReturnsAnonymousConfig()
    {
        // Arrange
        var mockRedis = Mock.Of<IConnectionMultiplexer>();
        var logger = Mock.Of<ILogger<RateLimitService>>();
        var config = CreateDefaultRateLimitConfig();
        var service = new RateLimitService(mockRedis, logger, config);

        // Act
        var result = service.GetConfigForRole("");

        // Assert - Empty string should be treated as anonymous
        Assert.Equal(60, result.MaxTokens);
        Assert.Equal(1.0, result.RefillRate);
    }

    #endregion
}