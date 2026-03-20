using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

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
        config.EnabledDimensions.Count.Should().Be(2);
        config.EnabledDimensions.Should().Contain("points");
        config.EnabledDimensions.Should().Contain("ranking");
        config.DimensionUnits["points"].Should().Be("pts");
        config.DimensionUnits["ranking"].Should().Be("#");
    }

    [Fact]
    public void SessionScoringConfig_NullDimensions_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () =>
            new SessionScoringConfig(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SessionScoringConfig_EmptyDimensions_ThrowsValidationException()
    {
        // Act & Assert
        var act = () =>
            new SessionScoringConfig(new List<string>());
        var exception = act.Should().Throw<ValidationException>().Which;

        exception.Message.Should().ContainEquivalentOf("At least one");
    }

    [Fact]
    public void SessionScoringConfig_TooManyDimensions_ThrowsValidationException()
    {
        // Arrange
        var dimensions = Enumerable.Range(1, 11).Select(i => $"dim{i}").ToList();

        // Act & Assert
        var act = () =>
            new SessionScoringConfig(dimensions);
        var exception = act.Should().Throw<ValidationException>().Which;

        exception.Message.Should().ContainEquivalentOf("Maximum 10");
    }

    [Fact]
    public void SessionScoringConfig_EmptyDimensionName_ThrowsValidationException()
    {
        // Arrange
        var dimensions = new List<string> { "points", "" };

        // Act & Assert
        var act = () =>
            new SessionScoringConfig(dimensions);
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void SessionScoringConfig_DimensionTooLong_ThrowsValidationException()
    {
        // Arrange
        var longDimension = new string('x', 51);
        var dimensions = new List<string> { longDimension };

        // Act & Assert
        var act = () =>
            new SessionScoringConfig(dimensions);
        var exception = act.Should().Throw<ValidationException>().Which;

        exception.Message.Should().ContainEquivalentOf("cannot exceed 50");
    }

    [Fact]
    public void CreateDefault_CreatesPointsConfig()
    {
        // Act
        var config = SessionScoringConfig.CreateDefault();

        // Assert
        config.EnabledDimensions.Should().ContainSingle();
        config.EnabledDimensions.Should().Contain("points");
        config.DimensionUnits["points"].Should().Be("pts");
    }

    [Fact]
    public void HasDimension_ExistingDimension_ReturnsTrue()
    {
        // Arrange
        var config = new SessionScoringConfig(
            new List<string> { "points", "ranking" });

        // Act & Assert
        (config.HasDimension("points")).Should().BeTrue();
        (config.HasDimension("ranking")).Should().BeTrue();
        (config.HasDimension("POINTS")).Should().BeTrue();  // Case-insensitive
    }

    [Fact]
    public void HasDimension_NonExistingDimension_ReturnsFalse()
    {
        // Arrange
        var config = SessionScoringConfig.CreateDefault();

        // Act & Assert
        (config.HasDimension("ranking")).Should().BeFalse();
        (config.HasDimension("wins")).Should().BeFalse();
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
        config2.Should().Be(config1);  // Order doesn't matter
        config3.Should().NotBe(config1);  // Different dimensions
    }
}
