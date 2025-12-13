using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Application.Validators;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Application.Validators;

/// <summary>
/// Unit tests for MarkNotificationReadCommandValidator.
/// Issue #2153: Add input validation to notification commands
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class MarkNotificationReadCommandValidatorTests
{
    private readonly MarkNotificationReadCommandValidator _validator = new();

    [Fact]
    public void Should_Pass_When_Valid_NotificationId_And_UserId()
    {
        // Arrange
        var command = new MarkNotificationReadCommand(
            NotificationId: Guid.NewGuid(),
            UserId: Guid.NewGuid()
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Fail_When_NotificationId_Is_Empty()
    {
        // Arrange
        var command = new MarkNotificationReadCommand(
            NotificationId: Guid.Empty,
            UserId: Guid.NewGuid()
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.NotificationId)
            .WithErrorMessage("NotificationId is required");
    }

    [Fact]
    public void Should_Fail_When_UserId_Is_Empty()
    {
        // Arrange
        var command = new MarkNotificationReadCommand(
            NotificationId: Guid.NewGuid(),
            UserId: Guid.Empty
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserId)
            .WithErrorMessage("UserId is required");
    }

    [Fact]
    public void Should_Fail_When_Both_Ids_Are_Empty()
    {
        // Arrange
        var command = new MarkNotificationReadCommand(
            NotificationId: Guid.Empty,
            UserId: Guid.Empty
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.NotificationId);
        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }
}
