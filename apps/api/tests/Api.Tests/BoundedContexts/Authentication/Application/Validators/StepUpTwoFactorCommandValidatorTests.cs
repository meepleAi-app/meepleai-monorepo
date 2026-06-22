using Api.BoundedContexts.Authentication.Application.Commands.TwoFactor;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Validators;

/// <summary>
/// Unit tests for <see cref="StepUpTwoFactorCommandValidator"/> (SP5 Admin Security S3 — T5).
/// Format validation is intentionally minimal — the authoritative TOTP check (and constant-time
/// comparison) lives in TotpService — so these only assert the not-empty guards on
/// SessionId/ActorUserId/Code plus the Code length bound.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public sealed class StepUpTwoFactorCommandValidatorTests
{
    private readonly StepUpTwoFactorCommandValidator _validator = new();

    private static StepUpTwoFactorCommand ValidCommand() =>
        new(SessionId: Guid.NewGuid(), ActorUserId: Guid.NewGuid(), Code: "123456");

    [Fact]
    public void Should_Pass_When_Command_Is_Valid()
    {
        var result = _validator.TestValidate(ValidCommand());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Fail_When_SessionId_Is_Empty()
    {
        var command = ValidCommand() with { SessionId = Guid.Empty };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.SessionId);
    }

    [Fact]
    public void Should_Fail_When_ActorUserId_Is_Empty()
    {
        var command = ValidCommand() with { ActorUserId = Guid.Empty };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.ActorUserId);
    }

    [Fact]
    public void Should_Fail_When_Code_Is_Empty()
    {
        var command = ValidCommand() with { Code = string.Empty };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Code);
    }

    [Fact]
    public void Should_Fail_When_Code_Exceeds_Max_Length()
    {
        var command = ValidCommand() with { Code = new string('1', 11) };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Code);
    }

    [Fact]
    public void Should_Pass_When_Code_At_Max_Length()
    {
        var command = ValidCommand() with { Code = new string('1', 10) };
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.Code);
    }
}
