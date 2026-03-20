using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Validators;

/// <summary>
/// Unit tests for ApproveDocumentForRagProcessingCommandValidator.
/// Issue #3533: Admin API Endpoints - Document Approval Validation
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class ApproveDocumentForRagProcessingCommandValidatorTests
{
    private readonly ApproveDocumentForRagProcessingCommandValidator _validator = new();

    #region DocumentId Validation

    [Fact]
    public void Validate_WithValidDocumentId_Passes()
    {
        // Arrange
        var command = new ApproveDocumentForRagProcessingCommand(
            DocumentId: Guid.NewGuid(),
            ApprovedBy: Guid.NewGuid());

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyDocumentId_Fails()
    {
        // Arrange
        var command = new ApproveDocumentForRagProcessingCommand(
            DocumentId: Guid.Empty,
            ApprovedBy: Guid.NewGuid());

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DocumentId");
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("required"));
    }

    #endregion

    #region ApprovedBy Validation

    [Fact]
    public void Validate_WithValidApprovedBy_Passes()
    {
        // Arrange
        var command = new ApproveDocumentForRagProcessingCommand(
            DocumentId: Guid.NewGuid(),
            ApprovedBy: Guid.NewGuid());

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyApprovedBy_Fails()
    {
        // Arrange
        var command = new ApproveDocumentForRagProcessingCommand(
            DocumentId: Guid.NewGuid(),
            ApprovedBy: Guid.Empty);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ApprovedBy");
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("audit trail"));
    }

    #endregion

    #region Notes Validation

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Approved for quality content")]
    [InlineData("This document contains comprehensive rules for the game")]
    public void Validate_WithValidNotes_Passes(string? notes)
    {
        // Arrange
        var command = new ApproveDocumentForRagProcessingCommand(
            DocumentId: Guid.NewGuid(),
            ApprovedBy: Guid.NewGuid(),
            Notes: notes);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithMaxLengthNotes_Passes()
    {
        // Arrange
        var maxNotes = new string('a', 500);
        var command = new ApproveDocumentForRagProcessingCommand(
            DocumentId: Guid.NewGuid(),
            ApprovedBy: Guid.NewGuid(),
            Notes: maxNotes);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithTooLongNotes_Fails()
    {
        // Arrange
        var tooLongNotes = new string('a', 501);
        var command = new ApproveDocumentForRagProcessingCommand(
            DocumentId: Guid.NewGuid(),
            ApprovedBy: Guid.NewGuid(),
            Notes: tooLongNotes);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Notes");
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("500"));
    }

    #endregion

    #region Combined Validation

    [Fact]
    public void Validate_WithAllValidParameters_Passes()
    {
        // Arrange
        var command = new ApproveDocumentForRagProcessingCommand(
            DocumentId: Guid.NewGuid(),
            ApprovedBy: Guid.NewGuid(),
            Notes: "Approved - high quality rulebook");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithAllInvalidParameters_ReportsAllErrors()
    {
        // Arrange
        var command = new ApproveDocumentForRagProcessingCommand(
            DocumentId: Guid.Empty,
            ApprovedBy: Guid.Empty,
            Notes: new string('a', 501));

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(3);
        result.Errors.Should().Contain(e => e.PropertyName == "DocumentId");
        result.Errors.Should().Contain(e => e.PropertyName == "ApprovedBy");
        result.Errors.Should().Contain(e => e.PropertyName == "Notes");
    }

    [Fact]
    public void Validate_WithBothIdsEmpty_ReportsBothErrors()
    {
        // Arrange
        var command = new ApproveDocumentForRagProcessingCommand(
            DocumentId: Guid.Empty,
            ApprovedBy: Guid.Empty);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
    }

    #endregion
}