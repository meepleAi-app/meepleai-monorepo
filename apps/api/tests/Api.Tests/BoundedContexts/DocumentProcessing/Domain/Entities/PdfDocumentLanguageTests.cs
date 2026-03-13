using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Domain.Entities;

/// <summary>
/// Unit tests for PdfDocument language detection and override features (E5-1).
/// Tests SetDetectedLanguage, OverrideLanguage, and EffectiveLanguage.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public class PdfDocumentLanguageTests
{
    [Fact]
    public void SetDetectedLanguage_ShouldSetLanguageAndConfidence()
    {
        // Arrange
        var document = CreateTestDocument();

        // Act
        document.SetDetectedLanguage("de", 0.94);

        // Assert
        Assert.Equal("de", document.Language.Value);
        Assert.Equal(0.94, document.LanguageConfidence);
    }

    [Fact]
    public void OverrideLanguage_ShouldSetOverride()
    {
        // Arrange
        var document = CreateTestDocument();

        // Act
        document.OverrideLanguage("it");

        // Assert
        Assert.Equal("it", document.LanguageOverride);
    }

    [Fact]
    public void EffectiveLanguage_ReturnsOverrideWhenSet()
    {
        // Arrange
        var document = CreateTestDocument();
        document.SetDetectedLanguage("de", 0.94);
        document.OverrideLanguage("it");

        // Act
        var effective = document.EffectiveLanguage;

        // Assert
        Assert.Equal("it", effective);
    }

    [Fact]
    public void EffectiveLanguage_ReturnsDetectedWhenNoOverride()
    {
        // Arrange
        var document = CreateTestDocument();
        document.SetDetectedLanguage("de", 0.88);

        // Act
        var effective = document.EffectiveLanguage;

        // Assert
        Assert.Equal("de", effective);
    }

    [Fact]
    public void EffectiveLanguage_ReturnsDetectedLanguage_WhenNoOverrideAndDefaultLanguage()
    {
        // Arrange — constructor defaults Language to English
        var document = CreateTestDocument();

        // Act
        var effective = document.EffectiveLanguage;

        // Assert — defaults to English from constructor
        Assert.Equal("en", effective);
    }

    [Fact]
    public void SetDetectedLanguage_InvalidConfidence_BelowZero_Throws()
    {
        // Arrange
        var document = CreateTestDocument();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(
            () => document.SetDetectedLanguage("de", -0.1));
    }

    [Fact]
    public void SetDetectedLanguage_InvalidConfidence_AboveOne_Throws()
    {
        // Arrange
        var document = CreateTestDocument();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(
            () => document.SetDetectedLanguage("de", 1.1));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void SetDetectedLanguage_BoundaryConfidence_DoesNotThrow(double confidence)
    {
        // Arrange
        var document = CreateTestDocument();

        // Act
        document.SetDetectedLanguage("fr", confidence);

        // Assert
        Assert.Equal(confidence, document.LanguageConfidence);
    }

    [Fact]
    public void OverrideLanguage_EmptyString_ClearsOverride()
    {
        // Arrange
        var document = CreateTestDocument();
        document.OverrideLanguage("it");
        Assert.Equal("it", document.LanguageOverride);

        // Act
        document.OverrideLanguage("");

        // Assert
        Assert.Null(document.LanguageOverride);
    }

    [Fact]
    public void OverrideLanguage_Null_ClearsOverride()
    {
        // Arrange
        var document = CreateTestDocument();
        document.OverrideLanguage("it");
        Assert.Equal("it", document.LanguageOverride);

        // Act
        document.OverrideLanguage(null);

        // Assert
        Assert.Null(document.LanguageOverride);
    }

    [Fact]
    public void OverrideLanguage_WhitespaceOnly_ClearsOverride()
    {
        // Arrange
        var document = CreateTestDocument();
        document.OverrideLanguage("it");

        // Act
        document.OverrideLanguage("   ");

        // Assert
        Assert.Null(document.LanguageOverride);
    }

    [Fact]
    public void EffectiveLanguage_AfterClearingOverride_FallsBackToDetected()
    {
        // Arrange
        var document = CreateTestDocument();
        document.SetDetectedLanguage("de", 0.9);
        document.OverrideLanguage("it");
        Assert.Equal("it", document.EffectiveLanguage);

        // Act
        document.OverrideLanguage(null);

        // Assert
        Assert.Equal("de", document.EffectiveLanguage);
    }

    [Fact]
    public void Constructor_DefaultLanguageConfidence_IsNull()
    {
        // Arrange & Act
        var document = CreateTestDocument();

        // Assert
        Assert.Null(document.LanguageConfidence);
    }

    [Fact]
    public void Constructor_DefaultLanguageOverride_IsNull()
    {
        // Arrange & Act
        var document = CreateTestDocument();

        // Assert
        Assert.Null(document.LanguageOverride);
    }

    private static PdfDocument CreateTestDocument()
    {
        return new PdfDocument(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new FileName("test.pdf"),
            "/path/to/test.pdf",
            new FileSize(1024),
            Guid.NewGuid());
    }
}
