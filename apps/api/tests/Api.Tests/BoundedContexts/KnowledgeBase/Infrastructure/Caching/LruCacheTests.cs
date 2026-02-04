using Api.BoundedContexts.KnowledgeBase.Infrastructure.Caching;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.Caching;

/// <summary>
/// ISSUE-3494: Unit tests for LRU cache component.
/// Tests cache promotion, TTL calculation, and eviction behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "3494")]
public class LruCacheTests : IDisposable
{
    private readonly FakeTimeProvider _timeProvider;
    private readonly LruCache<string, string> _cache;

    public LruCacheTests()
    {
        _timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        _cache = new LruCache<string, string>(5, _timeProvider);
    }

    public void Dispose()
    {
        _cache.Dispose();
    }

    [Fact]
    public void Set_And_Get_Should_Return_Value()
    {
        // Arrange
        const string key = "test-key";
        const string value = "test-value";

        // Act
        _cache.Set(key, value);
        var found = _cache.TryGet(key, out var result);

        // Assert
        found.Should().BeTrue();
        result.Should().Be(value);
    }

    [Fact]
    public void TryGet_NonExistentKey_Should_Return_False()
    {
        // Act
        var found = _cache.TryGet("non-existent", out var result);

        // Assert
        found.Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void Set_WithTtl_Should_Expire()
    {
        // Arrange
        const string key = "expiring-key";
        const string value = "expiring-value";
        var ttl = TimeSpan.FromMinutes(5);

        // Act
        _cache.Set(key, value, ttl);

        // Initially should be present
        _cache.TryGet(key, out _).Should().BeTrue();

        // Advance time past TTL
        _timeProvider.Advance(TimeSpan.FromMinutes(6));

        // Should be expired
        var foundAfterExpiry = _cache.TryGet(key, out var result);

        // Assert
        foundAfterExpiry.Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void Set_AtCapacity_Should_Evict_LeastRecentlyUsed()
    {
        // Arrange - capacity is 5
        _cache.Set("key1", "value1");
        _cache.Set("key2", "value2");
        _cache.Set("key3", "value3");
        _cache.Set("key4", "value4");
        _cache.Set("key5", "value5");

        // Access key1 to make it recently used
        _cache.TryGet("key1", out _);

        // Act - Add 6th item, should evict key2 (least recently used)
        _cache.Set("key6", "value6");

        // Assert
        _cache.TryGet("key1", out _).Should().BeTrue(); // Recently accessed
        _cache.TryGet("key2", out _).Should().BeFalse(); // Should be evicted
        _cache.TryGet("key6", out _).Should().BeTrue(); // Newly added
        _cache.Count.Should().Be(5);
    }

    [Fact]
    public void Get_Should_Update_AccessOrder()
    {
        // Arrange
        _cache.Set("key1", "value1");
        _cache.Set("key2", "value2");
        _cache.Set("key3", "value3");
        _cache.Set("key4", "value4");
        _cache.Set("key5", "value5");

        // Act - Access key1 multiple times to increase its access count
        _cache.TryGet("key1", out _);
        _cache.TryGet("key1", out _);
        _cache.TryGet("key1", out _);

        // Assert
        var accessCount = _cache.GetAccessCount("key1");
        accessCount.Should().BeGreaterThan(1);
    }

    [Fact]
    public void Remove_Should_Delete_Entry()
    {
        // Arrange
        const string key = "to-remove";
        _cache.Set(key, "value");

        // Act
        var removed = _cache.Remove(key);

        // Assert
        removed.Should().BeTrue();
        _cache.TryGet(key, out _).Should().BeFalse();
    }

    [Fact]
    public void Remove_NonExistentKey_Should_Return_False()
    {
        // Act
        var removed = _cache.Remove("non-existent");

        // Assert
        removed.Should().BeFalse();
    }

    [Fact]
    public void Clear_Should_Remove_All_Entries()
    {
        // Arrange
        _cache.Set("key1", "value1");
        _cache.Set("key2", "value2");
        _cache.Set("key3", "value3");

        // Act
        _cache.Clear();

        // Assert
        _cache.Count.Should().Be(0);
        _cache.TryGet("key1", out _).Should().BeFalse();
    }

    [Fact]
    public void RemoveExpired_Should_Clean_Expired_Entries()
    {
        // Arrange
        _cache.Set("short-ttl", "value1", TimeSpan.FromMinutes(1));
        _cache.Set("long-ttl", "value2", TimeSpan.FromHours(1));
        _cache.Set("no-ttl", "value3");

        // Advance time past short TTL but before long TTL
        _timeProvider.Advance(TimeSpan.FromMinutes(5));

        // Act
        var removed = _cache.RemoveExpired();

        // Assert
        removed.Should().Be(1);
        _cache.TryGet("short-ttl", out _).Should().BeFalse();
        _cache.TryGet("long-ttl", out _).Should().BeTrue();
        _cache.TryGet("no-ttl", out _).Should().BeTrue();
    }

    [Fact]
    public void GetStats_Should_Return_Correct_Metrics()
    {
        // Arrange
        _cache.Set("key1", "value1");
        _cache.Set("key2", "value2");

        // Generate hits and misses
        _cache.TryGet("key1", out _); // Hit
        _cache.TryGet("key2", out _); // Hit
        _cache.TryGet("non-existent", out _); // Miss

        // Act
        var stats = _cache.GetStats();

        // Assert
        stats.Hits.Should().Be(2);
        stats.Misses.Should().Be(1);
        stats.EntryCount.Should().Be(2);
        stats.MaxCapacity.Should().Be(5);
        stats.HitRatePercent.Should().BeApproximately(66.67, 0.1);
    }

    [Fact]
    public void Update_ExistingKey_Should_Update_Value()
    {
        // Arrange
        const string key = "key";
        _cache.Set(key, "original");

        // Act
        _cache.Set(key, "updated");
        _cache.TryGet(key, out var result);

        // Assert
        result.Should().Be("updated");
        _cache.Count.Should().Be(1);
    }

    [Fact]
    public void Constructor_WithZeroCapacity_Should_Throw()
    {
        // Act & Assert
        var act = () => new LruCache<string, string>(0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithNegativeCapacity_Should_Throw()
    {
        // Act & Assert
        var act = () => new LruCache<string, string>(-1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void MaxCapacity_Should_Return_Configured_Value()
    {
        // Assert
        _cache.MaxCapacity.Should().Be(5);
    }

    [Fact]
    public void LruEviction_Should_Evict_OldestUnaccessed()
    {
        // Arrange - Fill cache to capacity
        _cache.Set("a", "1");
        _cache.Set("b", "2");
        _cache.Set("c", "3");
        _cache.Set("d", "4");
        _cache.Set("e", "5");

        // Access in specific order: e, d, c, b (not a)
        _cache.TryGet("e", out _);
        _cache.TryGet("d", out _);
        _cache.TryGet("c", out _);
        _cache.TryGet("b", out _);

        // Act - Add new item, should evict "a"
        _cache.Set("f", "6");

        // Assert
        _cache.TryGet("a", out _).Should().BeFalse(); // Evicted (least recently used)
        _cache.TryGet("b", out _).Should().BeTrue();
        _cache.TryGet("c", out _).Should().BeTrue();
        _cache.TryGet("d", out _).Should().BeTrue();
        _cache.TryGet("e", out _).Should().BeTrue();
        _cache.TryGet("f", out _).Should().BeTrue();
    }
}
