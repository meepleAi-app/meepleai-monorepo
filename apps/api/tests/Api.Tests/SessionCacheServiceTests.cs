using System;
using System.Threading.Tasks;
using Api.Models;
using Api.Services;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Api.Tests;

public class SessionCacheServiceTests
{
    [Fact]
    public async Task GetAsync_WhenCached_ReturnsSession()
    {
        // Arrange
        var mockRedis = new Mock<IConnectionMultiplexer>();
        var mockDatabase = new Mock<IDatabase>();
        var mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<SessionCacheService>>();

        var tokenHash = "test-hash";
        var user = new AuthUser("user-123", "test@example.com", "Test User", "User");
        var expiresAt = DateTime.UtcNow.AddDays(7);
        var lastSeenAt = DateTime.UtcNow;
        var session = new ActiveSession(user, expiresAt, lastSeenAt);

        var cachedJson = System.Text.Json.JsonSerializer.Serialize(session, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });

        mockDatabase
            .Setup(db => db.StringGetAsync($"session:{tokenHash}", It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)cachedJson);

        mockRedis
            .Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(mockDatabase.Object);

        var service = new SessionCacheService(mockRedis.Object, mockLogger.Object);

        // Act
        var result = await service.GetAsync(tokenHash);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.Id, result!.User.Id);
        Assert.Equal(user.Email, result.User.Email);
        Assert.Equal(expiresAt.ToString("O"), result.ExpiresAt.ToString("O"));
    }

    [Fact]
    public async Task GetAsync_WhenNotCached_ReturnsNull()
    {
        // Arrange
        var mockRedis = new Mock<IConnectionMultiplexer>();
        var mockDatabase = new Mock<IDatabase>();
        var mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<SessionCacheService>>();

        mockDatabase
            .Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        mockRedis
            .Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(mockDatabase.Object);

        var service = new SessionCacheService(mockRedis.Object, mockLogger.Object);

        // Act
        var result = await service.GetAsync("not-cached-hash");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_CachesSessionWithCorrectTTL()
    {
        // Arrange
        var mockRedis = new Mock<IConnectionMultiplexer>();
        var mockDatabase = new Mock<IDatabase>();
        var mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<SessionCacheService>>();

        var tokenHash = "test-hash";
        var user = new AuthUser("user-123", "test@example.com", "Test User", "User");
        var expiresAt = DateTime.UtcNow.AddDays(7);
        var lastSeenAt = DateTime.UtcNow;
        var session = new ActiveSession(user, expiresAt, lastSeenAt);

        TimeSpan? capturedTtl = null;
        mockDatabase
            .Setup(db => db.StringSetAsync(
                It.Is<RedisKey>(k => k == $"session:{tokenHash}"),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, TimeSpan?, bool, When, CommandFlags>((k, v, ttl, keepTtl, when, flags) =>
            {
                capturedTtl = ttl;
            })
            .ReturnsAsync(true);

        mockDatabase
            .Setup(db => db.SetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        mockDatabase
            .Setup(db => db.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        mockRedis
            .Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(mockDatabase.Object);

        var service = new SessionCacheService(mockRedis.Object, mockLogger.Object);

        // Act
        await service.SetAsync(tokenHash, session, expiresAt);

        // Assert
        Assert.NotNull(capturedTtl);
        // TTL should be close to 7 days (within 1 second tolerance)
        var expectedTtl = expiresAt - DateTime.UtcNow;
        Assert.InRange(capturedTtl!.Value.TotalSeconds, expectedTtl.TotalSeconds - 1, expectedTtl.TotalSeconds + 1);
    }

    [Fact]
    public async Task InvalidateAsync_RemovesSessionFromCache()
    {
        // Arrange
        var mockRedis = new Mock<IConnectionMultiplexer>();
        var mockDatabase = new Mock<IDatabase>();
        var mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<SessionCacheService>>();

        var tokenHash = "test-hash";
        bool deleteCalled = false;

        mockDatabase
            .Setup(db => db.KeyDeleteAsync(
                It.Is<RedisKey>(k => k == $"session:{tokenHash}"),
                It.IsAny<CommandFlags>()))
            .Callback(() => deleteCalled = true)
            .ReturnsAsync(true);

        mockRedis
            .Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(mockDatabase.Object);

        var service = new SessionCacheService(mockRedis.Object, mockLogger.Object);

        // Act
        await service.InvalidateAsync(tokenHash);

        // Assert
        Assert.True(deleteCalled);
    }

    [Fact]
    public async Task GetAsync_WhenRedisThrows_ReturnsNull()
    {
        // Arrange
        var mockRedis = new Mock<IConnectionMultiplexer>();
        var mockDatabase = new Mock<IDatabase>();
        var mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<SessionCacheService>>();

        mockDatabase
            .Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed"));

        mockRedis
            .Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(mockDatabase.Object);

        var service = new SessionCacheService(mockRedis.Object, mockLogger.Object);

        // Act
        var result = await service.GetAsync("test-hash");

        // Assert
        Assert.Null(result); // Should fail gracefully
    }
}
