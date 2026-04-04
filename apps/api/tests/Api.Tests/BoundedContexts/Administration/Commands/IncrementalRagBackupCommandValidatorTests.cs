using Api.BoundedContexts.Administration.Application.Commands.IncrementalRagBackup;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Commands;

/// <summary>
/// Unit tests for <see cref="IncrementalRagBackupCommandValidator"/>.
/// Verifies that PdfDocumentId must be a non-empty GUID.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
public sealed class IncrementalRagBackupCommandValidatorTests
{
    private readonly IncrementalRagBackupCommandValidator _validator = new();

    // ────────────────────────────────────────────────────────────────────────
    // Valid inputs
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_WithValidGuid_ShouldNotHaveErrors()
    {
        var command = new IncrementalRagBackupCommand(Guid.NewGuid());
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithSpecificValidGuid_ShouldNotHaveErrors()
    {
        var command = new IncrementalRagBackupCommand(Guid.Parse("12345678-1234-1234-1234-123456789abc"));
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    // ────────────────────────────────────────────────────────────────────────
    // Invalid inputs
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_WithEmptyGuid_ShouldHaveError()
    {
        var command = new IncrementalRagBackupCommand(Guid.Empty);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.PdfDocumentId)
            .WithErrorMessage("PdfDocumentId must not be empty.");
    }
}
