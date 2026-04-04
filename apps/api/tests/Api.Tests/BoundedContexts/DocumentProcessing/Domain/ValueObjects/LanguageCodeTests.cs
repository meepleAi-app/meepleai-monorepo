using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Domain.ValueObjects;

/// <summary>
/// Unit tests for LanguageCode value object (Issue #2029).
/// Validates ISO 639-1 language code handling for PDF filtering.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class LanguageCodeTests
{
    [Theory]
    [InlineData("en")]
    [InlineData("it")]
    [InlineData("de")]
    [InlineData("fr")]
    [InlineData("es")]
    [InlineData("pt")]
    [InlineData("pl")]
    [InlineData("nl")]
    [InlineData("ja")]
    [InlineData("zh")]
    public void Constructor_WithValidLanguageCode_CreatesSuccessfully(string code)
    {
        // Act
        var languageCode = new LanguageCode(code);

        // Assert
        languageCode.Value.Should().Be(code.ToLowerInvariant());
    }

    [Theory]
    [InlineData("EN", "en")]
    [InlineData("IT", "it")]
    [InlineData("De", "de")]
    [InlineData("FR", "fr")]
    [InlineData("Es", "es")]
    public void Constructor_WithDifferentCasing_NormalizesToLowercase(string input, string expected)
    {
        // Act
        var languageCode = new LanguageCode(input);

        // Assert
        languageCode.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_WithEmptyValue_ThrowsValidationException(string? invalidCode)
    {
        // Act & Assert
        var act = () => new LanguageCode(invalidCode!);
        var exception = act.Should().Throw<ValidationException>().Which;
        exception.Message.Should().Contain("Language code cannot be empty");
    }

    [Theory]
    [InlineData("e")]      // Too short
    [InlineData("eng")]    // Too long (ISO 639-2)
    [InlineData("x")]      // Invalid
    public void Constructor_WithInvalidLength_ThrowsValidationException(string invalidCode)
    {
        // Act & Assert
        var act = () => new LanguageCode(invalidCode);
        var exception = act.Should().Throw<ValidationException>().Which;
        (exception.Message.Contains("must be at least 2 characters") ||
            exception.Message.Contains("must be exactly 2 characters")).Should().BeTrue($"Expected length validation message, got: {exception.Message}");
    }

    [Theory]
    [InlineData("xx")]     // Not supported
    [InlineData("yy")]
    [InlineData("zz")]
    [InlineData("aa")]
    public void Constructor_WithUnsupportedLanguage_ThrowsValidationException(string unsupportedCode)
    {
        // Act & Assert
        var act = () => new LanguageCode(unsupportedCode);
        var exception = act.Should().Throw<ValidationException>().Which;
        exception.Message.Should().Contain("must be one of:");
    }

    [Fact]
    public void StaticInstances_AreCorrectlyDefined()
    {
        // Assert
        LanguageCode.English.Value.Should().Be("en");
        LanguageCode.Italian.Value.Should().Be("it");
        LanguageCode.German.Value.Should().Be("de");
        LanguageCode.French.Value.Should().Be("fr");
        LanguageCode.Spanish.Value.Should().Be("es");
    }

    [Theory]
    [InlineData("en", true)]
    [InlineData("it", true)]
    [InlineData("EN", true)]  // Case insensitive
    [InlineData("xx", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsSupported_ReturnsCorrectValue(string? code, bool expected)
    {
        // Act
        var result = LanguageCode.IsSupported(code!);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void GetSupportedLanguages_ReturnsAllSupportedCodes()
    {
        // Act
        var supported = LanguageCode.GetSupportedLanguages();

        // Assert
        supported.Should().Contain("en");
        supported.Should().Contain("it");
        supported.Should().Contain("de");
        supported.Should().Contain("fr");
        supported.Should().Contain("es");
        supported.Should().Contain("pt");
        supported.Should().Contain("pl");
        supported.Should().Contain("nl");
        supported.Should().Contain("ja");
        supported.Should().Contain("zh");
        supported.Count.Should().Be(10);
    }

    [Fact]
    public void EqualityComparison_WithSameLanguage_AreEqual()
    {
        // Arrange
        var code1 = new LanguageCode("en");
        var code2 = new LanguageCode("EN");
        var code3 = LanguageCode.English;

        // Act & Assert
        code2.Should().Be(code1);
        code3.Should().Be(code1);
        code3.Should().Be(code2);
    }

    [Fact]
    public void EqualityComparison_WithDifferentLanguages_AreNotEqual()
    {
        // Arrange
        var english = LanguageCode.English;
        var italian = LanguageCode.Italian;
        var german = LanguageCode.German;

        // Act & Assert
        italian.Should().NotBe(english);
        german.Should().NotBe(english);
        german.Should().NotBe(italian);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("it")]
    [InlineData("de")]
    public void ToString_ReturnsCorrectValue(string code)
    {
        // Arrange
        var languageCode = new LanguageCode(code);

        // Act
        var result = languageCode.ToString();

        // Assert
        result.Should().Be(code);
    }
}
