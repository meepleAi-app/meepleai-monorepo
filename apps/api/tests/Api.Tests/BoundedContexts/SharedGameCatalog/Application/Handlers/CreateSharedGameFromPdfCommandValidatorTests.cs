using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

/// <summary>
/// Validator tests for CreateSharedGameFromPdfCommand (Issue #4140).
/// Covers all validation rules: required fields, range constraints, cross-field validation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "4140")]
public sealed class CreateSharedGameFromPdfCommandValidatorTests
{
    private readonly CreateSharedGameFromPdfCommandValidator _validator = new();

    private static CreateSharedGameFromPdfCommand ValidCommand() => new(
        PdfDocumentId: Guid.NewGuid(),
        UserId: Guid.NewGuid(),
        ExtractedTitle: "Catan",
        MinPlayers: 3,
        MaxPlayers: 4,
        PlayingTimeMinutes: 90,
        MinAge: 10,
        SelectedBggId: 13,
        RequiresApproval: false);

    #region Valid Commands

    [Fact]
    public void Validate_WithValidCommand_PassesValidation()
    {
        var result = _validator.TestValidate(ValidCommand());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithNullOptionalFields_PassesValidation()
    {
        var command = new CreateSharedGameFromPdfCommand(
            PdfDocumentId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            ExtractedTitle: "Spirit Island",
            MinPlayers: null,
            MaxPlayers: null,
            PlayingTimeMinutes: null,
            MinAge: null,
            SelectedBggId: null,
            RequiresApproval: true);

        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion

    #region PdfDocumentId Validation

    [Fact]
    public void Validate_WithEmptyPdfDocumentId_FailsValidation()
    {
        var command = ValidCommand() with { PdfDocumentId = Guid.Empty };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.PdfDocumentId);
    }

    #endregion

    #region UserId Validation

    [Fact]
    public void Validate_WithEmptyUserId_FailsValidation()
    {
        var command = ValidCommand() with { UserId = Guid.Empty };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }

    #endregion

    #region ExtractedTitle Validation

    [Fact]
    public void Validate_WithEmptyTitle_FailsValidation()
    {
        var command = ValidCommand() with { ExtractedTitle = "" };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.ExtractedTitle);
    }

    [Fact]
    public void Validate_WithTitleExceeding200Characters_FailsValidation()
    {
        var command = ValidCommand() with { ExtractedTitle = new string('A', 201) };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.ExtractedTitle);
    }

    [Fact]
    public void Validate_WithTitleExactly200Characters_PassesValidation()
    {
        var command = ValidCommand() with { ExtractedTitle = new string('A', 200) };
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.ExtractedTitle);
    }

    #endregion

    #region MinPlayers Validation

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void Validate_WithMinPlayersOutOfRange_FailsValidation(int minPlayers)
    {
        var command = ValidCommand() with { MinPlayers = minPlayers };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.MinPlayers);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    public void Validate_WithMinPlayersInRange_PassesValidation(int minPlayers)
    {
        var command = ValidCommand() with { MinPlayers = minPlayers, MaxPlayers = 100 };
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.MinPlayers);
    }

    #endregion

    #region MaxPlayers Validation

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void Validate_WithMaxPlayersOutOfRange_FailsValidation(int maxPlayers)
    {
        var command = ValidCommand() with { MaxPlayers = maxPlayers };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.MaxPlayers);
    }

    #endregion

    #region Cross-field: MaxPlayers >= MinPlayers

    [Fact]
    public void Validate_WithMaxPlayersLessThanMinPlayers_FailsValidation()
    {
        var command = ValidCommand() with { MinPlayers = 5, MaxPlayers = 3 };
        var result = _validator.TestValidate(command);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithMaxPlayersEqualToMinPlayers_PassesValidation()
    {
        var command = ValidCommand() with { MinPlayers = 4, MaxPlayers = 4 };
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion

    #region PlayingTimeMinutes Validation

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1441)]
    public void Validate_WithPlayingTimeOutOfRange_FailsValidation(int minutes)
    {
        var command = ValidCommand() with { PlayingTimeMinutes = minutes };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.PlayingTimeMinutes);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(720)]
    [InlineData(1440)]
    public void Validate_WithPlayingTimeInRange_PassesValidation(int minutes)
    {
        var command = ValidCommand() with { PlayingTimeMinutes = minutes };
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.PlayingTimeMinutes);
    }

    #endregion

    #region MinAge Validation

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100)]
    public void Validate_WithMinAgeOutOfRange_FailsValidation(int minAge)
    {
        var command = ValidCommand() with { MinAge = minAge };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.MinAge);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(12)]
    [InlineData(99)]
    public void Validate_WithMinAgeInRange_PassesValidation(int minAge)
    {
        var command = ValidCommand() with { MinAge = minAge };
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.MinAge);
    }

    #endregion

    #region SelectedBggId Validation

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Validate_WithBggIdNotPositive_FailsValidation(int bggId)
    {
        var command = ValidCommand() with { SelectedBggId = bggId };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.SelectedBggId);
    }

    [Fact]
    public void Validate_WithNullBggId_PassesValidation()
    {
        var command = ValidCommand() with { SelectedBggId = null };
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.SelectedBggId);
    }

    #endregion
}
