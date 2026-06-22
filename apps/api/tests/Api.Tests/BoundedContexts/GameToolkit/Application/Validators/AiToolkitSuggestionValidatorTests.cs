using Api.BoundedContexts.GameToolkit.Application.DTOs;
using Api.BoundedContexts.GameToolkit.Application.Validators;
using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.GameToolkit.Application.Validators;

/// <summary>
/// Validator tests for AiToolkitSuggestionValidator. Issue #1747 (B19-3c).
/// Covers the spike-identified anti-patterns (Points-as-counter, dummy timer,
/// missing Overrides, etc.).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameToolkit")]
public class AiToolkitSuggestionValidatorTests
{
    private readonly AiToolkitSuggestionValidator _validator = new();

    private static AiToolkitSuggestionDto ValidSuggestion(
        Action<List<AiCounterToolSuggestion>>? mutateCounters = null,
        Action<List<AiDiceToolSuggestion>>? mutateDice = null,
        Action<List<AiTimerToolSuggestion>>? mutateTimers = null,
        AiOverrideSuggestion? overrides = null,
        string reasoning = "Curated test fixture.")
    {
        var counters = new List<AiCounterToolSuggestion>();
        var dice = new List<AiDiceToolSuggestion>();
        var timers = new List<AiTimerToolSuggestion>();
        mutateCounters?.Invoke(counters);
        mutateDice?.Invoke(dice);
        mutateTimers?.Invoke(timers);

        return new AiToolkitSuggestionDto(
            ToolkitName: "Test Game",
            DiceTools: dice,
            CounterTools: counters,
            TimerTools: timers,
            ScoringTemplate: null,
            TurnTemplate: null,
            Overrides: overrides ?? new AiOverrideSuggestion(false, false, false),
            Reasoning: reasoning);
    }

    [Fact]
    public async Task ValidMinimal_PassesValidation()
    {
        var dto = ValidSuggestion();
        var result = await _validator.TestValidateAsync(dto);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task EmptyToolkitName_Fails()
    {
        var dto = ValidSuggestion() with { ToolkitName = string.Empty };
        var result = await _validator.TestValidateAsync(dto);
        result.ShouldHaveValidationErrorFor(s => s.ToolkitName);
    }

    [Fact]
    public async Task MissingOverrides_Fails()
    {
        var dto = ValidSuggestion(overrides: null!) with { Overrides = null };
        var result = await _validator.TestValidateAsync(dto);
        result.ShouldHaveValidationErrorFor(s => s.Overrides);
    }

    [Fact]
    public async Task EmptyReasoning_Fails()
    {
        var dto = ValidSuggestion(reasoning: string.Empty);
        var result = await _validator.TestValidateAsync(dto);
        result.ShouldHaveValidationErrorFor(s => s.Reasoning);
    }

    [Theory]
    [InlineData("Points")]
    [InlineData("Victory Points")]
    [InlineData("VP")]
    [InlineData("Score")]
    [InlineData("Player Score")]   // ends with " score"
    public async Task CounterNamedAsScore_Fails(string forbiddenName)
    {
        var dto = ValidSuggestion(mutateCounters: counters =>
        {
            counters.Add(new AiCounterToolSuggestion(
                Name: forbiddenName,
                MinValue: 0, MaxValue: 99, DefaultValue: 0,
                IsPerPlayer: true, Icon: null, Color: null));
        });
        var result = await _validator.TestValidateAsync(dto);
        result.Errors.Should().Contain(e => e.PropertyName.Contains("CounterTools", StringComparison.Ordinal)
                                           && e.ErrorMessage.Contains("end-game score", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Eggs")]
    [InlineData("Wood")]
    [InlineData("Action cubes")]
    [InlineData("Settlements")]
    public async Task LegitimateCounters_Pass(string legitName)
    {
        var dto = ValidSuggestion(mutateCounters: counters =>
        {
            counters.Add(new AiCounterToolSuggestion(
                Name: legitName,
                MinValue: 0, MaxValue: 30, DefaultValue: 0,
                IsPerPlayer: true, Icon: null, Color: null));
        });
        var result = await _validator.TestValidateAsync(dto);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task CounterMinGreaterThanMax_Fails()
    {
        var dto = ValidSuggestion(mutateCounters: counters =>
        {
            counters.Add(new AiCounterToolSuggestion(
                Name: "Wood", MinValue: 100, MaxValue: 50, DefaultValue: 50,
                IsPerPlayer: false, Icon: null, Color: null));
        });
        var result = await _validator.TestValidateAsync(dto);
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("MinValue must be <= MaxValue", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CounterDefaultOutOfBounds_Fails()
    {
        var dto = ValidSuggestion(mutateCounters: counters =>
        {
            counters.Add(new AiCounterToolSuggestion(
                Name: "Wood", MinValue: 0, MaxValue: 10, DefaultValue: 50,
                IsPerPlayer: false, Icon: null, Color: null));
        });
        var result = await _validator.TestValidateAsync(dto);
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("DefaultValue must be within", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DummyTimer_DurationZero_NonChess_Fails()
    {
        var dto = ValidSuggestion(mutateTimers: timers =>
        {
            timers.Add(new AiTimerToolSuggestion(
                Name: "Dummy",
                DurationSeconds: 0,
                TimerType: TimerType.CountDown,
                AutoStart: false, Color: null, IsPerPlayer: false,
                WarningThresholdSeconds: null));
        });
        var result = await _validator.TestValidateAsync(dto);
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("DurationSeconds must be > 0", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EmptyTimerArray_Pass()
    {
        var dto = ValidSuggestion();
        var result = await _validator.TestValidateAsync(dto);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task CustomDiceWithoutFaces_Fails()
    {
        var dto = ValidSuggestion(mutateDice: dice =>
        {
            dice.Add(new AiDiceToolSuggestion(
                Name: "Custom dice",
                DiceType: DiceType.Custom,
                Quantity: 5,
                CustomFaces: null,
                IsInteractive: true,
                Color: null));
        });
        var result = await _validator.TestValidateAsync(dto);
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("CustomFaces", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StandardDice_NoFaces_OK()
    {
        var dto = ValidSuggestion(mutateDice: dice =>
        {
            dice.Add(new AiDiceToolSuggestion(
                Name: "D6", DiceType: DiceType.D6, Quantity: 2,
                CustomFaces: null, IsInteractive: true, Color: null));
        });
        var result = await _validator.TestValidateAsync(dto);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task DiceZeroQuantity_Fails()
    {
        var dto = ValidSuggestion(mutateDice: dice =>
        {
            dice.Add(new AiDiceToolSuggestion(
                Name: "D6", DiceType: DiceType.D6, Quantity: 0,
                CustomFaces: null, IsInteractive: true, Color: null));
        });
        var result = await _validator.TestValidateAsync(dto);
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Quantity must be > 0", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExcludedToolMissingReason_Fails()
    {
        var dto = ValidSuggestion() with
        {
            ExcludedTools = new List<AiExcludedToolSuggestion>
            {
                new("Timer", string.Empty),
            },
        };
        var result = await _validator.TestValidateAsync(dto);
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Reason is required", StringComparison.Ordinal));
    }
}
