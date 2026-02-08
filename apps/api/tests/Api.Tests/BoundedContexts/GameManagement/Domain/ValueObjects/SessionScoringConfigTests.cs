using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.ValueObjects;

[Trait("Category", TestCategories.Unit)]
public class SessionScoringConfigTests
{
    [Fact]
    public void SessionScoringConfig_ValidData_CreatesSuccessfully()
    {
        // Arrange
        var dimensions = new List<string> { "points", "ranking" };
        var units = new Dictionary<string, string> { ["points"] = "pts", ["ranking"] = "#" };

        // Act
        var config = new SessionScoringConfig(dimensions, units);

        // Assert
        Assert.Equal(2, config.EnabledDimensions.Count);
        Assert.Contains("points", config.EnabledDimensions);
        Assert.Contains("ranking", config.EnabledDimensions);
        Assert.Equal("pts", config.DimensionUnits["points"]);
        Assert.Equal("#", config.DimensionUnits["ranking"]);
    }

    [Fact]
    public void SessionScoringConfig_NullDimensions_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new SessionScoringConfig(null!));
    }

    [Fact]
    public void SessionScoringConfig_EmptyDimensions_ThrowsValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            new SessionScoringConfig(new List<string>()));

        Assert.Contains("At least one", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SessionScoringConfig_TooManyDimensions_ThrowsValidationException()
    {
        // Arrange
        var dimensions = Enumerable.Range(1, 11).Select(i => $"dim{i}").ToList();

        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            new SessionScoringConfig(dimensions));

        Assert.Contains("Maximum 10", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SessionScoringConfig_EmptyDimensionName_ThrowsValidationException()
    {
        // Arrange
        var dimensions = new List<string> { "points", "" };

        // Act & Assert
        Assert.Throws<ValidationException>(() =>
            new SessionScoringConfig(dimensions));
    }

    [Fact]
    public void SessionScoringConfig_DimensionTooLong_ThrowsValidationException()
    {
        // Arrange
        var longDimension = new string('x', 51);
        var dimensions = new List<string> { longDimension };

        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            new SessionScoringConfig(dimensions));

        Assert.Contains("cannot exceed 50", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateDefault_CreatesPointsConfig()
    {
        // Act
        var config = SessionScoringConfig.CreateDefault();

        // Assert
        Assert.Single(config.EnabledDimensions);
        Assert.Contains("points", config.EnabledDimensions);
        Assert.Equal("pts", config.DimensionUnits["points"]);
    }

    [Fact]
    public void HasDimension_ExistingDimension_ReturnsTrue()
    {
        // Arrange
        var config = new SessionScoringConfig(
            new List<string> { "points", "ranking" });

        // Act & Assert
        Assert.True(config.HasDimension("points"));
        Assert.True(config.HasDimension("ranking"));
        Assert.True(config.HasDimension("POINTS"));  // Case-insensitive
    }

    [Fact]
    public void HasDimension_NonExistingDimension_ReturnsFalse()
    {
        // Arrange
        var config = SessionScoringConfig.CreateDefault();

        // Act & Assert
        Assert.False(config.HasDimension("ranking"));
        Assert.False(config.HasDimension("wins"));
    }

    [Fact]
    public void SessionScoringConfig_EqualityComparison_WorksCorrectly()
    {
        // Arrange
        var config1 = new SessionScoringConfig(
            new List<string> { "points", "ranking" },
            new Dictionary<string, string> { ["points"] = "pts" });

        var config2 = new SessionScoringConfig(
            new List<string> { "ranking", "points" },  // Different order
            new Dictionary<string, string> { ["points"] = "pts" });

        var config3 = new SessionScoringConfig(
            new List<string> { "points" });

        // Assert
        Assert.Equal(config1, config2);  // Order doesn't matter
        Assert.NotEqual(config1, config3);  // Different dimensions
    }
}
