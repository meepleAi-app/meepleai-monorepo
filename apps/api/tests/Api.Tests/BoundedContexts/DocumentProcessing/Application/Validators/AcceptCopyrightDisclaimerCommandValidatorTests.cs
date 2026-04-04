using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Validators;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Validators;

/// <summary>
/// Unit tests for AcceptCopyrightDisclaimerCommandValidator.
/// Issue #5446: Copyright disclaimer acceptance required before RAG processing.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public class AcceptCopyrightDisclaimerCommandValidatorTests
{
    private readonly AcceptCopyrightDisclaimerCommandValidator _validator;

    public AcceptCopyrightDisclaimerCommandValidatorTests()
    {
        _validator = new AcceptCopyrightDisclaimerCommandValidator();
    }

    [Fact]
    public void Validate_WithValidCommand_PassesValidation()
    {
        // Arrange
        var command = new AcceptCopyrightDisclaimerCommand(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyPdfDocumentId_FailsValidation()
    {
        // Arrange — UserId valid, PdfDocumentId empty
        var command = new AcceptCopyrightDisclaimerCommand(Guid.NewGuid(), Guid.Empty);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PdfDocumentId)
            .WithErrorMessage("PDF document ID is required.");
    }

    [Fact]
    public void Validate_WithEmptyUserId_FailsValidation()
    {
        // Arrange — UserId empty, PdfDocumentId valid
        var command = new AcceptCopyrightDisclaimerCommand(Guid.Empty, Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserId)
            .WithErrorMessage("User ID is required.");
    }

    [Fact]
    public void Validate_WithBothFieldsEmpty_FailsBothValidations()
    {
        // Arrange
        var command = new AcceptCopyrightDisclaimerCommand(Guid.Empty, Guid.Empty);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PdfDocumentId);
        result.ShouldHaveValidationErrorFor(x => x.UserId);
        result.Errors.Should().HaveCount(2);
    }
}
