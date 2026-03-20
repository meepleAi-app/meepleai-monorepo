using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

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

        config.Id.Should().NotBe(Guid.Empty);
        config.CircuitBreakerFailureThreshold.Should().Be(5);
        config.CircuitBreakerOpenDurationSeconds.Should().Be(30);
        config.CircuitBreakerSuccessThreshold.Should().Be(3);
        config.DailyBudgetUsd.Should().Be(10.00m);
        config.MonthlyBudgetUsd.Should().Be(100.00m);
        config.FallbackChainJson.Should().Be("[\"Ollama\",\"OpenRouter\"]");
    }

    [Fact]
    public void UpdateCircuitBreakerSettings_ValidValues_UpdatesProperties()
    {
        var config = LlmSystemConfig.CreateDefault();
        var userId = Guid.NewGuid();

        config.UpdateCircuitBreakerSettings(10, 60, 5, userId);

        config.CircuitBreakerFailureThreshold.Should().Be(10);
        config.CircuitBreakerOpenDurationSeconds.Should().Be(60);
        config.CircuitBreakerSuccessThreshold.Should().Be(5);
        config.UpdatedAt.Should().NotBeNull();
        config.UpdatedByUserId.Should().Be(userId);
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

        ((Action)(() => config.UpdateCircuitBreakerSettings(failure, open, success))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateBudgetLimits_ValidValues_UpdatesProperties()
    {
        var config = LlmSystemConfig.CreateDefault();
        var userId = Guid.NewGuid();

        config.UpdateBudgetLimits(25.00m, 250.00m, userId);

        config.DailyBudgetUsd.Should().Be(25.00m);
        config.MonthlyBudgetUsd.Should().Be(250.00m);
        config.UpdatedAt.Should().NotBeNull();
        config.UpdatedByUserId.Should().Be(userId);
    }

    [Theory]
    [InlineData(-1, 100)]
    [InlineData(10, -1)]
    public void UpdateBudgetLimits_NegativeValues_ThrowsArgumentException(decimal daily, decimal monthly)
    {
        var config = LlmSystemConfig.CreateDefault();

        ((Action)(() => config.UpdateBudgetLimits(daily, monthly))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateBudgetLimits_ZeroValues_Succeeds()
    {
        var config = LlmSystemConfig.CreateDefault();

        config.UpdateBudgetLimits(0m, 0m);

        config.DailyBudgetUsd.Should().Be(0m);
        config.MonthlyBudgetUsd.Should().Be(0m);
    }

    [Fact]
    public void UpdateBudgetLimits_DailyExceedsMonthly_ThrowsArgumentException()
    {
        var config = LlmSystemConfig.CreateDefault();

        ((Action)(() => config.UpdateBudgetLimits(500.00m, 100.00m))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateFallbackChain_ValidJson_UpdatesProperty()
    {
        var config = LlmSystemConfig.CreateDefault();
        var newChain = "[\"OpenRouter\"]";

        config.UpdateFallbackChain(newChain);

        config.FallbackChainJson.Should().Be(newChain);
    }

    [Fact]
    public void UpdateFallbackChain_NullJson_ThrowsArgumentNullException()
    {
        var config = LlmSystemConfig.CreateDefault();

        ((Action)(() => config.UpdateFallbackChain(null!))).Should().Throw<ArgumentNullException>();
    }
}
