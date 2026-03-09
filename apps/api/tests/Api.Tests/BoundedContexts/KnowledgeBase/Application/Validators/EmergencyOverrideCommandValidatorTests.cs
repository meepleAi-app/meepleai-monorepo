using Api.BoundedContexts.KnowledgeBase.Application.Commands.EmergencyOverride;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Unit tests for ActivateEmergencyOverrideCommandValidator and
/// DeactivateEmergencyOverrideCommandValidator (Issue #5476).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "5476")]
public sealed class EmergencyOverrideCommandValidatorTests
{
    private readonly ActivateEmergencyOverrideCommandValidator _activateValidator = new();
    private readonly DeactivateEmergencyOverrideCommandValidator _deactivateValidator = new();

    // ─── ActivateEmergencyOverrideCommandValidator ──────────────────────────

    [Theory]
    [InlineData("force-ollama-only")]
    [InlineData("reset-circuit-breaker")]
    [InlineData("flush-quota-cache")]
    public void Activate_ValidAction_Passes(string action)
    {
        var command = new ActivateEmergencyOverrideCommand(
            action, 30, "Valid reason", Guid.NewGuid());

        var result = _activateValidator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid-action")]
    [InlineData("FORCE-OLLAMA-ONLY")] // Case-sensitive
    public void Activate_InvalidAction_Fails(string action)
    {
        var command = new ActivateEmergencyOverrideCommand(
            action, 30, "Valid reason", Guid.NewGuid());

        var result = _activateValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Action);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(720)]
    [InlineData(1440)]
    public void Activate_ValidDuration_Passes(int minutes)
    {
        var command = new ActivateEmergencyOverrideCommand(
            "force-ollama-only", minutes, "Valid reason", Guid.NewGuid());

        var result = _activateValidator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.DurationMinutes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1441)]
    public void Activate_InvalidDuration_Fails(int minutes)
    {
        var command = new ActivateEmergencyOverrideCommand(
            "force-ollama-only", minutes, "Valid reason", Guid.NewGuid());

        var result = _activateValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.DurationMinutes);
    }

    [Fact]
    public void Activate_EmptyReason_Fails()
    {
        var command = new ActivateEmergencyOverrideCommand(
            "force-ollama-only", 30, "", Guid.NewGuid());

        var result = _activateValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Reason);
    }

    [Fact]
    public void Activate_ReasonTooLong_Fails()
    {
        var command = new ActivateEmergencyOverrideCommand(
            "force-ollama-only", 30, new string('x', 501), Guid.NewGuid());

        var result = _activateValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Reason);
    }

    [Fact]
    public void Activate_EmptyAdminUserId_Fails()
    {
        var command = new ActivateEmergencyOverrideCommand(
            "force-ollama-only", 30, "Reason", Guid.Empty);

        var result = _activateValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.AdminUserId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Ollama")]
    [InlineData("OpenRouter")]
    public void Activate_ValidTargetProvider_Passes(string? provider)
    {
        var command = new ActivateEmergencyOverrideCommand(
            "force-ollama-only", 30, "Reason", Guid.NewGuid(), provider);

        var result = _activateValidator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.TargetProvider);
    }

    [Theory]
    [InlineData("InvalidProvider")]
    [InlineData("ollama")] // Case-sensitive
    public void Activate_InvalidTargetProvider_Fails(string provider)
    {
        var command = new ActivateEmergencyOverrideCommand(
            "force-ollama-only", 30, "Reason", Guid.NewGuid(), provider);

        var result = _activateValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.TargetProvider);
    }

    // ─── DeactivateEmergencyOverrideCommandValidator ────────────────────────

    [Theory]
    [InlineData("force-ollama-only")]
    [InlineData("reset-circuit-breaker")]
    [InlineData("flush-quota-cache")]
    public void Deactivate_ValidAction_Passes(string action)
    {
        var command = new DeactivateEmergencyOverrideCommand(action, Guid.NewGuid());

        var result = _deactivateValidator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    public void Deactivate_InvalidAction_Fails(string action)
    {
        var command = new DeactivateEmergencyOverrideCommand(action, Guid.NewGuid());

        var result = _deactivateValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Action);
    }

    [Fact]
    public void Deactivate_EmptyAdminUserId_Fails()
    {
        var command = new DeactivateEmergencyOverrideCommand(
            "force-ollama-only", Guid.Empty);

        var result = _deactivateValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.AdminUserId);
    }
}
