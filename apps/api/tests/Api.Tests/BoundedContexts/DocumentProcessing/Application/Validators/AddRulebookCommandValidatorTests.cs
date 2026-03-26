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
/// Unit tests for AddRulebookCommandValidator.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class AddRulebookCommandValidatorTests
{
    private readonly AddRulebookCommandValidator _validator = new();

    #region Valid Command

    [Fact]
    public void Should_Pass_When_Valid_Command()
    {
        // Arrange
        var command = new AddRulebookCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CreateMockPdfFormFile("rulebook.pdf", 1024));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    #endregion

    #region GameId Validation

    [Fact]
    public void Should_Fail_When_GameId_Empty()
    {
        // Arrange
        var command = new AddRulebookCommand(
            Guid.Empty,
            Guid.NewGuid(),
            CreateMockPdfFormFile("rulebook.pdf", 1024));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.GameId)
            .WithErrorMessage("GameId is required.");
    }

    #endregion

    #region UserId Validation

    [Fact]
    public void Should_Fail_When_UserId_Empty()
    {
        // Arrange
        var command = new AddRulebookCommand(
            Guid.NewGuid(),
            Guid.Empty,
            CreateMockPdfFormFile("rulebook.pdf", 1024));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserId)
            .WithErrorMessage("UserId is required.");
    }

    #endregion

    #region File Validation

    [Fact]
    public void Should_Fail_When_File_Null()
    {
        // Arrange
        var command = new AddRulebookCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null!);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.File)
            .WithErrorMessage("A PDF file is required.");
    }

    [Fact]
    public void Should_Fail_When_File_Not_Pdf()
    {
        // Arrange
        var command = new AddRulebookCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CreateMockFormFile("document.txt", 1024, "text/plain"));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.File.ContentType)
            .WithErrorMessage("File must be a PDF.");
    }

    [Fact]
    public void Should_Fail_When_File_Empty()
    {
        // Arrange
        var command = new AddRulebookCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CreateMockPdfFormFile("empty.pdf", 0));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.File.Length)
            .WithErrorMessage("File must not be empty.");
    }

    #endregion

    #region Helper Methods

    private static IFormFile CreateMockPdfFormFile(string fileName, long length)
    {
        return CreateMockFormFile(fileName, length, "application/pdf");
    }

    private static IFormFile CreateMockFormFile(string fileName, long length, string contentType)
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
