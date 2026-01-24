using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Application.Validators;
using FluentAssertions;
using Xunit;

namespace Api.Tests.UserLibrary.Application.Validators;

public sealed class UpdateGameStateCommandValidatorTests
{
    private readonly UpdateGameStateCommandValidator _validator = new();

    [Theory]
    [InlineData("Nuovo")]
    [InlineData("InPrestito")]
    [InlineData("Wishlist")]
    [InlineData("Owned")]
    public void Validate_ValidState_Passes(string state)
    {
        // Arrange
        var command = new UpdateGameStateCommand(Guid.NewGuid(), Guid.NewGuid(), state);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyUserId_Fails()
    {
        // Arrange
        var command = new UpdateGameStateCommand(Guid.Empty, Guid.NewGuid(), "Owned");

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
        var command = new UpdateGameStateCommand(Guid.NewGuid(), Guid.Empty, "Owned");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "GameId");
    }

    [Fact]
    public void Validate_InvalidState_Fails()
    {
        // Arrange
        var command = new UpdateGameStateCommand(Guid.NewGuid(), Guid.NewGuid(), "InvalidState");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "NewState");
    }

    [Fact]
    public void Validate_StateNotesTooLong_Fails()
    {
        // Arrange
        var longNotes = new string('a', 501);
        var command = new UpdateGameStateCommand(Guid.NewGuid(), Guid.NewGuid(), "Owned", longNotes);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "StateNotes");
    }
}
