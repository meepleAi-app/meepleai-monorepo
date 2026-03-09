using Api.BoundedContexts.SystemConfiguration.Application.Commands.UpdateLlmSystemConfig;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Commands;

/// <summary>
/// Unit tests for UpdateLlmSystemConfigValidator (Issue #5495).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
[Trait("Issue", "5495")]
public sealed class UpdateLlmSystemConfigValidatorTests
{
    private readonly UpdateLlmSystemConfigValidator _validator = new();

    private static UpdateLlmSystemConfigCommand ValidCommand() => new(
        CircuitBreakerFailureThreshold: 5,
        CircuitBreakerOpenDurationSeconds: 30,
        CircuitBreakerSuccessThreshold: 3,
        DailyBudgetUsd: 10.00m,
        MonthlyBudgetUsd: 100.00m,
        FallbackChainJson: "[]",
        UpdatedByUserId: Guid.NewGuid());

    [Fact]
    public void ValidCommand_PassesValidation()
    {
        var result = _validator.TestValidate(ValidCommand());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void InvalidFailureThreshold_FailsValidation(int value)
    {
        var cmd = ValidCommand() with { CircuitBreakerFailureThreshold = value };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.CircuitBreakerFailureThreshold);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(3601)]
    public void InvalidOpenDuration_FailsValidation(int value)
    {
        var cmd = ValidCommand() with { CircuitBreakerOpenDurationSeconds = value };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.CircuitBreakerOpenDurationSeconds);
    }

    [Fact]
    public void NegativeDailyBudget_FailsValidation()
    {
        var cmd = ValidCommand() with { DailyBudgetUsd = -1m };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.DailyBudgetUsd);
    }

    [Fact]
    public void DailyExceedsMonthly_FailsValidation()
    {
        var cmd = ValidCommand() with { DailyBudgetUsd = 200m, MonthlyBudgetUsd = 100m };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.DailyBudgetUsd);
    }

    [Fact]
    public void EmptyFallbackChain_FailsValidation()
    {
        var cmd = ValidCommand() with { FallbackChainJson = "" };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.FallbackChainJson);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"key\": \"value\"}")]
    [InlineData("123")]
    public void InvalidJsonFallbackChain_FailsValidation(string value)
    {
        var cmd = ValidCommand() with { FallbackChainJson = value };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.FallbackChainJson);
    }

    [Fact]
    public void ValidJsonArrayFallbackChain_PassesValidation()
    {
        var cmd = ValidCommand() with { FallbackChainJson = "[\"OpenRouter\", \"Ollama\"]" };
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.FallbackChainJson);
    }

    [Fact]
    public void EmptyUserId_FailsValidation()
    {
        var cmd = ValidCommand() with { UpdatedByUserId = Guid.Empty };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.UpdatedByUserId);
    }
}
