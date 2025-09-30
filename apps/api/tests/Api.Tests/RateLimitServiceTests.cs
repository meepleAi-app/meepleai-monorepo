using Api.Services;
using Microsoft.Extensions.Logging;
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
        var service = new RateLimitService(mockRedis.Object, logger);

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
        var service = new RateLimitService(mockRedis.Object, logger);

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
        var service = new RateLimitService(mockRedis.Object, logger);

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
        // Act
        var config = RateLimitService.GetConfigForRole(role);

        // Assert
        Assert.Equal(expectedTokens, config.MaxTokens);
        Assert.Equal(expectedRate, config.RefillRate);
    }

    private static Mock<IConnectionMultiplexer> CreateMockRedis(bool allowRequest, int tokensRemaining, int retryAfter)
    {
        var resultArray = new RedisValue[] { allowRequest ? 1 : 0, tokensRemaining, retryAfter };

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
}