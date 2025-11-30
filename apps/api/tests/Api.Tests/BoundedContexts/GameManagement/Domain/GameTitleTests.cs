using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain;

public class GameTitleTests
{
    [Fact]
    public void GameTitle_WithValidTitle_CreatesSuccessfully()
    {
        // Arrange & Act
        var title = new GameTitle("Catan");

        // Assert
        Assert.Equal("Catan", title.Value);
        Assert.Equal("catan", title.NormalizedValue);
        Assert.Equal("catan", title.Slug);
    }

    [Fact]
    public void GameTitle_TrimsWhitespace()
    {
        // Arrange & Act
        var title = new GameTitle("  Ticket to Ride  ");

        // Assert
        Assert.Equal("Ticket to Ride", title.Value);
    }

    [Fact]
    public void GameTitle_NormalizesToLowercase()
    {
        // Arrange & Act
        var title = new GameTitle("Ticket to RIDE");

        // Assert
        Assert.Equal("ticket to ride", title.NormalizedValue);
    }

    [Fact]
    public void GameTitle_GeneratesSlugCorrectly()
    {
        // Arrange & Act
        var title = new GameTitle("Ticket to Ride: Europe!");

        // Assert
        Assert.Equal("ticket-to-ride-europe", title.Slug);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void GameTitle_WithEmptyValue_ThrowsValidationException(string invalidTitle)
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => new GameTitle(invalidTitle));
        Assert.Contains("Game title cannot be empty", exception.Message);
    }

    [Fact]
    public void GameTitle_ExceedingMaxLength_ThrowsValidationException()
    {
        // Arrange
        var longTitle = new string('a', 201);

        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => new GameTitle(longTitle));
        Assert.Contains("cannot exceed 200 characters", exception.Message);
    }

    [Fact]
    public void GameTitle_GenerateId_IsDeterministic()
    {
        // Arrange
        var title1 = new GameTitle("Catan");
        var title2 = new GameTitle("CATAN");

        // Act
        var id1 = title1.GenerateId();
        var id2 = title2.GenerateId();

        // Assert
        Assert.Equal(id1, id2); // Same normalized title = same ID
    }

    [Fact]
    public void GameTitle_EqualityComparison_WorksCorrectly()
    {
        // Arrange
        var title1 = new GameTitle("Catan");
        var title2 = new GameTitle("CATAN");
        var title3 = new GameTitle("Ticket to Ride");

        // Act & Assert
        Assert.Equal(title1, title2); // Same normalized value
        Assert.NotEqual(title1, title3);
    }

    [Fact]
    public void GameTitle_ImplicitStringConversion_Works()
    {
        // Arrange
        var title = new GameTitle("Catan");

        // Act
        string titleString = title;

        // Assert
        Assert.Equal("Catan", titleString);
    }
}

