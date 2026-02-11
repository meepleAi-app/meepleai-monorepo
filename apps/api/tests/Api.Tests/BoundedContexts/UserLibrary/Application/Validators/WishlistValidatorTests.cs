using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Application.Validators;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Validators;

/// <summary>
/// Tests for Wishlist command validators.
/// Issue #3917: Wishlist Management API.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public sealed class WishlistValidatorTests
{
    #region AddToWishlistCommandValidator Tests

    private readonly AddToWishlistCommandValidator _addValidator = new();

    [Fact]
    public void AddValidator_ValidCommand_PassesValidation()
    {
        // Arrange
        var command = new AddToWishlistCommand(Guid.NewGuid(), Guid.NewGuid(), "HIGH", 29.99m, "Notes");

        // Act
        var result = _addValidator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void AddValidator_EmptyUserId_Fails()
    {
        var command = new AddToWishlistCommand(Guid.Empty, Guid.NewGuid(), "HIGH");
        var result = _addValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.UserId)
            .WithErrorMessage("UserId is required");
    }

    [Fact]
    public void AddValidator_EmptyGameId_Fails()
    {
        var command = new AddToWishlistCommand(Guid.NewGuid(), Guid.Empty, "HIGH");
        var result = _addValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.GameId)
            .WithErrorMessage("GameId is required");
    }

    [Fact]
    public void AddValidator_EmptyPriority_Fails()
    {
        var command = new AddToWishlistCommand(Guid.NewGuid(), Guid.NewGuid(), "");
        var result = _addValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Priority)
            .WithErrorMessage("Priority is required");
    }

    [Theory]
    [InlineData("HIGH")]
    [InlineData("MEDIUM")]
    [InlineData("LOW")]
    [InlineData("high")]
    [InlineData("Medium")]
    [InlineData("low")]
    public void AddValidator_ValidPriority_Passes(string priority)
    {
        var command = new AddToWishlistCommand(Guid.NewGuid(), Guid.NewGuid(), priority);
        var result = _addValidator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.Priority);
    }

    [Theory]
    [InlineData("URGENT")]
    [InlineData("CRITICAL")]
    [InlineData("NONE")]
    public void AddValidator_InvalidPriority_Fails(string priority)
    {
        var command = new AddToWishlistCommand(Guid.NewGuid(), Guid.NewGuid(), priority);
        var result = _addValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Priority)
            .WithErrorMessage("Priority must be HIGH, MEDIUM, or LOW");
    }

    [Fact]
    public void AddValidator_ZeroTargetPrice_Fails()
    {
        var command = new AddToWishlistCommand(Guid.NewGuid(), Guid.NewGuid(), "HIGH", 0m);
        var result = _addValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.TargetPrice)
            .WithErrorMessage("TargetPrice must be greater than 0");
    }

    [Fact]
    public void AddValidator_NegativeTargetPrice_Fails()
    {
        var command = new AddToWishlistCommand(Guid.NewGuid(), Guid.NewGuid(), "HIGH", -5m);
        var result = _addValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.TargetPrice);
    }

    [Fact]
    public void AddValidator_NullTargetPrice_Passes()
    {
        var command = new AddToWishlistCommand(Guid.NewGuid(), Guid.NewGuid(), "HIGH", null);
        var result = _addValidator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.TargetPrice);
    }

    [Fact]
    public void AddValidator_ValidTargetPrice_Passes()
    {
        var command = new AddToWishlistCommand(Guid.NewGuid(), Guid.NewGuid(), "HIGH", 49.99m);
        var result = _addValidator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.TargetPrice);
    }

    [Fact]
    public void AddValidator_NotesExceed500_Fails()
    {
        var longNotes = new string('a', 501);
        var command = new AddToWishlistCommand(Guid.NewGuid(), Guid.NewGuid(), "HIGH", null, longNotes);
        var result = _addValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Notes)
            .WithErrorMessage("Notes cannot exceed 500 characters");
    }

    [Fact]
    public void AddValidator_Notes500Chars_Passes()
    {
        var notes = new string('a', 500);
        var command = new AddToWishlistCommand(Guid.NewGuid(), Guid.NewGuid(), "HIGH", null, notes);
        var result = _addValidator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.Notes);
    }

    [Fact]
    public void AddValidator_NullNotes_Passes()
    {
        var command = new AddToWishlistCommand(Guid.NewGuid(), Guid.NewGuid(), "HIGH", null, null);
        var result = _addValidator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.Notes);
    }

    #endregion

    #region UpdateWishlistItemCommandValidator Tests

    private readonly UpdateWishlistItemCommandValidator _updateValidator = new();

    [Fact]
    public void UpdateValidator_ValidCommand_PassesValidation()
    {
        var command = new UpdateWishlistItemCommand(Guid.NewGuid(), Guid.NewGuid(), "MEDIUM", 25m, false, "Updated", false);
        var result = _updateValidator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void UpdateValidator_EmptyUserId_Fails()
    {
        var command = new UpdateWishlistItemCommand(Guid.Empty, Guid.NewGuid(), "HIGH");
        var result = _updateValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.UserId)
            .WithErrorMessage("UserId is required");
    }

    [Fact]
    public void UpdateValidator_EmptyWishlistItemId_Fails()
    {
        var command = new UpdateWishlistItemCommand(Guid.NewGuid(), Guid.Empty, "HIGH");
        var result = _updateValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.WishlistItemId)
            .WithErrorMessage("WishlistItemId is required");
    }

    [Theory]
    [InlineData("URGENT")]
    [InlineData("CRITICAL")]
    public void UpdateValidator_InvalidPriority_Fails(string priority)
    {
        var command = new UpdateWishlistItemCommand(Guid.NewGuid(), Guid.NewGuid(), priority);
        var result = _updateValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Priority)
            .WithErrorMessage("Priority must be HIGH, MEDIUM, or LOW");
    }

    [Fact]
    public void UpdateValidator_NullPriority_Passes()
    {
        var command = new UpdateWishlistItemCommand(Guid.NewGuid(), Guid.NewGuid(), null);
        var result = _updateValidator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.Priority);
    }

    [Theory]
    [InlineData("HIGH")]
    [InlineData("low")]
    [InlineData("Medium")]
    public void UpdateValidator_ValidPriority_Passes(string priority)
    {
        var command = new UpdateWishlistItemCommand(Guid.NewGuid(), Guid.NewGuid(), priority);
        var result = _updateValidator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.Priority);
    }

    [Fact]
    public void UpdateValidator_ZeroTargetPrice_Fails()
    {
        var command = new UpdateWishlistItemCommand(Guid.NewGuid(), Guid.NewGuid(), null, 0m);
        var result = _updateValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.TargetPrice);
    }

    [Fact]
    public void UpdateValidator_NullTargetPrice_Passes()
    {
        var command = new UpdateWishlistItemCommand(Guid.NewGuid(), Guid.NewGuid(), null, null);
        var result = _updateValidator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.TargetPrice);
    }

    [Fact]
    public void UpdateValidator_ClearTargetPrice_SkipsValidation()
    {
        // When ClearTargetPrice is true, TargetPrice validation should not fire
        var command = new UpdateWishlistItemCommand(Guid.NewGuid(), Guid.NewGuid(), null, 0m, true);
        var result = _updateValidator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.TargetPrice);
    }

    [Fact]
    public void UpdateValidator_NotesExceed500_Fails()
    {
        var longNotes = new string('a', 501);
        var command = new UpdateWishlistItemCommand(Guid.NewGuid(), Guid.NewGuid(), null, null, false, longNotes);
        var result = _updateValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Notes);
    }

    [Fact]
    public void UpdateValidator_ClearNotes_SkipsValidation()
    {
        var longNotes = new string('a', 501);
        var command = new UpdateWishlistItemCommand(Guid.NewGuid(), Guid.NewGuid(), null, null, false, longNotes, true);
        var result = _updateValidator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.Notes);
    }

    #endregion

    #region RemoveFromWishlistCommandValidator Tests

    private readonly RemoveFromWishlistCommandValidator _removeValidator = new();

    [Fact]
    public void RemoveValidator_ValidCommand_PassesValidation()
    {
        var command = new RemoveFromWishlistCommand(Guid.NewGuid(), Guid.NewGuid());
        var result = _removeValidator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void RemoveValidator_EmptyUserId_Fails()
    {
        var command = new RemoveFromWishlistCommand(Guid.Empty, Guid.NewGuid());
        var result = _removeValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.UserId)
            .WithErrorMessage("UserId is required");
    }

    [Fact]
    public void RemoveValidator_EmptyWishlistItemId_Fails()
    {
        var command = new RemoveFromWishlistCommand(Guid.NewGuid(), Guid.Empty);
        var result = _removeValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.WishlistItemId)
            .WithErrorMessage("WishlistItemId is required");
    }

    #endregion
}
