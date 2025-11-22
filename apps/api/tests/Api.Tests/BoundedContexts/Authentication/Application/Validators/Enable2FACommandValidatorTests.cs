using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Application.Validators;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Validators;

/// <summary>
/// Unit tests for Enable2FACommandValidator.
/// Issue #1449: FluentValidation for Authentication CQRS pipeline
/// </summary>
public sealed class Enable2FACommandValidatorTests
{
    private readonly Enable2FACommandValidator _validator = new();

    [Fact]
    public void Should_Pass_When_All_Fields_Are_Valid()
    {
        // Arrange
        var command = new Enable2FACommand(
            UserId: Guid.NewGuid(),
            TotpCode: "123456"
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    #region UserId Validation

    [Fact]
    public void Should_Fail_When_UserId_Is_Empty()
    {
        // Arrange
        var command = new Enable2FACommand(
            UserId: Guid.Empty,
            TotpCode: "123456"
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserId)
            .WithErrorMessage("User ID is required");
    }

    #endregion

    #region TotpCode Validation

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_Fail_When_TotpCode_Is_Empty(string? totpCode)
    {
        // Arrange
        var command = new Enable2FACommand(
            UserId: Guid.NewGuid(),
            TotpCode: totpCode!
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.TotpCode)
            .WithErrorMessage("TOTP code is required");
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("1234")]
    [InlineData("123")]
    public void Should_Fail_When_TotpCode_Is_Too_Short(string totpCode)
    {
        // Arrange
        var command = new Enable2FACommand(
            UserId: Guid.NewGuid(),
            TotpCode: totpCode
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.TotpCode)
            .WithErrorMessage("TOTP code must be exactly 6 digits");
    }

    [Theory]
    [InlineData("1234567")]
    [InlineData("12345678")]
    public void Should_Fail_When_TotpCode_Is_Too_Long(string totpCode)
    {
        // Arrange
        var command = new Enable2FACommand(
            UserId: Guid.NewGuid(),
            TotpCode: totpCode
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.TotpCode)
            .WithErrorMessage("TOTP code must be exactly 6 digits");
    }

    [Theory]
    [InlineData("12345a")]
    [InlineData("abcdef")]
    [InlineData("123 56")]
    [InlineData("12-456")]
    public void Should_Fail_When_TotpCode_Contains_Non_Digits(string totpCode)
    {
        // Arrange
        var command = new Enable2FACommand(
            UserId: Guid.NewGuid(),
            TotpCode: totpCode
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.TotpCode)
            .WithErrorMessage("TOTP code must contain only digits");
    }

    [Theory]
    [InlineData("000000")]
    [InlineData("123456")]
    [InlineData("999999")]
    public void Should_Pass_When_TotpCode_Is_Valid_6_Digit_Number(string totpCode)
    {
        // Arrange
        var command = new Enable2FACommand(
            UserId: Guid.NewGuid(),
            TotpCode: totpCode
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion

    [Fact]
    public void Should_Fail_With_Multiple_Validation_Errors()
    {
        // Arrange
        var command = new Enable2FACommand(
            UserId: Guid.Empty,
            TotpCode: "invalid"
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserId);
        result.ShouldHaveValidationErrorFor(x => x.TotpCode);
    }
}
