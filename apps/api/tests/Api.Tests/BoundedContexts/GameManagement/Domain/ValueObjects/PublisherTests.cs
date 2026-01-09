using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.ValueObjects;

[Trait("Category", TestCategories.Unit)]
public class PublisherTests
{
    [Fact]
    public void Create_ValidPublisher_Succeeds()
    {
        // Arrange
        var name = "Asmodee";

        // Act
        var publisher = new Publisher(name);

        // Assert
        publisher.Name.Should().Be(name);
        publisher.ToString().Should().Be(name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_NullOrEmptyName_ThrowsValidationException(string? invalidName)
    {
        // Act
        var act = () => new Publisher(invalidName!);

        // Assert
        act.Should().Throw<ValidationException>()
            .WithMessage("*Publisher name cannot be empty*");
    }

    [Fact]
    public void Create_NameExceeding100Characters_ThrowsValidationException()
    {
        // Arrange
        var longName = new string('a', 101);

        // Act
        var act = () => new Publisher(longName);

        // Assert
        act.Should().Throw<ValidationException>()
            .WithMessage("*Publisher name cannot exceed 100 characters*");
    }

    [Fact]
    public void Create_NameWithWhitespace_TrimsWhitespace()
    {
        // Arrange
        var nameWithWhitespace = "  Days of Wonder  ";

        // Act
        var publisher = new Publisher(nameWithWhitespace);

        // Assert
        publisher.Name.Should().Be("Days of Wonder");
    }

    [Fact]
    public void Equals_TwoPublishersWithSameName_AreEqual()
    {
        // Arrange
        var publisher1 = new Publisher("CMON");
        var publisher2 = new Publisher("CMON");

        // Act & Assert
        publisher1.Should().Be(publisher2);
        publisher1.GetHashCode().Should().Be(publisher2.GetHashCode());
    }

    [Fact]
    public void Equals_TwoPublishersWithSameNameDifferentCase_AreEqual()
    {
        // Arrange
        var publisher1 = new Publisher("Stonemaier Games");
        var publisher2 = new Publisher("STONEMAIER GAMES");

        // Act & Assert
        publisher1.Should().Be(publisher2); // GetEqualityComponents uses ToLowerInvariant
    }

    [Fact]
    public void Equals_TwoPublishersWithDifferentNames_AreNotEqual()
    {
        // Arrange
        var publisher1 = new Publisher("Publisher 1");
        var publisher2 = new Publisher("Publisher 2");

        // Act & Assert
        publisher1.Should().NotBe(publisher2);
    }

    [Fact]
    public void ImplicitConversion_ToString_WorksCorrectly()
    {
        // Arrange
        var publisher = new Publisher("Fantasy Flight Games");

        // Act
        string name = publisher;

        // Assert
        name.Should().Be("Fantasy Flight Games");
    }
}
