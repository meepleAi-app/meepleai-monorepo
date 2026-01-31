using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Application.Validators;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Application.Validators;

/// <summary>
/// Unit tests for MarkAllNotificationsReadCommandValidator.
/// Issue #2153: Add input validation to notification commands
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class MarkAllNotificationsReadCommandValidatorTests
{
    private readonly MarkAllNotificationsReadCommandValidator _validator = new();

    [Fact]
    public void Should_Pass_When_Valid_UserId()
    {
        // Arrange
        var command = new MarkAllNotificationsReadCommand(
            UserId: Guid.NewGuid()
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
        var command = new MarkAllNotificationsReadCommand(
            UserId: Guid.Empty
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserId)
            .WithErrorMessage("UserId is required");
    }
}
