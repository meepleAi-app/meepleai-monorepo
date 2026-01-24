using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Application.Validators;
using FluentAssertions;
using Xunit;

namespace Api.Tests.UserLibrary.Application.Validators;

public sealed class RecordGameSessionCommandValidatorTests
{
    private readonly RecordGameSessionCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        // Arrange
        var command = new RecordGameSessionCommand(
            Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddHours(-2), 90);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_FuturePlayedAt_Fails()
    {
        // Arrange
        var command = new RecordGameSessionCommand(
            Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddDays(1), 90);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "PlayedAt");
    }

    [Fact]
    public void Validate_NegativeDuration_Fails()
    {
        // Arrange
        var command = new RecordGameSessionCommand(
            Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, -10);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DurationMinutes");
    }

    [Fact]
    public void Validate_DurationTooLong_Fails()
    {
        // Arrange
        var command = new RecordGameSessionCommand(
            Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, 1500); // > 24 hours

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
    }
}
