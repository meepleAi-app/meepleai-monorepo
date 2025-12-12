using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
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
        Assert.Equal(code.ToLowerInvariant(), languageCode.Value);
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
        Assert.Equal(expected, languageCode.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_WithEmptyValue_ThrowsValidationException(string? invalidCode)
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => new LanguageCode(invalidCode!));
        Assert.Contains("Language code cannot be empty", exception.Message);
    }

    [Theory]
    [InlineData("e")]      // Too short
    [InlineData("eng")]    // Too long (ISO 639-2)
    [InlineData("x")]      // Invalid
    public void Constructor_WithInvalidLength_ThrowsValidationException(string invalidCode)
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => new LanguageCode(invalidCode));
        Assert.True(
            exception.Message.Contains("must be at least 2 characters") ||
            exception.Message.Contains("must be exactly 2 characters"),
            $"Expected length validation message, got: {exception.Message}"
        );
    }

    [Theory]
    [InlineData("xx")]     // Not supported
    [InlineData("yy")]
    [InlineData("zz")]
    [InlineData("aa")]
    public void Constructor_WithUnsupportedLanguage_ThrowsValidationException(string unsupportedCode)
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => new LanguageCode(unsupportedCode));
        Assert.Contains("must be one of:", exception.Message);
    }

    [Fact]
    public void StaticInstances_AreCorrectlyDefined()
    {
        // Assert
        Assert.Equal("en", LanguageCode.English.Value);
        Assert.Equal("it", LanguageCode.Italian.Value);
        Assert.Equal("de", LanguageCode.German.Value);
        Assert.Equal("fr", LanguageCode.French.Value);
        Assert.Equal("es", LanguageCode.Spanish.Value);
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
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetSupportedLanguages_ReturnsAllSupportedCodes()
    {
        // Act
        var supported = LanguageCode.GetSupportedLanguages();

        // Assert
        Assert.Contains("en", supported);
        Assert.Contains("it", supported);
        Assert.Contains("de", supported);
        Assert.Contains("fr", supported);
        Assert.Contains("es", supported);
        Assert.Contains("pt", supported);
        Assert.Contains("pl", supported);
        Assert.Contains("nl", supported);
        Assert.Contains("ja", supported);
        Assert.Contains("zh", supported);
        Assert.Equal(10, supported.Count);
    }

    [Fact]
    public void EqualityComparison_WithSameLanguage_AreEqual()
    {
        // Arrange
        var code1 = new LanguageCode("en");
        var code2 = new LanguageCode("EN");
        var code3 = LanguageCode.English;

        // Act & Assert
        Assert.Equal(code1, code2);
        Assert.Equal(code1, code3);
        Assert.Equal(code2, code3);
    }

    [Fact]
    public void EqualityComparison_WithDifferentLanguages_AreNotEqual()
    {
        // Arrange
        var english = LanguageCode.English;
        var italian = LanguageCode.Italian;
        var german = LanguageCode.German;

        // Act & Assert
        Assert.NotEqual(english, italian);
        Assert.NotEqual(english, german);
        Assert.NotEqual(italian, german);
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
        Assert.Equal(code, result);
    }
}
