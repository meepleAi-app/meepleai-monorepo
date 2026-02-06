using Api.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;
using Api.BoundedContexts.UserLibrary.Application.Validators.PrivateGames;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Validators.PrivateGames;

/// <summary>
/// Unit tests for UpdatePrivateGameCommandValidator.
/// Issue #3670: Phase 9 - Testing &amp; Polish.
/// Tests all validation rules for updating private games.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public sealed class UpdatePrivateGameCommandValidatorTests
{
    private readonly UpdatePrivateGameCommandValidator _validator;

    public UpdatePrivateGameCommandValidatorTests()
    {
        _validator = new UpdatePrivateGameCommandValidator();
    }

    /// <summary>
    /// Helper method to create a valid command with all required parameters.
    /// </summary>
    private static UpdatePrivateGameCommand CreateValidCommand(
        Guid? privateGameId = null,
        Guid? userId = null,
        string title = "Test Game",
        int minPlayers = 2,
        int maxPlayers = 4,
        int? yearPublished = null,
        string? description = null,
        int? playingTimeMinutes = null,
        int? minAge = null,
        decimal? complexityRating = null,
        string? imageUrl = null)
    {
        return new UpdatePrivateGameCommand(
            PrivateGameId: privateGameId ?? Guid.NewGuid(),
            UserId: userId ?? Guid.NewGuid(),
            Title: title,
            MinPlayers: minPlayers,
            MaxPlayers: maxPlayers,
            YearPublished: yearPublished,
            Description: description,
            PlayingTimeMinutes: playingTimeMinutes,
            MinAge: minAge,
            ComplexityRating: complexityRating,
            ImageUrl: imageUrl);
    }

    #region PrivateGameId Validation

    [Fact]
    public void PrivateGameId_Empty_HasValidationError()
    {
        // Arrange
        var command = CreateValidCommand(privateGameId: Guid.Empty);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PrivateGameId)
            .WithErrorMessage("PrivateGameId is required");
    }

    [Fact]
    public void PrivateGameId_Valid_NoValidationError()
    {
        // Arrange
        var command = CreateValidCommand();

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.PrivateGameId);
    }

    #endregion

    #region UserId Validation

    [Fact]
    public void UserId_Empty_HasValidationError()
    {
        // Arrange
        var command = CreateValidCommand(userId: Guid.Empty);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserId)
            .WithErrorMessage("UserId is required");
    }

    #endregion

    #region Title Validation

    [Fact]
    public void Title_Empty_HasValidationError()
    {
        // Arrange
        var command = CreateValidCommand(title: "");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage("Title is required");
    }

    [Fact]
    public void Title_TooLong_HasValidationError()
    {
        // Arrange
        var longTitle = new string('A', 201);
        var command = CreateValidCommand(title: longTitle);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage("Title cannot exceed 200 characters");
    }

    #endregion

    #region MinPlayers Validation

    [Fact]
    public void MinPlayers_Zero_HasValidationError()
    {
        // Arrange
        var command = CreateValidCommand(minPlayers: 0);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.MinPlayers)
            .WithErrorMessage("MinPlayers must be at least 1");
    }

    [Fact]
    public void MinPlayers_TooHigh_HasValidationError()
    {
        // Arrange
        var command = CreateValidCommand(minPlayers: 101, maxPlayers: 101);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.MinPlayers)
            .WithErrorMessage("MinPlayers cannot exceed 100");
    }

    #endregion

    #region MaxPlayers Validation

    [Fact]
    public void MaxPlayers_LessThanMinPlayers_HasValidationError()
    {
        // Arrange
        var command = CreateValidCommand(minPlayers: 4, maxPlayers: 2);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.MaxPlayers)
            .WithErrorMessage("MaxPlayers must be greater than or equal to MinPlayers");
    }

    [Fact]
    public void MaxPlayers_TooHigh_HasValidationError()
    {
        // Arrange
        var command = CreateValidCommand(minPlayers: 1, maxPlayers: 101);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.MaxPlayers)
            .WithErrorMessage("MaxPlayers cannot exceed 100");
    }

    #endregion

    #region YearPublished Validation

    [Fact]
    public void YearPublished_TooEarly_HasValidationError()
    {
        // Arrange
        var command = CreateValidCommand(yearPublished: 1899);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.YearPublished)
            .WithErrorMessage("YearPublished must be after 1900");
    }

    [Fact]
    public void YearPublished_TooLate_HasValidationError()
    {
        // Arrange
        var command = CreateValidCommand(yearPublished: 2101);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.YearPublished)
            .WithErrorMessage("YearPublished cannot be after 2100");
    }

    [Fact]
    public void YearPublished_Null_NoValidationError()
    {
        // Arrange
        var command = CreateValidCommand(yearPublished: null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.YearPublished);
    }

    #endregion

    #region ComplexityRating Validation

    [Fact]
    public void ComplexityRating_TooLow_HasValidationError()
    {
        // Arrange
        var command = CreateValidCommand(complexityRating: 0.5m);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ComplexityRating)
            .WithErrorMessage("ComplexityRating must be between 1.0 and 5.0");
    }

    [Fact]
    public void ComplexityRating_TooHigh_HasValidationError()
    {
        // Arrange
        var command = CreateValidCommand(complexityRating: 5.5m);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ComplexityRating);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(3.0)]
    [InlineData(5.0)]
    public void ComplexityRating_ValidRange_NoValidationError(decimal rating)
    {
        // Arrange
        var command = CreateValidCommand(complexityRating: rating);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.ComplexityRating);
    }

    #endregion

    #region PlayingTimeMinutes Validation

    [Fact]
    public void PlayingTimeMinutes_Zero_HasValidationError()
    {
        // Arrange
        var command = CreateValidCommand(playingTimeMinutes: 0);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PlayingTimeMinutes)
            .WithErrorMessage("PlayingTimeMinutes must be positive");
    }

    #endregion

    #region MinAge Validation

    [Fact]
    public void MinAge_Negative_HasValidationError()
    {
        // Arrange
        var command = CreateValidCommand(minAge: -1);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.MinAge)
            .WithErrorMessage("MinAge cannot be negative");
    }

    #endregion

    #region ImageUrl Validation

    [Fact]
    public void ImageUrl_TooLong_HasValidationError()
    {
        // Arrange
        var longUrl = "https://example.com/" + new string('a', 500);
        var command = CreateValidCommand(imageUrl: longUrl);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ImageUrl)
            .WithErrorMessage("ImageUrl cannot exceed 500 characters");
    }

    #endregion

    #region Description Validation

    [Fact]
    public void Description_TooLong_HasValidationError()
    {
        // Arrange
        var longDescription = new string('A', 2001);
        var command = CreateValidCommand(description: longDescription);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Description)
            .WithErrorMessage("Description cannot exceed 2000 characters");
    }

    #endregion

    #region Full Valid Command Tests

    [Fact]
    public void ValidUpdateCommand_NoValidationErrors()
    {
        // Arrange
        var command = CreateValidCommand(
            title: "Updated Game",
            minPlayers: 2,
            maxPlayers: 6,
            yearPublished: 2024,
            description: "Updated description",
            playingTimeMinutes: 90,
            minAge: 12,
            complexityRating: 3.5m,
            imageUrl: "https://example.com/updated.jpg");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void MinimalValidUpdateCommand_NoValidationErrors()
    {
        // Arrange
        var command = CreateValidCommand(
            title: "Minimal Update",
            minPlayers: 1,
            maxPlayers: 1);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion
}
