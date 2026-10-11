using Api.BoundedContexts.SystemConfiguration.Application.Commands.UpdateLlmSystemConfig;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Validators;

/// <summary>
/// #4169: la pagina «Budget e quote» manda solo tetti, soglia e quote. Le soglie del circuit breaker e
/// la catena di fallback sono opzionali; le soglie, se arrivano, arrivano tutte e tre.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
[Trait("Issue", "4169")]
public sealed class UpdateLlmSystemConfigValidatorTests
{
    private readonly UpdateLlmSystemConfigValidator _validator = new();

    private static UpdateLlmSystemConfigCommand BudgetOnly() =>
        new(DailyBudgetUsd: 1m, MonthlyBudgetUsd: 20m, UpdatedByUserId: Guid.NewGuid(),
            DegradeThresholdPercent: 80, BackgroundQuotaPercent: 30, EvaluationQuotaPercent: 20);

    [Fact]
    public void ACommandWithOnlyTheBudget_IsValid()
    {
        _validator.Validate(BudgetOnly()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void CircuitBreakerThresholds_SentPartially_AreRefused()
    {
        var result = _validator.Validate(BudgetOnly() with { CircuitBreakerFailureThreshold = 5 });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("all together", StringComparison.Ordinal));
    }

    [Fact]
    public void CircuitBreakerThresholds_SentTogether_AreStillValidated()
    {
        _validator.Validate(BudgetOnly() with
        {
            CircuitBreakerFailureThreshold = 5, CircuitBreakerOpenDurationSeconds = 30, CircuitBreakerSuccessThreshold = 2,
        }).IsValid.Should().BeTrue();

        _validator.Validate(BudgetOnly() with
        {
            CircuitBreakerFailureThreshold = 0, CircuitBreakerOpenDurationSeconds = 30, CircuitBreakerSuccessThreshold = 2,
        }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void AFallbackChain_IfSent_MustBeAJsonArray()
    {
        _validator.Validate(BudgetOnly() with { FallbackChainJson = "[\"OpenRouter\"]" }).IsValid.Should().BeTrue();
        _validator.Validate(BudgetOnly() with { FallbackChainJson = "not json" }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void QuotasOverOneHundredTogether_AreRefused()
    {
        _validator.Validate(BudgetOnly() with { BackgroundQuotaPercent = 60, EvaluationQuotaPercent = 50 })
            .IsValid.Should().BeFalse();
    }
}
