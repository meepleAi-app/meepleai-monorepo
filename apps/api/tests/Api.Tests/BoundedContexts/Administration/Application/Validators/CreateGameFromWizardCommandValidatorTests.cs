using Api.BoundedContexts.Administration.Application.Commands.GameWizard;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Unit tests for CreateGameFromWizardCommandValidator.
/// Issue #4673: Verifies BggId > 0 and CreatedByUserId not empty.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
public sealed class CreateGameFromWizardCommandValidatorTests
{
    private readonly CreateGameFromWizardCommandValidator _validator = new();

    // ────────────────────────────────────────────────────────────────────────
    // Valid inputs
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_WithValidBggIdAndUserId_ShouldNotHaveErrors()
    {
        var command = new CreateGameFromWizardCommand(BggId: 174430, CreatedByUserId: Guid.NewGuid());
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(int.MaxValue)]
    public void Validate_WithPositiveBggId_ShouldNotHaveErrorForBggId(int bggId)
    {
        var command = new CreateGameFromWizardCommand(BggId: bggId, CreatedByUserId: Guid.NewGuid());
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.BggId);
    }

    // ────────────────────────────────────────────────────────────────────────
    // BggId validation
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_WithZeroBggId_ShouldHaveErrorForBggId()
    {
        var command = new CreateGameFromWizardCommand(BggId: 0, CreatedByUserId: Guid.NewGuid());
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.BggId)
            .WithErrorMessage("A valid BGG ID is required");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    [InlineData(int.MinValue)]
    public void Validate_WithNegativeBggId_ShouldHaveErrorForBggId(int bggId)
    {
        var command = new CreateGameFromWizardCommand(BggId: bggId, CreatedByUserId: Guid.NewGuid());
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.BggId)
            .WithErrorMessage("A valid BGG ID is required");
    }

    // ────────────────────────────────────────────────────────────────────────
    // CreatedByUserId validation
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_WithEmptyUserId_ShouldHaveErrorForCreatedByUserId()
    {
        var command = new CreateGameFromWizardCommand(BggId: 174430, CreatedByUserId: Guid.Empty);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.CreatedByUserId)
            .WithErrorMessage("CreatedByUserId is required");
    }

    [Fact]
    public void Validate_WithValidUserId_ShouldNotHaveErrorForCreatedByUserId()
    {
        var command = new CreateGameFromWizardCommand(BggId: 1, CreatedByUserId: Guid.NewGuid());
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.CreatedByUserId);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Multiple failures
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_WithZeroBggIdAndEmptyUserId_ShouldHaveTwoErrors()
    {
        var command = new CreateGameFromWizardCommand(BggId: 0, CreatedByUserId: Guid.Empty);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.BggId);
        result.ShouldHaveValidationErrorFor(x => x.CreatedByUserId);
    }
}
