using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Domain.ValueObjects;

/// <summary>
/// Unit tests for FileName value object.
/// Issue #2640: Comprehensive test suite for DocumentProcessing bounded context
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class FileNameTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_ValidPdfFileName_CreatesInstance()
    {
        // Act
        var fileName = new FileName("rulebook.pdf");

        // Assert
        fileName.Value.Should().Be("rulebook.pdf");
    }

    [Fact]
    public void Constructor_TrimsWhitespace_CreatesInstance()
    {
        // Act
        var fileName = new FileName("  rulebook.pdf  ");

        // Assert
        fileName.Value.Should().Be("rulebook.pdf");
    }

    [Theory]
    [InlineData("my-game-rules.pdf")]
    [InlineData("Game_Rulebook_v2.pdf")]
    [InlineData("123-numbers.pdf")]
    [InlineData("a.pdf")]
    [InlineData("UPPERCASE.PDF")]
    [InlineData("MixedCase.Pdf")]
    public void Constructor_VariousValidNames_CreatesInstance(string name)
    {
        // Act
        var fileName = new FileName(name);

        // Assert
        fileName.Value.Should().NotBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_EmptyOrNull_ThrowsValidationException(string? invalidName)
    {
        // Act
        Action act = () => new FileName(invalidName!);

        // Assert
        act.Should().Throw<ValidationException>()
            .WithMessage("*File name cannot be empty*");
    }

    [Theory]
    [InlineData("document.txt")]
    [InlineData("image.png")]
    [InlineData("archive.zip")]
    [InlineData("document.docx")]
    [InlineData("noextension")]
    [InlineData("file.pdf.txt")]
    public void Constructor_NotPdfExtension_ThrowsValidationException(string invalidName)
    {
        // Act
        Action act = () => new FileName(invalidName);

        // Assert
        act.Should().Throw<ValidationException>()
            .WithMessage("*File must be a PDF*");
    }

    [Fact]
    public void Constructor_TooLongFileName_ThrowsValidationException()
    {
        // Arrange - 256 characters total (exceeds 255 limit)
        var longName = new string('a', 252) + ".pdf";

        // Act
        Action act = () => new FileName(longName);

        // Assert
        act.Should().Throw<ValidationException>()
            .WithMessage("*File name cannot exceed 255 characters*");
    }

    [Fact]
    public void Constructor_MaxLengthFileName_CreatesInstance()
    {
        // Arrange - Exactly 255 characters
        var maxName = new string('a', 251) + ".pdf";

        // Act
        var fileName = new FileName(maxName);

        // Assert
        fileName.Value.Length.Should().Be(255);
    }

    #endregion

    #region GetNameWithoutExtension Tests

    [Theory]
    [InlineData("rulebook.pdf", "rulebook")]
    [InlineData("my-game-rules.pdf", "my-game-rules")]
    [InlineData("Game_v2.pdf", "Game_v2")]
    [InlineData("a.pdf", "a")]
    [InlineData("file.name.with.dots.pdf", "file.name.with.dots")]
    public void GetNameWithoutExtension_ReturnsNameWithoutPdf(string input, string expected)
    {
        // Arrange
        var fileName = new FileName(input);

        // Act
        var result = fileName.GetNameWithoutExtension();

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equality_SameNameSameCase_AreEqual()
    {
        // Arrange
        var fileName1 = new FileName("rulebook.pdf");
        var fileName2 = new FileName("rulebook.pdf");

        // Assert
        fileName1.Should().Be(fileName2);
        (fileName1 == fileName2).Should().BeTrue();
        (fileName1 != fileName2).Should().BeFalse();
    }

    [Fact]
    public void Equality_SameNameDifferentCase_AreEqual()
    {
        // Arrange - Equality is case-insensitive (normalized to lowercase)
        var fileName1 = new FileName("Rulebook.pdf");
        var fileName2 = new FileName("rulebook.pdf");

        // Assert
        fileName1.Should().Be(fileName2);
    }

    [Fact]
    public void Equality_DifferentNames_AreNotEqual()
    {
        // Arrange
        var fileName1 = new FileName("rulebook1.pdf");
        var fileName2 = new FileName("rulebook2.pdf");

        // Assert
        fileName1.Should().NotBe(fileName2);
        (fileName1 == fileName2).Should().BeFalse();
        (fileName1 != fileName2).Should().BeTrue();
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsValue()
    {
        // Arrange
        var fileName = new FileName("my-rulebook.pdf");

        // Act
        var result = fileName.ToString();

        // Assert
        result.Should().Be("my-rulebook.pdf");
    }

    #endregion

    #region Edge Cases

    [Theory]
    [InlineData("test.PDF")]    // Uppercase extension
    [InlineData("test.Pdf")]    // Mixed case extension
    [InlineData("test.pDf")]    // Mixed case extension
    public void Constructor_CaseInsensitiveExtension_CreatesInstance(string name)
    {
        // Act
        var fileName = new FileName(name);

        // Assert
        fileName.Value.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_FileNameWithSpaces_CreatesInstance()
    {
        // Act
        var fileName = new FileName("My Game Rulebook.pdf");

        // Assert
        fileName.Value.Should().Be("My Game Rulebook.pdf");
    }

    [Fact]
    public void Constructor_FileNameWithSpecialCharacters_CreatesInstance()
    {
        // Act
        var fileName = new FileName("Game-Rules_v1.2 (Final).pdf");

        // Assert
        fileName.Value.Should().Be("Game-Rules_v1.2 (Final).pdf");
    }

    [Fact]
    public void Constructor_UnicodeFileName_CreatesInstance()
    {
        // Act
        var fileName = new FileName("规则书.pdf");

        // Assert
        fileName.Value.Should().Be("规则书.pdf");
    }

    #endregion
}
