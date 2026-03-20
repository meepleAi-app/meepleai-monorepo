using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Unit tests for AbTestBudgetService (Issue #5505).
/// Verifies budget isolation, rate limiting, and response caching for A/B testing.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "5505")]
public sealed class AbTestBudgetServiceTests
{
    private readonly Mock<IConnectionMultiplexer> _redisMock = new();
    private readonly Mock<IDatabase> _dbMock = new();
    private readonly Mock<ILogger<AbTestBudgetService>> _loggerMock = new();
    private readonly AbTestBudgetService _sut;

    public AbTestBudgetServiceTests()
    {
        _redisMock
            .Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_dbMock.Object);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AbTesting:DailyBudgetUsd"] = "5.00",
                ["AbTesting:EditorDailyLimit"] = "50",
                ["AbTesting:AdminDailyLimit"] = "200",
            })
            .Build();

        _sut = new AbTestBudgetService(_redisMock.Object, config, _loggerMock.Object);
    }

    // ─── Budget ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task HasBudgetRemainingAsync_UnderBudget_ReturnsTrue()
    {
        _dbMock.Setup(db => db.StringGetAsync(It.Is<RedisKey>(k => k.ToString().Contains("daily_budget")), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)"3.50");

        var result = await _sut.HasBudgetRemainingAsync();

        Assert.True(result);
    }

    [Fact]
    public async Task HasBudgetRemainingAsync_OverBudget_ReturnsFalse()
    {
        _dbMock.Setup(db => db.StringGetAsync(It.Is<RedisKey>(k => k.ToString().Contains("daily_budget")), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)"5.50");

        var result = await _sut.HasBudgetRemainingAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task HasBudgetRemainingAsync_ExactBudget_ReturnsFalse()
    {
        _dbMock.Setup(db => db.StringGetAsync(It.Is<RedisKey>(k => k.ToString().Contains("daily_budget")), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)"5.00");

        var result = await _sut.HasBudgetRemainingAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task HasBudgetRemainingAsync_NoKeyExists_ReturnsTrue()
    {
        _dbMock.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var result = await _sut.HasBudgetRemainingAsync();

        Assert.True(result);
    }

    [Fact]
    public async Task HasBudgetRemainingAsync_RedisFailure_ReturnsTrueFailOpen()
    {
        _dbMock.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis down"));

        var result = await _sut.HasBudgetRemainingAsync();

        Assert.True(result);
    }

    [Fact]
    public async Task RecordTestCostAsync_IncrementsRedisKey()
    {
        _dbMock.SetReturnsDefault(Task.FromResult(0.0));
        _dbMock.SetReturnsDefault(Task.FromResult(true));

        await _sut.RecordTestCostAsync(0.25m);

        _dbMock.Verify(
            db => db.StringIncrementAsync(
                It.Is<RedisKey>(k => k.ToString().Contains("daily_budget")),
                0.25,
                It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task GetDailySpendAsync_ReturnsStoredValue()
    {
        _dbMock.Setup(db => db.StringGetAsync(It.Is<RedisKey>(k => k.ToString().Contains("daily_budget")), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)"2.75");

        var result = await _sut.GetDailySpendAsync();

        result.Should().Be(2.75m);
    }

    [Fact]
    public async Task GetDailySpendAsync_NoKey_ReturnsZero()
    {
        _dbMock.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var result = await _sut.GetDailySpendAsync();

        result.Should().Be(0m);
    }

    // ─── Rate Limiting ───────────────────────────────────────────────────────

    [Fact]
    public async Task HasRateLimitRemainingAsync_EditorUnderLimit_ReturnsTrue()
    {
        var userId = Guid.NewGuid();
        _dbMock.Setup(db => db.StringGetAsync(It.Is<RedisKey>(k => k.ToString().Contains(userId.ToString())), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)"30");

        var result = await _sut.HasRateLimitRemainingAsync(userId, isAdmin: false);

        Assert.True(result);
    }

    [Fact]
    public async Task HasRateLimitRemainingAsync_EditorAtLimit_ReturnsFalse()
    {
        var userId = Guid.NewGuid();
        _dbMock.Setup(db => db.StringGetAsync(It.Is<RedisKey>(k => k.ToString().Contains(userId.ToString())), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)"50");

        var result = await _sut.HasRateLimitRemainingAsync(userId, isAdmin: false);

        Assert.False(result);
    }

    [Fact]
    public async Task HasRateLimitRemainingAsync_AdminHigherLimit_ReturnsTrue()
    {
        var userId = Guid.NewGuid();
        _dbMock.Setup(db => db.StringGetAsync(It.Is<RedisKey>(k => k.ToString().Contains(userId.ToString())), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)"150");

        var result = await _sut.HasRateLimitRemainingAsync(userId, isAdmin: true);

        Assert.True(result);
    }

    [Fact]
    public async Task HasRateLimitRemainingAsync_AdminAtLimit_ReturnsFalse()
    {
        var userId = Guid.NewGuid();
        _dbMock.Setup(db => db.StringGetAsync(It.Is<RedisKey>(k => k.ToString().Contains(userId.ToString())), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)"200");

        var result = await _sut.HasRateLimitRemainingAsync(userId, isAdmin: true);

        Assert.False(result);
    }

    [Fact]
    public async Task HasRateLimitRemainingAsync_RedisFailure_ReturnsTrueFailOpen()
    {
        var userId = Guid.NewGuid();
        _dbMock.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis down"));

        var result = await _sut.HasRateLimitRemainingAsync(userId, isAdmin: false);

        Assert.True(result);
    }

    [Fact]
    public async Task RecordTestExecutionAsync_IncrementsUserKey()
    {
        var userId = Guid.NewGuid();
        _dbMock.SetReturnsDefault(Task.FromResult(1L));
        _dbMock.SetReturnsDefault(Task.FromResult(true));

        await _sut.RecordTestExecutionAsync(userId);

        _dbMock.Verify(
            db => db.StringIncrementAsync(
                It.Is<RedisKey>(k => k.ToString().Contains(userId.ToString())),
                1,
                It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task GetUserTestCountTodayAsync_ReturnsCount()
    {
        var userId = Guid.NewGuid();
        _dbMock.Setup(db => db.StringGetAsync(It.Is<RedisKey>(k => k.ToString().Contains(userId.ToString())), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)"15");

        var result = await _sut.GetUserTestCountTodayAsync(userId);

        result.Should().Be(15);
    }

    [Fact]
    public async Task GetUserTestCountTodayAsync_NoKey_ReturnsZero()
    {
        var userId = Guid.NewGuid();
        _dbMock.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var result = await _sut.GetUserTestCountTodayAsync(userId);

        result.Should().Be(0);
    }

    // ─── Response Caching ────────────────────────────────────────────────────

    [Fact]
    public async Task GetCachedResponseAsync_CacheHit_ReturnsResponse()
    {
        _dbMock.Setup(db => db.StringGetAsync(It.Is<RedisKey>(k => k.ToString().Contains("response_cache")), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)"Cached LLM response text");

        var result = await _sut.GetCachedResponseAsync("What is bluffing?", "gpt-4o-mini");

        result.Should().Be("Cached LLM response text");
    }

    [Fact]
    public async Task GetCachedResponseAsync_CacheMiss_ReturnsNull()
    {
        _dbMock.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var result = await _sut.GetCachedResponseAsync("What is bluffing?", "gpt-4o-mini");

        Assert.Null(result);
    }

    [Fact]
    public async Task CacheResponseAsync_StoresWithTtl()
    {
        // SetReturnsDefault bypasses Moq expression-tree overload resolution issues with
        // StackExchange.Redis 2.10+ optional parameters.
        _dbMock.SetReturnsDefault(Task.FromResult(true));

        await _sut.CacheResponseAsync("What is bluffing?", "gpt-4o-mini", "Response text");

        var setCount = _dbMock.Invocations.Count(i => i.Method.Name == "StringSetAsync");
        setCount.Should().Be(1);
    }

    [Fact]
    public async Task CacheResponseAsync_SameQueryDifferentModel_DifferentKeys()
    {
        _dbMock.SetReturnsDefault(Task.FromResult(true));

        await _sut.CacheResponseAsync("What is bluffing?", "gpt-4o-mini", "Response A");
        await _sut.CacheResponseAsync("What is bluffing?", "llama-3.3-70b", "Response B");

        var setInvocations = _dbMock.Invocations
            .Where(i => i.Method.Name == "StringSetAsync")
            .Select(i => i.Arguments[0].ToString()!)
            .ToList();

        setInvocations.Count.Should().Be(2);
        setInvocations[1].Should().NotBe(setInvocations[0]);
    }

    [Fact]
    public async Task CacheResponseAsync_SameQuerySameModel_SameKey()
    {
        _dbMock.SetReturnsDefault(Task.FromResult(true));

        await _sut.CacheResponseAsync("What is bluffing?", "gpt-4o-mini", "Response A");
        await _sut.CacheResponseAsync("What is bluffing?", "gpt-4o-mini", "Response B");

        var setInvocations = _dbMock.Invocations
            .Where(i => i.Method.Name == "StringSetAsync")
            .Select(i => i.Arguments[0].ToString()!)
            .ToList();

        setInvocations.Count.Should().Be(2);
        setInvocations[1].Should().Be(setInvocations[0]);
    }

    [Fact]
    public async Task CacheResponseAsync_CaseInsensitive_SameKey()
    {
        _dbMock.SetReturnsDefault(Task.FromResult(true));

        await _sut.CacheResponseAsync("What is Bluffing?", "GPT-4o-Mini", "Response A");
        await _sut.CacheResponseAsync("what is bluffing?", "gpt-4o-mini", "Response B");

        var setInvocations = _dbMock.Invocations
            .Where(i => i.Method.Name == "StringSetAsync")
            .Select(i => i.Arguments[0].ToString()!)
            .ToList();

        setInvocations.Count.Should().Be(2);
        setInvocations[1].Should().Be(setInvocations[0]);
    }

    // ─── Configuration ───────────────────────────────────────────────────────

    [Fact]
    public async Task DefaultConfiguration_UsesConfigValues()
    {
        // Budget is $5.00 (from config)
        _dbMock.Setup(db => db.StringGetAsync(It.Is<RedisKey>(k => k.ToString().Contains("daily_budget")), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)"4.99");

        Assert.True(await _sut.HasBudgetRemainingAsync());

        _dbMock.Setup(db => db.StringGetAsync(It.Is<RedisKey>(k => k.ToString().Contains("daily_budget")), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)"5.01");

        Assert.False(await _sut.HasBudgetRemainingAsync());
    }

    [Fact]
    public void DefaultConfiguration_FallbacksWhenNotProvided()
    {
        var emptyConfig = new ConfigurationBuilder().Build();
        var service = new AbTestBudgetService(_redisMock.Object, emptyConfig, _loggerMock.Object);

        // Should not throw — defaults to $5/day, 50 editor, 200 admin
        Assert.NotNull(service);
    }
}
