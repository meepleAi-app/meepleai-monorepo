using Api.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;
using Api.BoundedContexts.UserLibrary.Application.Validators.PrivateGames;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Validators.PrivateGames;

/// <summary>
/// Unit tests for AddPrivateGameCommandValidator.
/// Issue #3670: Phase 9 - Testing &amp; Polish.
/// Tests all validation rules for adding private games.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public sealed class AddPrivateGameCommandValidatorTests
{
    private readonly AddPrivateGameCommandValidator _validator;

    public AddPrivateGameCommandValidatorTests()
    {
        _validator = new AddPrivateGameCommandValidator();
    }

    #region UserId Validation

    [Fact]
    public void UserId_Empty_HasValidationError()
    {
        // Arrange
        var command = new AddPrivateGameCommand(
            UserId: Guid.Empty,
            Source: "Manual",
            BggId: null,
            Title: "Test",
            MinPlayers: 2,
            MaxPlayers: 4);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserId)
            .WithErrorMessage("UserId is required");
    }

    [Fact]
    public void UserId_Valid_NoValidationError()
    {
        // Arrange
        var command = new AddPrivateGameCommand(
            UserId: Guid.NewGuid(),
            Source: "Manual",
            BggId: null,
            Title: "Test",
            MinPlayers: 2,
            MaxPlayers: 4);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.UserId);
    }

    #endregion

    #region Source Validation

    [Fact]
    public void Source_Empty_HasValidationError()
    {
        // Arrange
        var command = new AddPrivateGameCommand(
            UserId: Guid.NewGuid(),
            Source: "",
            BggId: null,
            Title: "Test",
            MinPlayers: 2,
            MaxPlayers: 4);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Source)
            .WithErrorMessage("Source is required");
    }

    [Fact]
    public void Source_Invalid_HasValidationError()
    {
        // Arrange
        var command = new AddPrivateGameCommand(
            UserId: Guid.NewGuid(),
            Source: "InvalidSource",
            BggId: null,
            Title: "Test",
            MinPlayers: 2,
            MaxPlayers: 4);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Source)
            .WithErrorMessage("Source must be 'Manual' or 'BoardGameGeek'");
    }

    [Theory]
    [InlineData("Manual")]
    [InlineData("manual")]
    [InlineData("MANUAL")]
    [InlineData("BoardGameGeek")]
    [InlineData("boardgamegeek")]
    [InlineData("BOARDGAMEGEEK")]
    public void Source_ValidVariations_NoValidationError(string source)
    {
        // Arrange
        var command = new AddPrivateGameCommand(
            UserId: Guid.NewGuid(),
            Source: source,
            BggId: source.Equals("BoardGameGeek", StringComparison.OrdinalIgnoreCase) ? 12345 : null,
            Title: "Test",
            MinPlayers: 2,
            MaxPlayers: 4);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Source);
    }

    #endregion

    #region BggId Validation

    [Fact]
    public void BggId_NullForBggSource_HasValidationError()
    {
        // Arrange
        var command = new AddPrivateGameCommand(
            UserId: Guid.NewGuid(),
            Source: "BoardGameGeek",
            BggId: null,
            Title: "Test",
            MinPlayers: 2,
            MaxPlayers: 4);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.BggId)
            .WithErrorMessage("BggId is required for BoardGameGeek source");
    }

    [Fact]
    public void BggId_ZeroForBggSource_HasValidationError()
    {
        // Arrange
        var command = new AddPrivateGameCommand(
            UserId: Guid.NewGuid(),
            Source: "BoardGameGeek",
            BggId: 0,
            Title: "Test",
            MinPlayers: 2,
            MaxPlayers: 4);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.BggId)
            .WithErrorMessage("BggId must be a positive integer");
    }

    [Fact]
    public void BggId_NegativeForBggSource_HasValidationError()
    {
        // Arrange
        var command = new AddPrivateGameCommand(
            UserId: Guid.NewGuid(),
            Source: "BoardGameGeek",
            BggId: -1,
            Title: "Test",
            MinPlayers: 2,
            MaxPlayers: 4);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.BggId);
    }

    [Fact]
    public void BggId_ValidForBggSource_NoValidationError()
    {
        // Arrange
        var command = new AddPrivateGameCommand(
            UserId: Guid.NewGuid(),
            Source: "BoardGameGeek",
            BggId: 12345,
            Title: "Test",
            MinPlayers: 2,
            MaxPlayers: 4);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.BggId);
    }

    [Fact]
    public void BggId_NullForManualSource_NoValidationError()
    {
        // Arrange
        var command = new AddPrivateGameCommand(
            UserId: Guid.NewGuid(),
            Source: "Manual",
            BggId: null,
            Title: "Test",
            MinPlayers: 2,
            MaxPlayers: 4);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.BggId);
    }

    #endregion

    #region Title Validation

    [Fact]
    public void Title_Empty_HasValidationError()
    {
        // Arrange
        var command = new AddPrivateGameCommand(
            UserId: Guid.NewGuid(),
            Source: "Manual",
            BggId: null,
            Title: "",
            MinPlayers: 2,
            MaxPlayers: 4);

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
        var command = new AddPrivateGameCommand(
            UserId: Guid.NewGuid(),
            Source: "Manual",
            BggId: null,
            Title: longTitle,
            MinPlayers: 2,
            MaxPlayers: 4);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage("Title cannot exceed 200 characters");
    }

    [Fact]
    public void Title_MaxLength_NoValidationError()
    {
        // Arrange
        var maxTitle = new string('A', 200);
        var command = new AddPrivateGameCommand(
            UserId: Guid.NewGuid(),
            Source: "Manual",
            BggId: null,
            Title: maxTitle,
            MinPlayers: 2,
            MaxPlayers: 4);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Title);
    }

    #endregion

    #region MinPlayers Validation

    [Fact]
    public void MinPlayers_Zero_HasValidationError()
    {
        // Arrange
        var command = new AddPrivateGameCommand(
            UserId: Guid.NewGuid(),
            Source: "Manual",
            BggId: null,
            Title: "Test",
            MinPlayers: 0,
            MaxPlayers: 4);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.MinPlayers)
            .WithErrorMessage("MinPlayers must be at least 1");
    }

    [Fact]
    public void MinPlayers_Negative_HasValidationError()
    {
        // Arrange
        var command = new AddPrivateGameCommand(
            UserId: Guid.NewGuid(),
            Source: "Manual",
            BggId: null,
            Title: "Test",
            MinPlayers: -1,
            MaxPlayers: 4);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.MinPlayers);
    }

    [Fact]
    public void MinPlayers_TooHigh_HasValidationError()
    {
        // Arrange
        var command = new AddPrivateGameCommand(
            UserId: Guid.NewGuid(),
            Source: "Manual",
            BggId: null,
            Title: "Test",
            MinPlayers: 101,
            MaxPlayers: 101);

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
        var command = new AddPrivateGameCommand(
            UserId: Guid.NewGuid(),
            Source: "Manual",
            BggId: null,
            Title: "Test",
            MinPlayers: 4,
            MaxPlayers: 2);

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
        var command = new AddPrivateGameCommand(
            UserId: Guid.NewGuid(),
            Source: "Manual",
            BggId: null,
            Title: "Test",
            MinPlayers: 1,
            MaxPlayers: 101);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.MaxPlayers)
            .WithErrorMessage("MaxPlayers cannot exceed 100");
    }

    [Fact]
    public void MaxPlayers_EqualToMinPlayers_NoValidationError()
    {
        // Arrange
        var command = new AddPrivateGameCommand(
            UserId: Guid.NewGuid(),
            Source: "Manual",
            BggId: null,
            Title: "Test",
            MinPlayers: 4,
            MaxPlayers: 4);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.MaxPlayers);
    }

    #endregion

    #region YearPublished Validation

    [Fact]
    public void YearPublished_TooEarly_HasValidationError()
    {
        // Arrange
        var command = new AddPrivateGameCommand(
            UserId: Guid.NewGuid(),
            Source: "Manual",
            BggId: null,
            Title: "Test",
            MinPlayers: 2,
            MaxPlayers: 4,
            YearPublished: 1899);

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
        var command = new AddPrivateGameCommand(
            UserId: Guid.NewGuid(),
            Source: "Manual",
            BggId: null,
            Title: "Test",
            MinPlayers: 2,
            MaxPlayers: 4,
            YearPublished: 2101);

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
        var command = new AddPrivateGameCommand(
            UserId: Guid.NewGuid(),
            Source: "Manual",
            BggId: null,
            Title: "Test",
            MinPlayers: 2,
            MaxPlayers: 4,
            YearPublished: null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.YearPublished);
    }

    [Theory]
    [InlineData(1901)]
    [InlineData(2024)]
    [InlineData(2100)]
    public void YearPublished_ValidRange_NoValidationError(int year)
    {
        // Arrange
        var command = new AddPrivateGameCommand(
            UserId: Guid.NewGuid(),
            Source: "Manual",
            BggId: null,
            Title: "Test",
            MinPlayers: 2,
            MaxPlayers: 4,
            YearPublished: year);

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
        var command = new AddPrivateGameCommand(
            UserId: Guid.NewGuid(),
            Source: "Manual",
            BggId: null,
            Title: "Test",
            MinPlayers: 2,
            MaxPlayers: 4,
            ComplexityRating: 0.5m);

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
        var command = new AddPrivateGameCommand(
            UserId: Guid.NewGuid(),
            Source: "Manual",
            BggId: null,
            Title: "Test",
            MinPlayers: 2,
            MaxPlayers: 4,
            ComplexityRating: 5.5m);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ComplexityRating);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.5)]
    [InlineData(3.7)]
    [InlineData(5.0)]
    public void ComplexityRating_ValidRange_NoValidationError(decimal rating)
    {
        // Arrange
        var command = new AddPrivateGameCommand(
            UserId: Guid.NewGuid(),
            Source: "Manual",
            BggId: null,
            Title: "Test",
            MinPlayers: 2,
            MaxPlayers: 4,
            ComplexityRating: rating);

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
        var command = new AddPrivateGameCommand(
            UserId: Guid.NewGuid(),
            Source: "Manual",
            BggId: null,
            Title: "Test",
            MinPlayers: 2,
            MaxPlayers: 4,
            PlayingTimeMinutes: 0);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PlayingTimeMinutes)
            .WithErrorMessage("PlayingTimeMinutes must be positive");
    }

    [Fact]
    public void PlayingTimeMinutes_Negative_HasValidationError()
    {
        // Arrange
        var command = new AddPrivateGameCommand(
            UserId: Guid.NewGuid(),
            Source: "Manual",
            BggId: null,
            Title: "Test",
            MinPlayers: 2,
            MaxPlayers: 4,
            PlayingTimeMinutes: -10);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PlayingTimeMinutes);
    }

    [Fact]
    public void PlayingTimeMinutes_Positive_NoValidationError()
    {
        // Arrange
        var command = new AddPrivateGameCommand(
            UserId: Guid.NewGuid(),
            Source: "Manual",
            BggId: null,
            Title: "Test",
            MinPlayers: 2,
            MaxPlayers: 4,
            PlayingTimeMinutes: 60);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.PlayingTimeMinutes);
    }

    #endregion

    #region MinAge Validation

    [Fact]
    public void MinAge_Negative_HasValidationError()
    {
        // Arrange
        var command = new AddPrivateGameCommand(
            UserId: Guid.NewGuid(),
            Source: "Manual",
            BggId: null,
            Title: "Test",
            MinPlayers: 2,
            MaxPlayers: 4,
            MinAge: -1);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.MinAge)
            .WithErrorMessage("MinAge cannot be negative");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(18)]
    public void MinAge_ValidValues_NoValidationError(int age)
    {
        // Arrange
        var command = new AddPrivateGameCommand(
            UserId: Guid.NewGuid(),
            Source: "Manual",
            BggId: null,
            Title: "Test",
            MinPlayers: 2,
            MaxPlayers: 4,
            MinAge: age);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.MinAge);
    }

    #endregion

    #region ImageUrl Validation

    [Fact]
    public void ImageUrl_TooLong_HasValidationError()
    {
        // Arrange
        var longUrl = "https://example.com/" + new string('a', 500);
        var command = new AddPrivateGameCommand(
            UserId: Guid.NewGuid(),
            Source: "Manual",
            BggId: null,
            Title: "Test",
            MinPlayers: 2,
            MaxPlayers: 4,
            ImageUrl: longUrl);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ImageUrl)
            .WithErrorMessage("ImageUrl cannot exceed 500 characters");
    }

    [Fact]
    public void ImageUrl_ValidLength_NoValidationError()
    {
        // Arrange
        var command = new AddPrivateGameCommand(
            UserId: Guid.NewGuid(),
            Source: "Manual",
            BggId: null,
            Title: "Test",
            MinPlayers: 2,
            MaxPlayers: 4,
            ImageUrl: "https://example.com/image.jpg");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.ImageUrl);
    }

    #endregion

    #region Description Validation

    [Fact]
    public void Description_TooLong_HasValidationError()
    {
        // Arrange
        var longDescription = new string('A', 2001);
        var command = new AddPrivateGameCommand(
            UserId: Guid.NewGuid(),
            Source: "Manual",
            BggId: null,
            Title: "Test",
            MinPlayers: 2,
            MaxPlayers: 4,
            Description: longDescription);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Description)
            .WithErrorMessage("Description cannot exceed 2000 characters");
    }

    #endregion

    #region Full Valid Command Tests

    [Fact]
    public void ValidManualCommand_NoValidationErrors()
    {
        // Arrange
        var command = new AddPrivateGameCommand(
            UserId: Guid.NewGuid(),
            Source: "Manual",
            BggId: null,
            Title: "Valid Game",
            MinPlayers: 2,
            MaxPlayers: 6,
            YearPublished: 2024,
            Description: "A valid description",
            PlayingTimeMinutes: 60,
            MinAge: 12,
            ComplexityRating: 3.0m,
            ImageUrl: "https://example.com/image.jpg");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ValidBggCommand_NoValidationErrors()
    {
        // Arrange
        var command = new AddPrivateGameCommand(
            UserId: Guid.NewGuid(),
            Source: "BoardGameGeek",
            BggId: 12345,
            Title: "Valid BGG Game",
            MinPlayers: 1,
            MaxPlayers: 4,
            YearPublished: 2020,
            Description: "A BGG game description",
            PlayingTimeMinutes: 120,
            MinAge: 14,
            ComplexityRating: 4.2m,
            ImageUrl: "https://cf.geekdo-images.com/image.jpg",
            ThumbnailUrl: "https://cf.geekdo-images.com/thumb.jpg");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion
}
