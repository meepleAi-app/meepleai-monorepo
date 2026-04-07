using Api.BoundedContexts.Administration.Domain.Services;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Api.Tests.Unit.Administration.Infrastructure;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "Administration")]
public class ServiceCooldownRegistryTests
{
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly ServiceCooldownRegistry _registry;

    public ServiceCooldownRegistryTests()
    {
        _registry = new ServiceCooldownRegistry(_timeProvider);
    }

    [Fact]
    public void IsInCooldown_WhenNoRestart_ReturnsFalse()
    {
        var result = _registry.IsInCooldown("embedding", out var remaining);
        Assert.False(result);
        Assert.Equal(0, remaining);
    }

    [Fact]
    public void IsInCooldown_AfterRestart_ReturnsTrue()
    {
        _registry.RecordRestart("embedding");
        var result = _registry.IsInCooldown("embedding", out var remaining);
        Assert.True(result);
        Assert.True(remaining > 0);
        Assert.True(remaining <= 300);
    }

    [Fact]
    public void IsInCooldown_AfterCooldownExpires_ReturnsFalse()
    {
        _registry.RecordRestart("embedding");
        _timeProvider.Advance(TimeSpan.FromMinutes(6));
        var result = _registry.IsInCooldown("embedding", out var remaining);
        Assert.False(result);
        Assert.Equal(0, remaining);
    }

    [Fact]
    public void IsInCooldown_DifferentServices_Independent()
    {
        _registry.RecordRestart("embedding");
        var embeddingCooldown = _registry.IsInCooldown("embedding", out _);
        var rerankerCooldown = _registry.IsInCooldown("reranker", out _);
        Assert.True(embeddingCooldown);
        Assert.False(rerankerCooldown);
    }

    [Fact]
    public void RecordRestart_ResetsExistingCooldown()
    {
        _registry.RecordRestart("embedding");
        _timeProvider.Advance(TimeSpan.FromMinutes(3));
        _registry.RecordRestart("embedding");
        var result = _registry.IsInCooldown("embedding", out var remaining);
        Assert.True(result);
        Assert.True(remaining > 180);
    }
}
