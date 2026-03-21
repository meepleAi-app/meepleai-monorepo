using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Validators;

/// <summary>
/// Unit tests for CreateSharedGameFromPdfCommandValidator.
/// Verifies validation rules for PDF game creation command.
/// Issue #4138: Backend - Commands and DTOs - PDF Wizard
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class CreateSharedGameFromPdfCommandValidatorTests
{
    private readonly CreateSharedGameFromPdfCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_ShouldNotHaveErrors()
    {
        // Arrange
        var command = new CreateSharedGameFromPdfCommand(
            PdfDocumentId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            ExtractedTitle: "Catan",
            MinPlayers: 3,
            MaxPlayers: 4,
            PlayingTimeMinutes: 90,
            MinAge: 10,
            SelectedBggId: 13,
            RequiresApproval: false
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyPdfDocumentId_ShouldHaveError()
    {
        // Arrange
        var command = new CreateSharedGameFromPdfCommand(
            PdfDocumentId: Guid.Empty,
            UserId: Guid.NewGuid(),
            ExtractedTitle: "Catan",
            MinPlayers: null,
            MaxPlayers: null,
            PlayingTimeMinutes: null,
            MinAge: null,
            SelectedBggId: null,
            RequiresApproval: false
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PdfDocumentId)
            .WithErrorMessage("PDF Document ID is required");
    }

    [Fact]
    public void Validate_WithEmptyUserId_ShouldHaveError()
    {
        // Arrange
        var command = new CreateSharedGameFromPdfCommand(
            PdfDocumentId: Guid.NewGuid(),
            UserId: Guid.Empty,
            ExtractedTitle: "Catan",
            MinPlayers: null,
            MaxPlayers: null,
            PlayingTimeMinutes: null,
            MinAge: null,
            SelectedBggId: null,
            RequiresApproval: false
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserId)
            .WithErrorMessage("User ID is required");
    }

    [Fact]
    public void Validate_WithEmptyTitle_ShouldHaveError()
    {
        // Arrange
        var command = new CreateSharedGameFromPdfCommand(
            PdfDocumentId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            ExtractedTitle: string.Empty,
            MinPlayers: null,
            MaxPlayers: null,
            PlayingTimeMinutes: null,
            MinAge: null,
            SelectedBggId: null,
            RequiresApproval: false
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ExtractedTitle)
            .WithErrorMessage("Extracted title is required");
    }

    [Fact]
    public void Validate_WithTitleTooLong_ShouldHaveError()
    {
        // Arrange
        var command = new CreateSharedGameFromPdfCommand(
            PdfDocumentId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            ExtractedTitle: new string('A', 201), // 201 characters
            MinPlayers: null,
            MaxPlayers: null,
            PlayingTimeMinutes: null,
            MinAge: null,
            SelectedBggId: null,
            RequiresApproval: false
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ExtractedTitle)
            .WithErrorMessage("Title cannot exceed 200 characters");
    }

    [Theory]
    [InlineData(0)] // Below minimum
    [InlineData(101)] // Above maximum
    public void Validate_WithMinPlayersOutOfRange_ShouldHaveError(int minPlayers)
    {
        // Arrange
        var command = new CreateSharedGameFromPdfCommand(
            PdfDocumentId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            ExtractedTitle: "Catan",
            MinPlayers: minPlayers,
            MaxPlayers: null,
            PlayingTimeMinutes: null,
            MinAge: null,
            SelectedBggId: null,
            RequiresApproval: false
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.MinPlayers)
            .WithErrorMessage("Minimum players must be between 1 and 100");
    }

    [Theory]
    [InlineData(0)] // Below minimum
    [InlineData(101)] // Above maximum
    public void Validate_WithMaxPlayersOutOfRange_ShouldHaveError(int maxPlayers)
    {
        // Arrange
        var command = new CreateSharedGameFromPdfCommand(
            PdfDocumentId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            ExtractedTitle: "Catan",
            MinPlayers: null,
            MaxPlayers: maxPlayers,
            PlayingTimeMinutes: null,
            MinAge: null,
            SelectedBggId: null,
            RequiresApproval: false
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.MaxPlayers)
            .WithErrorMessage("Maximum players must be between 1 and 100");
    }

    [Fact]
    public void Validate_WithMaxPlayersLessThanMinPlayers_ShouldHaveError()
    {
        // Arrange
        var command = new CreateSharedGameFromPdfCommand(
            PdfDocumentId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            ExtractedTitle: "Catan",
            MinPlayers: 4,
            MaxPlayers: 2, // MaxPlayers < MinPlayers
            PlayingTimeMinutes: null,
            MinAge: null,
            SelectedBggId: null,
            RequiresApproval: false
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x)
            .WithErrorMessage("Maximum players must be greater than or equal to minimum players");
    }

    [Theory]
    [InlineData(0)] // Below minimum
    [InlineData(1441)] // Above maximum (24 hours + 1)
    public void Validate_WithPlayingTimeOutOfRange_ShouldHaveError(int playingTime)
    {
        // Arrange
        var command = new CreateSharedGameFromPdfCommand(
            PdfDocumentId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            ExtractedTitle: "Catan",
            MinPlayers: null,
            MaxPlayers: null,
            PlayingTimeMinutes: playingTime,
            MinAge: null,
            SelectedBggId: null,
            RequiresApproval: false
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PlayingTimeMinutes)
            .WithErrorMessage("Playing time must be between 1 and 1440 minutes (24 hours)");
    }

    [Theory]
    [InlineData(0)] // Below minimum
    [InlineData(100)] // Above maximum
    public void Validate_WithMinAgeOutOfRange_ShouldHaveError(int minAge)
    {
        // Arrange
        var command = new CreateSharedGameFromPdfCommand(
            PdfDocumentId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            ExtractedTitle: "Catan",
            MinPlayers: null,
            MaxPlayers: null,
            PlayingTimeMinutes: null,
            MinAge: minAge,
            SelectedBggId: null,
            RequiresApproval: false
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.MinAge)
            .WithErrorMessage("Minimum age must be between 1 and 99");
    }

    [Fact]
    public void Validate_WithNegativeBggId_ShouldHaveError()
    {
        // Arrange
        var command = new CreateSharedGameFromPdfCommand(
            PdfDocumentId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            ExtractedTitle: "Catan",
            MinPlayers: null,
            MaxPlayers: null,
            PlayingTimeMinutes: null,
            MinAge: null,
            SelectedBggId: -1,
            RequiresApproval: false
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.SelectedBggId)
            .WithErrorMessage("BGG ID must be a positive integer");
    }

    [Fact]
    public void Validate_WithNullableFieldsNull_ShouldNotHaveErrors()
    {
        // Arrange - All optional fields are null
        var command = new CreateSharedGameFromPdfCommand(
            PdfDocumentId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            ExtractedTitle: "Catan",
            MinPlayers: null,
            MaxPlayers: null,
            PlayingTimeMinutes: null,
            MinAge: null,
            SelectedBggId: null,
            RequiresApproval: false
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
