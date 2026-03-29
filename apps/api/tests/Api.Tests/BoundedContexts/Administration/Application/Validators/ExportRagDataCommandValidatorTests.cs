using Api.BoundedContexts.Administration.Application.Commands.ExportRagData;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Unit tests for ExportRagDataCommandValidator.
/// Verifies that GameIdFilter must be null or a valid GUID.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
public sealed class ExportRagDataCommandValidatorTests
{
    private readonly ExportRagDataCommandValidator _validator = new();

    // ────────────────────────────────────────────────────────────────────────
    // Valid inputs
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_DefaultCommand_ShouldNotHaveErrors()
    {
        var command = new ExportRagDataCommand();
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithNullGameIdFilter_ShouldNotHaveErrors()
    {
        var command = new ExportRagDataCommand { GameIdFilter = null };
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithValidGuidGameIdFilter_ShouldNotHaveErrors()
    {
        var command = new ExportRagDataCommand
        {
            GameIdFilter = Guid.NewGuid().ToString()
        };
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithUpperCaseGuidGameIdFilter_ShouldNotHaveErrors()
    {
        var command = new ExportRagDataCommand
        {
            GameIdFilter = Guid.NewGuid().ToString().ToUpperInvariant()
        };
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    // ────────────────────────────────────────────────────────────────────────
    // Invalid GameIdFilter
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_WithInvalidGameIdFilter_ShouldHaveError()
    {
        var command = new ExportRagDataCommand { GameIdFilter = "not-a-guid" };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.GameIdFilter)
            .WithErrorMessage("GameIdFilter must be a valid GUID when provided");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12345")]
    [InlineData("not-a-guid")]
    [InlineData("zzzzzzzz-zzzz-zzzz-zzzz-zzzzzzzzzzzz")]
    public void Validate_WithVariousInvalidGameIdFilters_ShouldHaveError(string invalidFilter)
    {
        var command = new ExportRagDataCommand { GameIdFilter = invalidFilter };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.GameIdFilter)
            .WithErrorMessage("GameIdFilter must be a valid GUID when provided");
    }
}
