using System;
using System.Threading.Tasks;
using Api.BoundedContexts.Authentication.Infrastructure;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Unit tests for RedisOAuthStateStore.
/// Tests OAuth state storage, validation, and expiration using Redis.
/// </summary>
public class RedisOAuthStateStoreTests
{
    private readonly Mock<IConnectionMultiplexer> _mockRedis;
    private readonly Mock<IDatabase> _mockDatabase;
    private readonly Mock<ILogger<RedisOAuthStateStore>> _mockLogger;
    private readonly RedisOAuthStateStore _stateStore;

    public RedisOAuthStateStoreTests()
    {
        _mockRedis = new Mock<IConnectionMultiplexer>();
        _mockDatabase = new Mock<IDatabase>();
        _mockLogger = new Mock<ILogger<RedisOAuthStateStore>>();

        _mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_mockDatabase.Object);

        _stateStore = new RedisOAuthStateStore(_mockRedis.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task StoreStateAsync_ValidState_StoresInRedisWithTTL()
    {
        // Arrange
        var state = "test-state-12345";
        var expiration = TimeSpan.FromMinutes(10);
        var expectedKey = $"meepleai:oauth:state:{state}";

        _mockDatabase.Setup(db => db.StringSetAsync(
                expectedKey,
                It.Is<RedisValue>(v => !v.IsNullOrEmpty), // Accept any non-empty value (DateTime string)
                expiration,
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        await _stateStore.StoreStateAsync(state, expiration);

        // Assert - Verify the call was made with correct key, TTL, and a timestamp value
        _mockDatabase.Verify(db => db.StringSetAsync(
            expectedKey,
            It.Is<RedisValue>(v => !v.IsNullOrEmpty),
            expiration,
            It.IsAny<When>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task StoreStateAsync_NullState_ThrowsArgumentException()
    {
        // Arrange
        var expiration = TimeSpan.FromMinutes(10);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _stateStore.StoreStateAsync(null!, expiration));
    }

    [Fact]
    public async Task StoreStateAsync_EmptyState_ThrowsArgumentException()
    {
        // Arrange
        var expiration = TimeSpan.FromMinutes(10);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _stateStore.StoreStateAsync(string.Empty, expiration));
    }

    [Fact]
    public async Task StoreStateAsync_NegativeExpiration_ThrowsArgumentException()
    {
        // Arrange
        var state = "test-state";
        var expiration = TimeSpan.FromMinutes(-1);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _stateStore.StoreStateAsync(state, expiration));
    }

    [Fact]
    public async Task ValidateAndRemoveStateAsync_ValidState_ReturnsTrue()
    {
        // Arrange
        var state = "valid-state-12345";
        var expectedKey = $"meepleai:oauth:state:{state}";

        _mockDatabase.Setup(db => db.KeyDeleteAsync(expectedKey, It.IsAny<CommandFlags>()))
            .ReturnsAsync(true); // Key existed and was deleted

        // Act
        var result = await _stateStore.ValidateAndRemoveStateAsync(state);

        // Assert
        Assert.True(result);
        _mockDatabase.Verify(db => db.KeyDeleteAsync(expectedKey, It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task ValidateAndRemoveStateAsync_InvalidState_ReturnsFalse()
    {
        // Arrange
        var state = "invalid-state-12345";
        var expectedKey = $"meepleai:oauth:state:{state}";

        _mockDatabase.Setup(db => db.KeyDeleteAsync(expectedKey, It.IsAny<CommandFlags>()))
            .ReturnsAsync(false); // Key didn't exist

        // Act
        var result = await _stateStore.ValidateAndRemoveStateAsync(state);

        // Assert
        Assert.False(result);
        _mockDatabase.Verify(db => db.KeyDeleteAsync(expectedKey, It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task ValidateAndRemoveStateAsync_NullState_ReturnsFalse()
    {
        // Act
        var result = await _stateStore.ValidateAndRemoveStateAsync(null!);

        // Assert
        Assert.False(result);
        _mockDatabase.Verify(db => db.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Never);
    }

    [Fact]
    public async Task ValidateAndRemoveStateAsync_EmptyState_ReturnsFalse()
    {
        // Act
        var result = await _stateStore.ValidateAndRemoveStateAsync(string.Empty);

        // Assert
        Assert.False(result);
        _mockDatabase.Verify(db => db.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Never);
    }

    [Fact]
    public async Task ExistsAsync_StateExists_ReturnsTrue()
    {
        // Arrange
        var state = "existing-state-12345";
        var expectedKey = $"meepleai:oauth:state:{state}";

        _mockDatabase.Setup(db => db.KeyExistsAsync(expectedKey, It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        var result = await _stateStore.ExistsAsync(state);

        // Assert
        Assert.True(result);
        _mockDatabase.Verify(db => db.KeyExistsAsync(expectedKey, It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task ExistsAsync_StateDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var state = "nonexistent-state-12345";
        var expectedKey = $"meepleai:oauth:state:{state}";

        _mockDatabase.Setup(db => db.KeyExistsAsync(expectedKey, It.IsAny<CommandFlags>()))
            .ReturnsAsync(false);

        // Act
        var result = await _stateStore.ExistsAsync(state);

        // Assert
        Assert.False(result);
        _mockDatabase.Verify(db => db.KeyExistsAsync(expectedKey, It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task ExistsAsync_NullState_ReturnsFalse()
    {
        // Act
        var result = await _stateStore.ExistsAsync(null!);

        // Assert
        Assert.False(result);
        _mockDatabase.Verify(db => db.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Never);
    }

    [Fact]
    public async Task CleanupExpiredStatesAsync_AlwaysReturnsZero()
    {
        // Act
        var result = await _stateStore.CleanupExpiredStatesAsync();

        // Assert
        Assert.Equal(0, result);
        // No Redis operations should be called (TTL handles cleanup)
        _mockDatabase.Verify(db => db.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Never);
    }

    [Fact]
    public async Task ValidateAndRemoveStateAsync_RedisException_ReturnsFalse()
    {
        // Arrange
        var state = "test-state";
        var expectedKey = $"meepleai:oauth:state:{state}";

        _mockDatabase.Setup(db => db.KeyDeleteAsync(expectedKey, It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisException("Connection failed"));

        // Act
        var result = await _stateStore.ValidateAndRemoveStateAsync(state);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ExistsAsync_RedisException_ReturnsFalse()
    {
        // Arrange
        var state = "test-state";
        var expectedKey = $"meepleai:oauth:state:{state}";

        _mockDatabase.Setup(db => db.KeyExistsAsync(expectedKey, It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisException("Connection failed"));

        // Act
        var result = await _stateStore.ExistsAsync(state);

        // Assert
        Assert.False(result);
    }
}
