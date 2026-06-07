using Api.BoundedContexts.BusinessSimulations.Application.Commands.AppBudget;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.BusinessSimulations.Application.Commands.AppBudget;

/// <summary>
/// Unit tests for <see cref="UpsertAppBudgetCommandValidator"/> (Issue #1838 SP5 F4-C5).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "BusinessSimulations")]
[Trait("Issue", "1838")]
public sealed class UpsertAppBudgetCommandValidatorTests
{
    private readonly UpsertAppBudgetCommandValidator _validator = new();

    private static UpsertAppBudgetCommand ValidCommand() => new(
        MonthlyLimitAmount: 1200m,
        MonthlyLimitCurrency: "USD",
        AlertThresholdPct: 80,
        CriticalThresholdPct: 95,
        RowVersion: null,
        UpdatedBy: "admin@meepleai.dev");

    [Fact]
    public void ValidCommand_PassesValidation()
    {
        _validator.TestValidate(ValidCommand()).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Amount_ZeroOrNegative_FailsValidation(decimal amount)
    {
        var cmd = ValidCommand() with { MonthlyLimitAmount = amount };
        _validator.TestValidate(cmd)
            .ShouldHaveValidationErrorFor(c => c.MonthlyLimitAmount);
    }

    [Theory]
    [InlineData("EUR")]
    [InlineData("GBP")]
    [InlineData("JPY")]
    public void Currency_NonUsd_FailsValidation(string currency)
    {
        var cmd = ValidCommand() with { MonthlyLimitCurrency = currency };
        _validator.TestValidate(cmd)
            .ShouldHaveValidationErrorFor(c => c.MonthlyLimitCurrency);
    }

    [Theory]
    [InlineData("usd")]
    [InlineData("USD")]
    [InlineData("Usd")]
    public void Currency_UsdCaseInsensitive_PassesValidation(string currency)
    {
        var cmd = ValidCommand() with { MonthlyLimitCurrency = currency };
        _validator.TestValidate(cmd)
            .ShouldNotHaveValidationErrorFor(c => c.MonthlyLimitCurrency);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(-5)]
    public void AlertThreshold_OutOfRange_FailsValidation(int pct)
    {
        var cmd = ValidCommand() with { AlertThresholdPct = pct, CriticalThresholdPct = 99 };
        _validator.TestValidate(cmd)
            .ShouldHaveValidationErrorFor(c => c.AlertThresholdPct);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void CriticalThreshold_OutOfRange_FailsValidation(int pct)
    {
        var cmd = ValidCommand() with { CriticalThresholdPct = pct };
        _validator.TestValidate(cmd)
            .ShouldHaveValidationErrorFor(c => c.CriticalThresholdPct);
    }

    [Fact]
    public void CriticalThreshold_NotStrictlyAboveAlert_FailsValidation()
    {
        var cmd = ValidCommand() with { AlertThresholdPct = 80, CriticalThresholdPct = 80 };
        _validator.TestValidate(cmd)
            .ShouldHaveValidationErrorFor(c => c.CriticalThresholdPct);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void UpdatedBy_Empty_FailsValidation(string? updatedBy)
    {
        var cmd = ValidCommand() with { UpdatedBy = updatedBy! };
        _validator.TestValidate(cmd)
            .ShouldHaveValidationErrorFor(c => c.UpdatedBy);
    }

    [Fact]
    public void UpdatedBy_OverMaxLength_FailsValidation()
    {
        var cmd = ValidCommand() with { UpdatedBy = new string('x', 201) };
        _validator.TestValidate(cmd)
            .ShouldHaveValidationErrorFor(c => c.UpdatedBy);
    }
}
