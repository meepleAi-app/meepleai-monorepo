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
}