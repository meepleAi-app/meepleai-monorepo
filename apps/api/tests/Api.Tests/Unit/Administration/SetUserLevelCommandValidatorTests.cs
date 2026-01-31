using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Validators;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Unit.Administration;

/// <summary>
/// Unit tests for SetUserLevelCommandValidator (Issue #3141).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
public sealed class SetUserLevelCommandValidatorTests
{
    private readonly SetUserLevelCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_Passes()
    {
        // Arrange
        var command = new SetUserLevelCommand(Guid.NewGuid(), 5);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyUserId_Fails()
    {
        // Arrange
        var command = new SetUserLevelCommand(Guid.Empty, 5);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "UserId");
    }

    [Fact]
    public void Validate_WithNegativeLevel_Fails()
    {
        // Arrange
        var command = new SetUserLevelCommand(Guid.NewGuid(), -1);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Level" && e.ErrorMessage.Contains(">= 0"));
    }

    [Fact]
    public void Validate_WithLevelAbove100_Fails()
    {
        // Arrange
        var command = new SetUserLevelCommand(Guid.NewGuid(), 101);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Level" && e.ErrorMessage.Contains("<= 100"));
    }

    [Fact]
    public void Validate_WithZeroLevel_Passes()
    {
        // Arrange
        var command = new SetUserLevelCommand(Guid.NewGuid(), 0);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithMaxLevel_Passes()
    {
        // Arrange
        var command = new SetUserLevelCommand(Guid.NewGuid(), 100);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
