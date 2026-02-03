using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Validators;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Validators;

/// <summary>
/// Unit tests for UploadPrivatePdfCommandValidator.
/// Issue #3479: Private PDF Upload Endpoint
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public class UploadPrivatePdfCommandValidatorTests
{
    private readonly UploadPrivatePdfCommandValidator _validator;

    public UploadPrivatePdfCommandValidatorTests()
    {
        _validator = new UploadPrivatePdfCommandValidator();
    }

    #region UserId Validation

    [Fact]
    public void Validate_WithEmptyUserId_FailsValidation()
    {
        // Arrange
        var command = new UploadPrivatePdfCommand(
            Guid.Empty,
            Guid.NewGuid(),
            CreateMockPdfFormFile("test.pdf", 1024));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserId)
            .WithErrorMessage("User ID is required.");
    }

    [Fact]
    public void Validate_WithValidUserId_PassesValidation()
    {
        // Arrange
        var command = new UploadPrivatePdfCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CreateMockPdfFormFile("test.pdf", 1024));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.UserId);
    }

    #endregion

    #region UserLibraryEntryId Validation

    [Fact]
    public void Validate_WithEmptyEntryId_FailsValidation()
    {
        // Arrange
        var command = new UploadPrivatePdfCommand(
            Guid.NewGuid(),
            Guid.Empty,
            CreateMockPdfFormFile("test.pdf", 1024));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserLibraryEntryId)
            .WithErrorMessage("User library entry ID is required.");
    }

    [Fact]
    public void Validate_WithValidEntryId_PassesValidation()
    {
        // Arrange
        var command = new UploadPrivatePdfCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CreateMockPdfFormFile("test.pdf", 1024));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.UserLibraryEntryId);
    }

    #endregion

    #region PdfFile Validation

    [Fact]
    public void Validate_WithNullFile_FailsValidation()
    {
        // Arrange
        var command = new UploadPrivatePdfCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null!);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PdfFile)
            .WithErrorMessage("PDF file is required.");
    }

    [Fact]
    public void Validate_WithEmptyFile_FailsValidation()
    {
        // Arrange
        var command = new UploadPrivatePdfCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CreateMockPdfFormFile("empty.pdf", 0));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PdfFile.Length)
            .WithErrorMessage("PDF file cannot be empty.");
    }

    [Fact]
    public void Validate_WithOversizedFile_FailsValidation()
    {
        // Arrange - 51 MB file (exceeds 50 MB limit)
        var command = new UploadPrivatePdfCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CreateMockPdfFormFile("oversized.pdf", 53_428_800)); // ~51 MB

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PdfFile.Length)
            .WithErrorMessage("PDF file size cannot exceed 50 MB.");
    }

    [Fact]
    public void Validate_WithMaxSizeFile_PassesValidation()
    {
        // Arrange - Exactly 50 MB file
        var command = new UploadPrivatePdfCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CreateMockPdfFormFile("maxsize.pdf", 52_428_800)); // Exactly 50 MB

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.PdfFile.Length);
    }

    [Fact]
    public void Validate_WithInvalidContentType_FailsValidation()
    {
        // Arrange
        var command = new UploadPrivatePdfCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CreateMockFormFile("document.txt", 1024, "text/plain", ".txt"));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PdfFile.ContentType);
    }

    [Fact]
    public void Validate_WithValidContentType_PassesValidation()
    {
        // Arrange
        var command = new UploadPrivatePdfCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CreateMockPdfFormFile("document.pdf", 1024));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.PdfFile.ContentType);
    }

    [Fact]
    public void Validate_WithInvalidExtension_FailsValidation()
    {
        // Arrange
        var command = new UploadPrivatePdfCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CreateMockFormFile("document.txt", 1024, "application/pdf", ".txt"));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PdfFile.FileName)
            .WithErrorMessage("File must have a .pdf extension.");
    }

    [Theory]
    [InlineData("document.pdf")]
    [InlineData("DOCUMENT.PDF")]
    [InlineData("Document.Pdf")]
    public void Validate_WithValidPdfExtension_PassesValidation(string fileName)
    {
        // Arrange
        var command = new UploadPrivatePdfCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CreateMockPdfFormFile(fileName, 1024));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.PdfFile.FileName);
    }

    #endregion

    #region Complete Valid Command

    [Fact]
    public void Validate_WithValidCommand_PassesAllValidation()
    {
        // Arrange
        var command = new UploadPrivatePdfCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CreateMockPdfFormFile("rulebook.pdf", 1024 * 1024)); // 1 MB

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    #endregion

    #region Helper Methods

    private static IFormFile CreateMockPdfFormFile(string fileName, long length)
    {
        return CreateMockFormFile(fileName, length, "application/pdf", ".pdf");
    }

    private static IFormFile CreateMockFormFile(string fileName, long length, string contentType, string extension)
    {
        var formFile = new Mock<IFormFile>();
        formFile.Setup(f => f.FileName).Returns(fileName);
        formFile.Setup(f => f.Length).Returns(length);
        formFile.Setup(f => f.ContentType).Returns(contentType);
        formFile.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream(new byte[Math.Min(length, 1024)]));
        return formFile.Object;
    }

    #endregion
}
