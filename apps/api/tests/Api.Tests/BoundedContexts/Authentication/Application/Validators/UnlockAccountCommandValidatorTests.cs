using Api.BoundedContexts.Authentication.Application.Commands.AccountLockout;
using Api.BoundedContexts.Authentication.Application.Validators;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Validators;

/// <summary>
/// Unit tests for <see cref="UnlockAccountCommandValidator"/>.
/// Issue #3676: Account lockout after failed login attempts.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class UnlockAccountCommandValidatorTests
{
    private readonly UnlockAccountCommandValidator _validator = new();

    [Fact]
    public void Should_Pass_When_Valid_UserIdAndAdminId()
    {
        // Arrange
        var command = new UnlockAccountCommand(
            UserId: Guid.NewGuid(),
            AdminId: Guid.NewGuid()
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Fail_When_UserId_Is_Empty()
    {
        // Arrange
        var command = new UnlockAccountCommand(
            UserId: Guid.Empty,
            AdminId: Guid.NewGuid()
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserId)
            .WithErrorMessage("UserId is required");
    }

    [Fact]
    public void Should_Fail_When_AdminId_Is_Empty()
    {
        // Arrange
        var command = new UnlockAccountCommand(
            UserId: Guid.NewGuid(),
            AdminId: Guid.Empty
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.AdminId)
            .WithErrorMessage("AdminId is required");
    }

    [Fact]
    public void Should_Fail_When_Both_Ids_Are_Empty()
    {
        // Arrange
        var command = new UnlockAccountCommand(
            UserId: Guid.Empty,
            AdminId: Guid.Empty
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserId);
        result.ShouldHaveValidationErrorFor(x => x.AdminId);
    }

    [Fact]
    public void Should_Pass_When_Same_User_Is_Admin()
    {
        // Arrange - Same ID for user and admin (admin unlocking self, edge case)
        var sameId = Guid.NewGuid();
        var command = new UnlockAccountCommand(
            UserId: sameId,
            AdminId: sameId
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert - Validator doesn't check business logic, just required fields
        result.ShouldNotHaveAnyValidationErrors();
    }
}