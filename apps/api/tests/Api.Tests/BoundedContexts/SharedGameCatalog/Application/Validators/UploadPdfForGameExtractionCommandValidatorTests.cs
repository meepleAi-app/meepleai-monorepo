using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Application.Validators;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Validators;

/// <summary>
/// Unit tests for UploadPdfForGameExtractionCommandValidator.
/// Issue #4154: Upload PDF Command for Game Metadata Extraction Wizard
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class UploadPdfForGameExtractionCommandValidatorTests
{
    private readonly UploadPdfForGameExtractionCommandValidator _validator;

    public UploadPdfForGameExtractionCommandValidatorTests()
    {
        _validator = new UploadPdfForGameExtractionCommandValidator();
    }

    #region UserId Validation

    [Fact]
    public void Validate_WithEmptyUserId_FailsValidation()
    {
        // Arrange
        var command = new UploadPdfForGameExtractionCommand(
            CreateMockPdfFormFile("test.pdf", 1024),
            Guid.Empty);

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
        var command = new UploadPdfForGameExtractionCommand(
            CreateMockPdfFormFile("test.pdf", 1024),
            Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.UserId);
    }

    #endregion

    #region File Validation

    [Fact]
    public void Validate_WithNullFile_FailsValidation()
    {
        // Arrange
        var command = new UploadPdfForGameExtractionCommand(null!, Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.File)
            .WithErrorMessage("PDF file is required.");
    }

    [Fact]
    public void Validate_WithEmptyFile_FailsValidation()
    {
        // Arrange
        var command = new UploadPdfForGameExtractionCommand(
            CreateMockPdfFormFile("test.pdf", 0),
            Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.File!.Length)
            .WithErrorMessage("PDF file cannot be empty.");
    }

    [Fact]
    public void Validate_WithFileTooLarge_FailsValidation()
    {
        // Arrange
        var command = new UploadPdfForGameExtractionCommand(
            CreateMockPdfFormFile("test.pdf", 52_428_801), // 50MB + 1 byte
            Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.File!.Length)
            .WithErrorMessage("PDF file size cannot exceed 50 MB.");
    }

    [Theory]
    [InlineData(1024)]
    [InlineData(1_048_576)] // 1 MB
    [InlineData(52_428_800)] // Exactly 50 MB
    public void Validate_WithValidFileSize_PassesValidation(long fileSize)
    {
        // Arrange
        var command = new UploadPdfForGameExtractionCommand(
            CreateMockPdfFormFile("test.pdf", fileSize),
            Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.File!.Length);
    }

    [Theory]
    [InlineData("application/msword")]
    [InlineData("image/png")]
    [InlineData("text/plain")]
    public void Validate_WithInvalidContentType_FailsValidation(string contentType)
    {
        // Arrange
        var mockFile = CreateMockPdfFormFile("test.pdf", 1024);
        Mock.Get(mockFile).SetupGet(f => f.ContentType).Returns(contentType);

        var command = new UploadPdfForGameExtractionCommand(mockFile, Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.File!.ContentType)
            .WithErrorMessage($"Only PDF files are allowed. Received content type: {contentType}");
    }

    [Fact]
    public void Validate_WithValidContentType_PassesValidation()
    {
        // Arrange
        var command = new UploadPdfForGameExtractionCommand(
            CreateMockPdfFormFile("test.pdf", 1024),
            Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.File!.ContentType);
    }

    [Theory]
    [InlineData("document.doc")]
    [InlineData("file.txt")]
    [InlineData("image.png")]
    [InlineData("file")]
    public void Validate_WithInvalidFileExtension_FailsValidation(string fileName)
    {
        // Arrange
        var mockFile = CreateMockPdfFormFile(fileName, 1024);

        var command = new UploadPdfForGameExtractionCommand(mockFile, Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.File!.FileName)
            .WithErrorMessage("File must have a .pdf extension.");
    }

    [Theory]
    [InlineData("document.pdf")]
    [InlineData("file.PDF")]
    [InlineData("Test.Pdf")]
    public void Validate_WithValidFileExtension_PassesValidation(string fileName)
    {
        // Arrange
        var mockFile = CreateMockPdfFormFile(fileName, 1024);

        var command = new UploadPdfForGameExtractionCommand(mockFile, Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.File!.FileName);
    }

    [Fact]
    public void Validate_WithAllValidProperties_PassesValidation()
    {
        // Arrange
        var command = new UploadPdfForGameExtractionCommand(
            CreateMockPdfFormFile("valid-document.pdf", 10_485_760), // 10 MB
            Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion

    #region Helper Methods

    private static IFormFile CreateMockPdfFormFile(string fileName, long fileSize)
    {
        var mockFile = new Mock<IFormFile>();
        mockFile.SetupGet(f => f.FileName).Returns(fileName);
        mockFile.SetupGet(f => f.Length).Returns(fileSize);
        mockFile.SetupGet(f => f.ContentType).Returns("application/pdf");

        return mockFile.Object;
    }

    #endregion
}
