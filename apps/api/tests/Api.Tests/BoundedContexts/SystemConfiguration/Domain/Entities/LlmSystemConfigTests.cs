using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Domain.Entities;

/// <summary>
/// Unit tests for LlmSystemConfig domain entity (Issue #5498).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
[Trait("Issue", "5498")]
public sealed class LlmSystemConfigTests
{
    [Fact]
    public void CreateDefault_ReturnsConfigWithExpectedDefaults()
    {
        var config = LlmSystemConfig.CreateDefault();

        Assert.NotEqual(Guid.Empty, config.Id);
        Assert.Equal(5, config.CircuitBreakerFailureThreshold);
        Assert.Equal(30, config.CircuitBreakerOpenDurationSeconds);
        Assert.Equal(3, config.CircuitBreakerSuccessThreshold);
        Assert.Equal(10.00m, config.DailyBudgetUsd);
        Assert.Equal(100.00m, config.MonthlyBudgetUsd);
        Assert.Equal("[\"Ollama\",\"OpenRouter\"]", config.FallbackChainJson);
    }

    [Fact]
    public void UpdateCircuitBreakerSettings_ValidValues_UpdatesProperties()
    {
        var config = LlmSystemConfig.CreateDefault();
        var userId = Guid.NewGuid();

        config.UpdateCircuitBreakerSettings(10, 60, 5, userId);

        Assert.Equal(10, config.CircuitBreakerFailureThreshold);
        Assert.Equal(60, config.CircuitBreakerOpenDurationSeconds);
        Assert.Equal(5, config.CircuitBreakerSuccessThreshold);
        Assert.NotNull(config.UpdatedAt);
        Assert.Equal(userId, config.UpdatedByUserId);
    }

    [Theory]
    [InlineData(0, 30, 3)]
    [InlineData(-1, 30, 3)]
    [InlineData(5, 0, 3)]
    [InlineData(5, -1, 3)]
    [InlineData(5, 30, 0)]
    [InlineData(5, 30, -1)]
    public void UpdateCircuitBreakerSettings_InvalidValues_ThrowsArgumentException(int failure, int open, int success)
    {
        var config = LlmSystemConfig.CreateDefault();

        Assert.Throws<ArgumentException>(() => config.UpdateCircuitBreakerSettings(failure, open, success));
    }

    [Fact]
    public void UpdateBudgetLimits_ValidValues_UpdatesProperties()
    {
        var config = LlmSystemConfig.CreateDefault();
        var userId = Guid.NewGuid();

        config.UpdateBudgetLimits(25.00m, 250.00m, userId);

        Assert.Equal(25.00m, config.DailyBudgetUsd);
        Assert.Equal(250.00m, config.MonthlyBudgetUsd);
        Assert.NotNull(config.UpdatedAt);
        Assert.Equal(userId, config.UpdatedByUserId);
    }

    [Theory]
    [InlineData(-1, 100)]
    [InlineData(10, -1)]
    public void UpdateBudgetLimits_NegativeValues_ThrowsArgumentException(decimal daily, decimal monthly)
    {
        var config = LlmSystemConfig.CreateDefault();

        Assert.Throws<ArgumentException>(() => config.UpdateBudgetLimits(daily, monthly));
    }

    [Fact]
    public void UpdateBudgetLimits_ZeroValues_Succeeds()
    {
        var config = LlmSystemConfig.CreateDefault();

        config.UpdateBudgetLimits(0m, 0m);

        Assert.Equal(0m, config.DailyBudgetUsd);
        Assert.Equal(0m, config.MonthlyBudgetUsd);
    }

    [Fact]
    public void UpdateBudgetLimits_DailyExceedsMonthly_ThrowsArgumentException()
    {
        var config = LlmSystemConfig.CreateDefault();

        Assert.Throws<ArgumentException>(() => config.UpdateBudgetLimits(500.00m, 100.00m));
    }

    [Fact]
    public void UpdateFallbackChain_ValidJson_UpdatesProperty()
    {
        var config = LlmSystemConfig.CreateDefault();
        var newChain = "[\"OpenRouter\"]";

        config.UpdateFallbackChain(newChain);

        Assert.Equal(newChain, config.FallbackChainJson);
    }

    [Fact]
    public void UpdateFallbackChain_NullJson_ThrowsArgumentNullException()
    {
        var config = LlmSystemConfig.CreateDefault();

        Assert.Throws<ArgumentNullException>(() => config.UpdateFallbackChain(null!));
    }
}
