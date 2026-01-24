using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Application.Validators;
using FluentAssertions;
using Xunit;

namespace Api.Tests.UserLibrary.Application.Validators;

public sealed class SendLoanReminderCommandValidatorTests
{
    private readonly SendLoanReminderCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        // Arrange
        var command = new SendLoanReminderCommand(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyUserId_Fails()
    {
        // Arrange
        var command = new SendLoanReminderCommand(Guid.Empty, Guid.NewGuid());

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "UserId");
    }

    [Fact]
    public void Validate_EmptyGameId_Fails()
    {
        // Arrange
        var command = new SendLoanReminderCommand(Guid.NewGuid(), Guid.Empty);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "GameId");
    }

    [Fact]
    public void Validate_MessageTooLong_Fails()
    {
        // Arrange
        var longMessage = new string('a', 501);
        var command = new SendLoanReminderCommand(Guid.NewGuid(), Guid.NewGuid(), longMessage);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "CustomMessage");
    }
}
