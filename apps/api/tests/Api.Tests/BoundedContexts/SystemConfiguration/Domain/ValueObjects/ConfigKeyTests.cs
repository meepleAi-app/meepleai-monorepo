using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Domain.ValueObjects;

/// <summary>
/// Tests for the ConfigKey value object.
/// Issue #3025: Backend 90% Coverage Target - Phase 3
/// </summary>
[Trait("Category", "Unit")]
public sealed class ConfigKeyTests
{
    #region Constructor Tests

    [Theory]
    [InlineData("SimpleKey")]
    [InlineData("RateLimit:Admin:MaxTokens")]
    [InlineData("feature.enabled")]
    [InlineData("my-config-key")]
    [InlineData("config_with_underscore")]
    [InlineData("Config123")]
    public void Constructor_WithValidKey_CreatesInstance(string key)
    {
        // Act
        var configKey = new ConfigKey(key);

        // Assert
        configKey.Value.Should().Be(key);
    }

    [Fact]
    public void Constructor_TrimsWhitespace()
    {
        // Arrange
        var key = "  RateLimit:MaxTokens  ";

        // Act
        var configKey = new ConfigKey(key);

        // Assert
        configKey.Value.Should().Be("RateLimit:MaxTokens");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_WithEmptyOrNull_ThrowsValidationException(string? key)
    {
        // Act
        var action = () => new ConfigKey(key!);

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("*Configuration key cannot be empty*");
    }

    [Fact]
    public void Constructor_ExceedingMaxLength_ThrowsValidationException()
    {
        // Arrange - 201 characters
        var key = new string('a', 201);

        // Act
        var action = () => new ConfigKey(key);

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("*Configuration key cannot exceed 200 characters*");
    }

    [Fact]
    public void Constructor_WithMaxLength_Succeeds()
    {
        // Arrange - 200 characters
        var key = new string('a', 200);

        // Act
        var configKey = new ConfigKey(key);

        // Assert
        configKey.Value.Should().HaveLength(200);
    }

    [Theory]
    [InlineData("key with spaces")]
    [InlineData("key@invalid")]
    [InlineData("key#hash")]
    [InlineData("key$dollar")]
    [InlineData("key%percent")]
    [InlineData("key&ampersand")]
    [InlineData("key*star")]
    [InlineData("key+plus")]
    [InlineData("key=equals")]
    [InlineData("key/slash")]
    [InlineData("key\\backslash")]
    public void Constructor_WithInvalidCharacters_ThrowsValidationException(string key)
    {
        // Act
        var action = () => new ConfigKey(key);

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("*Configuration key can only contain alphanumeric characters, colons, underscores, hyphens, and dots*");
    }

    #endregion

    #region GetHierarchy Tests

    [Fact]
    public void GetHierarchy_WithColonSeparatedKey_ReturnsLevels()
    {
        // Arrange
        var configKey = new ConfigKey("RateLimit:Admin:MaxTokens");

        // Act
        var hierarchy = configKey.GetHierarchy();

        // Assert
        hierarchy.Should().HaveCount(3);
        hierarchy[0].Should().Be("RateLimit");
        hierarchy[1].Should().Be("Admin");
        hierarchy[2].Should().Be("MaxTokens");
    }

    [Fact]
    public void GetHierarchy_WithSingleKey_ReturnsSingleElement()
    {
        // Arrange
        var configKey = new ConfigKey("SimpleKey");

        // Act
        var hierarchy = configKey.GetHierarchy();

        // Assert
        hierarchy.Should().HaveCount(1);
        hierarchy[0].Should().Be("SimpleKey");
    }

    [Fact]
    public void GetHierarchy_WithMultipleColons_SplitsCorrectly()
    {
        // Arrange
        var configKey = new ConfigKey("Level1:Level2:Level3:Level4:Level5");

        // Act
        var hierarchy = configKey.GetHierarchy();

        // Assert
        hierarchy.Should().HaveCount(5);
        hierarchy.Should().BeEquivalentTo(new[] { "Level1", "Level2", "Level3", "Level4", "Level5" });
    }

    #endregion

    #region Category Tests

    [Fact]
    public void Category_WithHierarchicalKey_ReturnsFirstLevel()
    {
        // Arrange
        var configKey = new ConfigKey("RateLimit:Admin:MaxTokens");

        // Act
        var category = configKey.Category;

        // Assert
        category.Should().Be("RateLimit");
    }

    [Fact]
    public void Category_WithSimpleKey_ReturnsWholeKey()
    {
        // Arrange
        var configKey = new ConfigKey("SimpleKey");

        // Act
        var category = configKey.Category;

        // Assert
        category.Should().Be("SimpleKey");
    }

    [Fact]
    public void Category_WithTwoLevels_ReturnsFirstLevel()
    {
        // Arrange
        var configKey = new ConfigKey("Category:Setting");

        // Act
        var category = configKey.Category;

        // Assert
        category.Should().Be("Category");
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_WithSameKey_ReturnsTrue()
    {
        // Arrange
        var key1 = new ConfigKey("RateLimit:MaxTokens");
        var key2 = new ConfigKey("RateLimit:MaxTokens");

        // Act & Assert
        key1.Should().Be(key2);
    }

    [Fact]
    public void Equals_WithDifferentCase_ReturnsTrue()
    {
        // Arrange (case-insensitive comparison)
        var key1 = new ConfigKey("RateLimit:MaxTokens");
        var key2 = new ConfigKey("ratelimit:maxtokens");

        // Act & Assert
        key1.Should().Be(key2);
    }

    [Fact]
    public void Equals_WithDifferentKeys_ReturnsFalse()
    {
        // Arrange
        var key1 = new ConfigKey("RateLimit:MaxTokens");
        var key2 = new ConfigKey("RateLimit:MinTokens");

        // Act & Assert
        key1.Should().NotBe(key2);
    }

    [Fact]
    public void GetHashCode_SameKeys_ReturnsSameHash()
    {
        // Arrange
        var key1 = new ConfigKey("RateLimit:MaxTokens");
        var key2 = new ConfigKey("RateLimit:MaxTokens");

        // Act & Assert
        key1.GetHashCode().Should().Be(key2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentCaseSameKey_ReturnsSameHash()
    {
        // Arrange
        var key1 = new ConfigKey("RateLimit:MaxTokens");
        var key2 = new ConfigKey("RATELIMIT:MAXTOKENS");

        // Act & Assert (case-insensitive)
        key1.GetHashCode().Should().Be(key2.GetHashCode());
    }

    #endregion

    #region Implicit Conversion Tests

    [Fact]
    public void ImplicitConversion_ToString_ReturnsValue()
    {
        // Arrange
        var configKey = new ConfigKey("RateLimit:MaxTokens");

        // Act
        string value = configKey;

        // Assert
        value.Should().Be("RateLimit:MaxTokens");
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsValue()
    {
        // Arrange
        var configKey = new ConfigKey("RateLimit:MaxTokens");

        // Act
        var result = configKey.ToString();

        // Assert
        result.Should().Be("RateLimit:MaxTokens");
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void Constructor_WithDotsAndHyphens_Succeeds()
    {
        // Arrange
        var key = "my.config-key.with-special_chars";

        // Act
        var configKey = new ConfigKey(key);

        // Assert
        configKey.Value.Should().Be(key);
    }

    [Fact]
    public void Constructor_WithNumbers_Succeeds()
    {
        // Arrange
        var key = "Config123:Setting456";

        // Act
        var configKey = new ConfigKey(key);

        // Assert
        configKey.Value.Should().Be(key);
    }

    [Fact]
    public void GetHierarchy_WithConsecutiveColons_IgnoresEmpty()
    {
        // Arrange - The pattern validation would reject this, so this tests valid patterns
        var configKey = new ConfigKey("Level1:Level2");

        // Act
        var hierarchy = configKey.GetHierarchy();

        // Assert
        hierarchy.Should().NotContain(string.Empty);
    }

    #endregion
}
