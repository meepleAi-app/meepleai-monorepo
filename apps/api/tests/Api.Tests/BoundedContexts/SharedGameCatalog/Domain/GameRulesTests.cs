using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain;

[Trait("Category", TestCategories.Unit)]
public class GameRulesTests
{
    [Fact]
    public void Create_WithValidData_CreatesGameRulesSuccessfully()
    {
        // Act
        var rules = GameRules.Create("Game rules content", "en");

        // Assert
        Assert.NotNull(rules);
        Assert.Equal("Game rules content", rules.Content);
        Assert.Equal("en", rules.Language);
    }

    [Theory]
    [InlineData("", "en")] // Empty content
    [InlineData("   ", "en")] // Whitespace content
    [InlineData("Content", "")] // Empty language
    [InlineData("Content", "   ")] // Whitespace language
    public void Create_WithEmptyFields_ThrowsArgumentException(string content, string language)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => GameRules.Create(content, language));
    }

    [Theory]
    [InlineData("e")] // Too short
    [InlineData("eng")] // Too long
    [InlineData("EN1")] // Invalid format
    public void Create_WithInvalidLanguageCode_ThrowsArgumentException(string language)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => GameRules.Create("Content", language));
    }

    [Fact]
    public void Equals_WithSameValues_ReturnsTrue()
    {
        // Arrange
        var rules1 = GameRules.Create("Content", "en");
        var rules2 = GameRules.Create("Content", "en");

        // Act & Assert
        Assert.Equal(rules1, rules2);
    }

    [Fact]
    public void Equals_WithDifferentContent_ReturnsFalse()
    {
        // Arrange
        var rules1 = GameRules.Create("Content 1", "en");
        var rules2 = GameRules.Create("Content 2", "en");

        // Act & Assert
        Assert.NotEqual(rules1, rules2);
    }

    [Fact]
    public void Equals_WithDifferentLanguage_ReturnsFalse()
    {
        // Arrange
        var rules1 = GameRules.Create("Content", "en");
        var rules2 = GameRules.Create("Content", "it");

        // Act & Assert
        Assert.NotEqual(rules1, rules2);
    }
}
