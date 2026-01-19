using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using FluentAssertions;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Domain.ValueObjects;

/// <summary>
/// Unit tests for CollectionName value object.
/// Issue #2051: Collection naming validation
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class CollectionNameTests
{
    [Theory]
    [InlineData("Wingspan Base Game")]
    [InlineData("Terraforming Mars + All Expansions")]
    [InlineData("7 Wonders Complete Collection")]
    public void Constructor_ValidName_CreatesInstance(string name)
    {
        // Act
        var collectionName = new CollectionName(name);

        // Assert
        collectionName.Value.Should().Be(name);
    }

    [Fact]
    public void Constructor_TrimsWhitespace()
    {
        // Arrange
        const string nameWithSpaces = "   Wingspan   ";

        // Act
        var collectionName = new CollectionName(nameWithSpaces);

        // Assert
        collectionName.Value.Should().Be("Wingspan");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_EmptyOrWhitespace_ThrowsValidationException(string invalidName)
    {
        // Act
        Action act = () => new CollectionName(invalidName!);

        // Assert
        act.Should().Throw<ValidationException>()
            .WithMessage("*Collection name cannot be empty*");
    }

    [Fact]
    public void Constructor_TooShort_ThrowsValidationException()
    {
        // Arrange
        const string shortName = "AB"; // 2 chars, min is 3

        // Act
        Action act = () => new CollectionName(shortName);

        // Assert
        act.Should().Throw<ValidationException>()
            .WithMessage("*must be at least 3 characters*");
    }

    [Fact]
    public void Constructor_TooLong_ThrowsValidationException()
    {
        // Arrange
        var longName = new string('A', 201); // 201 chars, max is 200

        // Act
        Action act = () => new CollectionName(longName);

        // Assert
        act.Should().Throw<ValidationException>()
            .WithMessage("*cannot exceed 200 characters*");
    }

    [Fact]
    public void Constructor_ExactlyMaxLength_CreatesInstance()
    {
        // Arrange
        var maxName = new string('A', 200); // Exactly 200 chars

        // Act
        var collectionName = new CollectionName(maxName);

        // Assert
        collectionName.Value.Should().HaveLength(200);
    }

    [Fact]
    public void Constructor_ExactlyMinLength_CreatesInstance()
    {
        // Arrange
        const string minName = "ABC"; // Exactly 3 chars

        // Act
        var collectionName = new CollectionName(minName);

        // Assert
        collectionName.Value.Should().Be("ABC");
    }

    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        // Arrange
        var name1 = new CollectionName("Wingspan");
        var name2 = new CollectionName("Wingspan");

        // Act & Assert
        name1.Should().Be(name2);
        (name1 == name2).Should().BeTrue();
    }

    [Fact]
    public void Equality_DifferentValue_AreNotEqual()
    {
        // Arrange
        var name1 = new CollectionName("Wingspan");
        var name2 = new CollectionName("Terraforming Mars");

        // Act & Assert
        name1.Should().NotBe(name2);
        (name1 != name2).Should().BeTrue();
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        // Arrange
        var collectionName = new CollectionName("My Collection");

        // Act & Assert
        collectionName.ToString().Should().Be("My Collection");
    }
}
