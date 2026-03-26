using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

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
        rules.Should().NotBeNull();
        rules.Content.Should().Be("Game rules content");
        rules.Language.Should().Be("en");
    }

    [Theory]
    [InlineData("", "en")] // Empty content
    [InlineData("   ", "en")] // Whitespace content
    [InlineData("Content", "")] // Empty language
    [InlineData("Content", "   ")] // Whitespace language
    public void Create_WithEmptyFields_ThrowsArgumentException(string content, string language)
    {
        // Act & Assert
        var act = () => GameRules.Create(content, language);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("e")] // Too short
    [InlineData("eng")] // Too long
    [InlineData("EN1")] // Invalid format
    public void Create_WithInvalidLanguageCode_ThrowsArgumentException(string language)
    {
        // Act & Assert
        var act = () => GameRules.Create("Content", language);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Equals_WithSameValues_ReturnsTrue()
    {
        // Arrange
        var rules1 = GameRules.Create("Content", "en");
        var rules2 = GameRules.Create("Content", "en");

        // Act & Assert
        rules2.Should().Be(rules1);
    }

    [Fact]
    public void Equals_WithDifferentContent_ReturnsFalse()
    {
        // Arrange
        var rules1 = GameRules.Create("Content 1", "en");
        var rules2 = GameRules.Create("Content 2", "en");

        // Act & Assert
        rules2.Should().NotBe(rules1);
    }

    [Fact]
    public void Equals_WithDifferentLanguage_ReturnsFalse()
    {
        // Arrange
        var rules1 = GameRules.Create("Content", "en");
        var rules2 = GameRules.Create("Content", "it");

        // Act & Assert
        rules2.Should().NotBe(rules1);
    }
}
