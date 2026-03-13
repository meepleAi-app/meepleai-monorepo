using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Validators;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Tests for OverridePdfLanguageCommand, handler, and validator.
/// E5-2: Language Intelligence for Game Night Improvvisata.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public class OverridePdfLanguageCommandTests
{
    private static (Mock<IPdfDocumentRepository>, Mock<IUnitOfWork>, Mock<ILogger<OverridePdfLanguageCommandHandler>>) CreateMocks()
    {
        var pdfRepositoryMock = new Mock<IPdfDocumentRepository>();
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var loggerMock = new Mock<ILogger<OverridePdfLanguageCommandHandler>>();

        return (pdfRepositoryMock, unitOfWorkMock, loggerMock);
    }

    private static PdfDocument CreateTestPdfDocument(Guid? id = null)
    {
        var pdfId = id ?? Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var fileName = new FileName("test-rules.pdf");
        var fileSize = new FileSize(1024 * 1024);

        return new PdfDocument(
            pdfId,
            gameId,
            fileName,
            "/uploads/test-rules.pdf",
            fileSize,
            userId);
    }

    #region Handler Tests

    [Fact]
    public async Task Handle_CallsOverrideLanguageOnDocument_AndSaves()
    {
        // Arrange
        var (pdfRepoMock, uowMock, loggerMock) = CreateMocks();
        var pdfId = Guid.NewGuid();
        var pdf = CreateTestPdfDocument(pdfId);

        pdfRepoMock
            .Setup(x => x.GetByIdAsync(pdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdf);

        pdfRepoMock
            .Setup(x => x.UpdateAsync(It.IsAny<PdfDocument>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        uowMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = new OverridePdfLanguageCommandHandler(
            pdfRepoMock.Object, uowMock.Object, loggerMock.Object);

        var command = new OverridePdfLanguageCommand(pdfId, "de");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(MediatR.Unit.Value);
        pdf.LanguageOverride.Should().Be("de");
        pdfRepoMock.Verify(x => x.UpdateAsync(pdf, It.IsAny<CancellationToken>()), Times.Once);
        uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNullLanguageCode_ClearsOverride()
    {
        // Arrange
        var (pdfRepoMock, uowMock, loggerMock) = CreateMocks();
        var pdfId = Guid.NewGuid();
        var pdf = CreateTestPdfDocument(pdfId);
        pdf.OverrideLanguage("de"); // Set an override first

        pdfRepoMock
            .Setup(x => x.GetByIdAsync(pdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdf);

        pdfRepoMock
            .Setup(x => x.UpdateAsync(It.IsAny<PdfDocument>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        uowMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = new OverridePdfLanguageCommandHandler(
            pdfRepoMock.Object, uowMock.Object, loggerMock.Object);

        var command = new OverridePdfLanguageCommand(pdfId, null);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        pdf.LanguageOverride.Should().BeNull();
        pdfRepoMock.Verify(x => x.UpdateAsync(pdf, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenPdfNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var (pdfRepoMock, uowMock, loggerMock) = CreateMocks();
        var pdfId = Guid.NewGuid();

        pdfRepoMock
            .Setup(x => x.GetByIdAsync(pdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PdfDocument?)null);

        var handler = new OverridePdfLanguageCommandHandler(
            pdfRepoMock.Object, uowMock.Object, loggerMock.Object);

        var command = new OverridePdfLanguageCommand(pdfId, "en");

        // Act
        Func<Task> act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        pdfRepoMock.Verify(x => x.UpdateAsync(It.IsAny<PdfDocument>(), It.IsAny<CancellationToken>()), Times.Never);
        uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Validator Tests

    [Fact]
    public void Validator_RejectsEmptyPdfId()
    {
        // Arrange
        var validator = new OverridePdfLanguageCommandValidator();
        var command = new OverridePdfLanguageCommand(Guid.Empty, "en");

        // Act
        var result = validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PdfId);
    }

    [Fact]
    public void Validator_RejectsLanguageCodeLongerThan3Chars()
    {
        // Arrange
        var validator = new OverridePdfLanguageCommandValidator();
        var command = new OverridePdfLanguageCommand(Guid.NewGuid(), "engl");

        // Act
        var result = validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.LanguageCode);
    }

    [Fact]
    public void Validator_RejectsLanguageCodeShorterThan2Chars()
    {
        // Arrange
        var validator = new OverridePdfLanguageCommandValidator();
        var command = new OverridePdfLanguageCommand(Guid.NewGuid(), "e");

        // Act
        var result = validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.LanguageCode);
    }

    [Fact]
    public void Validator_AcceptsNullLanguageCode()
    {
        // Arrange
        var validator = new OverridePdfLanguageCommandValidator();
        var command = new OverridePdfLanguageCommand(Guid.NewGuid(), null);

        // Act
        var result = validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.LanguageCode);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("de")]
    [InlineData("it")]
    [InlineData("deu")]
    public void Validator_AcceptsValidLanguageCodes(string code)
    {
        // Arrange
        var validator = new OverridePdfLanguageCommandValidator();
        var command = new OverridePdfLanguageCommand(Guid.NewGuid(), code);

        // Act
        var result = validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        // Arrange
        var (_, uowMock, loggerMock) = CreateMocks();

        // Act
        Action act = () => new OverridePdfLanguageCommandHandler(null!, uowMock.Object, loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("pdfRepository");
    }

    [Fact]
    public void Constructor_WithNullUnitOfWork_ThrowsArgumentNullException()
    {
        // Arrange
        var (pdfRepoMock, _, loggerMock) = CreateMocks();

        // Act
        Action act = () => new OverridePdfLanguageCommandHandler(pdfRepoMock.Object, null!, loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("unitOfWork");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var (pdfRepoMock, uowMock, _) = CreateMocks();

        // Act
        Action act = () => new OverridePdfLanguageCommandHandler(pdfRepoMock.Object, uowMock.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    #endregion
}
