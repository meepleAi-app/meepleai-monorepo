using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class UpdateSharedGameCommandValidatorTests
{
    private readonly UpdateSharedGameCommandValidator _validator;

    public UpdateSharedGameCommandValidatorTests()
    {
        _validator = new UpdateSharedGameCommandValidator();
    }

    [Fact]
    public void Validate_WithValidCommand_PassesValidation()
    {
        // Arrange
        var command = new UpdateSharedGameCommand(
            Guid.NewGuid(), "Catan", 1995, "Updated description", 3, 4, 90, 10,
            2.5m, 7.5m, "https://example.com/image.jpg", "https://example.com/thumb.jpg",
            null, Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyTitle_FailsValidation(string title)
    {
        // Arrange
        var command = new UpdateSharedGameCommand(
            Guid.NewGuid(), title, 1995, "Description", 3, 4, 90, 10,
            null, null, "https://img.jpg", "https://thumb.jpg", null, Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Validate_WithEmptyGameId_FailsValidation()
    {
        // Arrange
        var command = new UpdateSharedGameCommand(
            Guid.Empty, "Catan", 1995, "Description", 3, 4, 90, 10,
            null, null, "https://img.jpg", "https://thumb.jpg", null, Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.GameId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithInvalidMinPlayers_FailsValidation(int minPlayers)
    {
        // Arrange
        var command = new UpdateSharedGameCommand(
            Guid.NewGuid(), "Catan", 1995, "Description", minPlayers, 4, 90, 10,
            null, null, "https://img.jpg", "https://thumb.jpg", null, Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.MinPlayers);
    }

    [Fact]
    public void Validate_WithMaxPlayersLessThanMin_FailsValidation()
    {
        // Arrange
        var command = new UpdateSharedGameCommand(
            Guid.NewGuid(), "Catan", 1995, "Description", 5, 2, 90, 10,
            null, null, "https://img.jpg", "https://thumb.jpg", null, Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.MaxPlayers);
    }
}