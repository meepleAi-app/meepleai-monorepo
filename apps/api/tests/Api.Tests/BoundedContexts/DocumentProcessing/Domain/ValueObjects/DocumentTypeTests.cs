using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Domain.ValueObjects;

/// <summary>
/// Unit tests for DocumentType value object.
/// Issue #2051: Document type validation and priority
/// </summary>
public class DocumentTypeTests
{
    [Theory]
    [InlineData("base")]
    [InlineData("expansion")]
    [InlineData("errata")]
    [InlineData("homerule")]
    public void Constructor_ValidType_CreatesInstance(string type)
    {
        // Act
        var documentType = new DocumentType(type);

        // Assert
        documentType.Value.Should().Be(type.ToLowerInvariant());
    }

    [Theory]
    [InlineData("BASE", "base")]
    [InlineData("Expansion", "expansion")]
    [InlineData("ERRATA", "errata")]
    [InlineData("HomeRule", "homerule")]
    public void Constructor_MixedCase_NormalizesToLowerCase(string input, string expected)
    {
        // Act
        var documentType = new DocumentType(input);

        // Assert
        documentType.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_EmptyOrNull_ThrowsValidationException(string invalidType)
    {
        // Act
        Action act = () => new DocumentType(invalidType!);

        // Assert
        act.Should().Throw<ValidationException>()
            .WithMessage("*Document type cannot be empty*");
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("supplement")]
    [InlineData("rulebook")]
    [InlineData("base-game")]
    public void Constructor_InvalidType_ThrowsValidationException(string invalidType)
    {
        // Act
        Action act = () => new DocumentType(invalidType);

        // Assert
        act.Should().Throw<ValidationException>()
            .WithMessage("*Document type must be one of:*");
    }

    [Fact]
    public void Priority_Base_Returns0()
    {
        // Arrange
        var documentType = DocumentType.Base;

        // Act & Assert
        documentType.Priority.Should().Be(0);
    }

    [Fact]
    public void Priority_Expansion_Returns1()
    {
        // Arrange
        var documentType = DocumentType.Expansion;

        // Act & Assert
        documentType.Priority.Should().Be(1);
    }

    [Fact]
    public void Priority_Errata_Returns2()
    {
        // Arrange
        var documentType = DocumentType.Errata;

        // Act & Assert
        documentType.Priority.Should().Be(2);
    }

    [Fact]
    public void Priority_Homerule_Returns3()
    {
        // Arrange
        var documentType = DocumentType.Homerule;

        // Act & Assert
        documentType.Priority.Should().Be(3);
    }

    [Fact]
    public void HasHigherPriorityThan_Homerule_vs_Base_ReturnsTrue()
    {
        // Arrange
        var homerule = DocumentType.Homerule;
        var baseType = DocumentType.Base;

        // Act & Assert
        homerule.HasHigherPriorityThan(baseType).Should().BeTrue();
    }

    [Fact]
    public void HasHigherPriorityThan_Base_vs_Homerule_ReturnsFalse()
    {
        // Arrange
        var baseType = DocumentType.Base;
        var homerule = DocumentType.Homerule;

        // Act & Assert
        baseType.HasHigherPriorityThan(homerule).Should().BeFalse();
    }

    [Fact]
    public void HasLowerPriorityThan_Base_vs_Errata_ReturnsTrue()
    {
        // Arrange
        var baseType = DocumentType.Base;
        var errata = DocumentType.Errata;

        // Act & Assert
        baseType.HasLowerPriorityThan(errata).Should().BeTrue();
    }

    [Fact]
    public void IsValid_ValidTypes_ReturnsTrue()
    {
        // Act & Assert
        DocumentType.IsValid("base").Should().BeTrue();
        DocumentType.IsValid("expansion").Should().BeTrue();
        DocumentType.IsValid("errata").Should().BeTrue();
        DocumentType.IsValid("homerule").Should().BeTrue();
        DocumentType.IsValid("BASE").Should().BeTrue(); // Case insensitive
    }

    [Fact]
    public void IsValid_InvalidTypes_ReturnsFalse()
    {
        // Act & Assert
        DocumentType.IsValid("invalid").Should().BeFalse();
        DocumentType.IsValid("").Should().BeFalse();
        DocumentType.IsValid(null).Should().BeFalse();
    }

    [Fact]
    public void GetValidTypes_ReturnsAllFourTypes()
    {
        // Act
        var validTypes = DocumentType.GetValidTypes();

        // Assert
        validTypes.Should().HaveCount(4);
        validTypes.Should().Contain("base");
        validTypes.Should().Contain("expansion");
        validTypes.Should().Contain("errata");
        validTypes.Should().Contain("homerule");
    }

    [Fact]
    public void Equality_SameType_AreEqual()
    {
        // Arrange
        var type1 = new DocumentType("base");
        var type2 = new DocumentType("BASE");

        // Act & Assert
        type1.Should().Be(type2);
        (type1 == type2).Should().BeTrue();
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        // Arrange
        var documentType = DocumentType.Expansion;

        // Act & Assert
        documentType.ToString().Should().Be("expansion");
    }

    [Fact]
    public void ConvenienceProperties_AllTypesAvailable()
    {
        // Act & Assert
        DocumentType.Base.Value.Should().Be("base");
        DocumentType.Expansion.Value.Should().Be("expansion");
        DocumentType.Errata.Value.Should().Be("errata");
        DocumentType.Homerule.Value.Should().Be("homerule");
    }
}
