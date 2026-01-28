using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.Validators;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Validators;

/// <summary>
/// Tests for UpdatePdfUploadLimitsCommandValidator.
/// Issue #3072: PDF Upload Limits - Admin API
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
public class UpdatePdfUploadLimitsCommandValidatorTests
{
    private readonly UpdatePdfUploadLimitsCommandValidator _validator;

    public UpdatePdfUploadLimitsCommandValidatorTests()
    {
        _validator = new UpdatePdfUploadLimitsCommandValidator();
    }

    [Fact]
    public void ValidateShouldPass_WhenAllValuesValid()
    {
        // Arrange
        var command = new UpdatePdfUploadLimitsCommand(
            MaxFileSizeBytes: 104857600, // 100MB
            MaxPagesPerDocument: 500,
            MaxDocumentsPerGame: 10,
            AllowedMimeTypes: ["application/pdf"],
            UpdatedByUserId: Guid.NewGuid()
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]           // Below minimum
    [InlineData(1048575)]     // Just below 1MB
    [InlineData(524288001)]   // Just above 500MB
    public void ValidateShouldFail_WhenMaxFileSizeOutOfRange(long maxFileSize)
    {
        // Arrange
        var command = new UpdatePdfUploadLimitsCommand(
            MaxFileSizeBytes: maxFileSize,
            MaxPagesPerDocument: 500,
            MaxDocumentsPerGame: 10,
            AllowedMimeTypes: ["application/pdf"],
            UpdatedByUserId: Guid.NewGuid()
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.MaxFileSizeBytes);
    }

    [Theory]
    [InlineData(1048576)]     // 1MB (minimum)
    [InlineData(104857600)]   // 100MB
    [InlineData(524288000)]   // 500MB (maximum)
    public void ValidateShouldPass_WhenMaxFileSizeWithinRange(long maxFileSize)
    {
        // Arrange
        var command = new UpdatePdfUploadLimitsCommand(
            MaxFileSizeBytes: maxFileSize,
            MaxPagesPerDocument: 500,
            MaxDocumentsPerGame: 10,
            AllowedMimeTypes: ["application/pdf"],
            UpdatedByUserId: Guid.NewGuid()
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.MaxFileSizeBytes);
    }

    [Theory]
    [InlineData(0)]     // Below minimum
    [InlineData(-1)]    // Negative
    [InlineData(2001)]  // Above maximum
    public void ValidateShouldFail_WhenMaxPagesOutOfRange(int maxPages)
    {
        // Arrange
        var command = new UpdatePdfUploadLimitsCommand(
            MaxFileSizeBytes: 104857600,
            MaxPagesPerDocument: maxPages,
            MaxDocumentsPerGame: 10,
            AllowedMimeTypes: ["application/pdf"],
            UpdatedByUserId: Guid.NewGuid()
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.MaxPagesPerDocument);
    }

    [Theory]
    [InlineData(1)]     // Minimum
    [InlineData(500)]   // Typical
    [InlineData(2000)]  // Maximum
    public void ValidateShouldPass_WhenMaxPagesWithinRange(int maxPages)
    {
        // Arrange
        var command = new UpdatePdfUploadLimitsCommand(
            MaxFileSizeBytes: 104857600,
            MaxPagesPerDocument: maxPages,
            MaxDocumentsPerGame: 10,
            AllowedMimeTypes: ["application/pdf"],
            UpdatedByUserId: Guid.NewGuid()
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.MaxPagesPerDocument);
    }

    [Theory]
    [InlineData(0)]     // Below minimum
    [InlineData(-1)]    // Negative
    [InlineData(101)]   // Above maximum
    public void ValidateShouldFail_WhenMaxDocumentsOutOfRange(int maxDocuments)
    {
        // Arrange
        var command = new UpdatePdfUploadLimitsCommand(
            MaxFileSizeBytes: 104857600,
            MaxPagesPerDocument: 500,
            MaxDocumentsPerGame: maxDocuments,
            AllowedMimeTypes: ["application/pdf"],
            UpdatedByUserId: Guid.NewGuid()
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.MaxDocumentsPerGame);
    }

    [Theory]
    [InlineData(1)]    // Minimum
    [InlineData(10)]   // Typical
    [InlineData(100)]  // Maximum
    public void ValidateShouldPass_WhenMaxDocumentsWithinRange(int maxDocuments)
    {
        // Arrange
        var command = new UpdatePdfUploadLimitsCommand(
            MaxFileSizeBytes: 104857600,
            MaxPagesPerDocument: 500,
            MaxDocumentsPerGame: maxDocuments,
            AllowedMimeTypes: ["application/pdf"],
            UpdatedByUserId: Guid.NewGuid()
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.MaxDocumentsPerGame);
    }

    [Fact]
    public void ValidateShouldFail_WhenMimeTypesEmpty()
    {
        // Arrange
        var command = new UpdatePdfUploadLimitsCommand(
            MaxFileSizeBytes: 104857600,
            MaxPagesPerDocument: 500,
            MaxDocumentsPerGame: 10,
            AllowedMimeTypes: [],
            UpdatedByUserId: Guid.NewGuid()
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.AllowedMimeTypes);
    }

    [Theory]
    [InlineData("text/plain")]
    [InlineData("image/png")]
    [InlineData("application/json")]
    [InlineData("invalid-mime")]
    public void ValidateShouldFail_WhenMimeTypeNotPdf(string invalidMimeType)
    {
        // Arrange
        var command = new UpdatePdfUploadLimitsCommand(
            MaxFileSizeBytes: 104857600,
            MaxPagesPerDocument: 500,
            MaxDocumentsPerGame: 10,
            AllowedMimeTypes: [invalidMimeType],
            UpdatedByUserId: Guid.NewGuid()
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor("AllowedMimeTypes[0]");
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("application/x-pdf")]
    [InlineData("APPLICATION/PDF")]  // Case insensitive
    public void ValidateShouldPass_WhenMimeTypeIsValidPdf(string validMimeType)
    {
        // Arrange
        var command = new UpdatePdfUploadLimitsCommand(
            MaxFileSizeBytes: 104857600,
            MaxPagesPerDocument: 500,
            MaxDocumentsPerGame: 10,
            AllowedMimeTypes: [validMimeType],
            UpdatedByUserId: Guid.NewGuid()
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.AllowedMimeTypes);
    }

    [Fact]
    public void ValidateShouldPass_WhenMultiplePdfMimeTypes()
    {
        // Arrange
        var command = new UpdatePdfUploadLimitsCommand(
            MaxFileSizeBytes: 104857600,
            MaxPagesPerDocument: 500,
            MaxDocumentsPerGame: 10,
            AllowedMimeTypes: ["application/pdf", "application/x-pdf"],
            UpdatedByUserId: Guid.NewGuid()
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ValidateShouldFail_WhenUpdatedByUserIdEmpty()
    {
        // Arrange
        var command = new UpdatePdfUploadLimitsCommand(
            MaxFileSizeBytes: 104857600,
            MaxPagesPerDocument: 500,
            MaxDocumentsPerGame: 10,
            AllowedMimeTypes: ["application/pdf"],
            UpdatedByUserId: Guid.Empty
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UpdatedByUserId);
    }
}
