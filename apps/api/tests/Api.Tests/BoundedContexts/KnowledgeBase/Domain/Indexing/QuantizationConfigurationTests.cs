using Api.BoundedContexts.KnowledgeBase.Domain.Indexing;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Indexing;

/// <summary>
/// ADR-016 Phase 3: Unit tests for QuantizationConfiguration value object.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class QuantizationConfigurationTests
{
    [Fact]
    public void Default_ReturnsAdr016Phase3Settings()
    {
        // Act
        var config = QuantizationConfiguration.Default();

        // Assert - ADR-016 Phase 3 specified values
        config.Type.Should().Be(QuantizationType.Int8);
        config.Quantile.Should().Be(0.99);
        config.AlwaysRam.Should().BeTrue();
        config.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Disabled_ReturnsDisabledConfiguration()
    {
        // Act
        var config = QuantizationConfiguration.Disabled();

        // Assert
        config.Type.Should().Be(QuantizationType.None);
        config.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Create_WithInt8Type_ReturnsInt8Configuration()
    {
        // Act
        var config = QuantizationConfiguration.Create(
            type: QuantizationType.Int8,
            quantile: 0.95,
            alwaysRam: false);

        // Assert
        config.Type.Should().Be(QuantizationType.Int8);
        config.Quantile.Should().Be(0.95);
        config.AlwaysRam.Should().BeFalse();
        config.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Create_WithBinaryType_ReturnsBinaryConfiguration()
    {
        // Act
        var config = QuantizationConfiguration.Create(
            type: QuantizationType.Binary,
            quantile: 0.99,
            alwaysRam: true);

        // Assert
        config.Type.Should().Be(QuantizationType.Binary);
        config.AlwaysRam.Should().BeTrue();
        config.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Create_WithQuantileAboveMax_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert - quantile must be between 0.9 and 0.999
        Action act = () =>
            QuantizationConfiguration.Create(quantile: 1.0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_WithQuantileBelowMin_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        Action act = () =>
            QuantizationConfiguration.Create(quantile: 0.8);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void MemoryOptimized_ReturnsMemoryOptimizedConfiguration()
    {
        // Act
        var config = QuantizationConfiguration.MemoryOptimized();

        // Assert
        config.Type.Should().Be(QuantizationType.Int8);
        config.Quantile.Should().Be(0.95);
        config.AlwaysRam.Should().BeFalse();
    }

    [Fact]
    public void HighAccuracy_ReturnsHighAccuracyConfiguration()
    {
        // Act
        var config = QuantizationConfiguration.HighAccuracy();

        // Assert
        config.Type.Should().Be(QuantizationType.Int8);
        config.Quantile.Should().Be(0.999);
        config.AlwaysRam.Should().BeTrue();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        // Arrange
        var config1 = QuantizationConfiguration.Create(QuantizationType.Int8, 0.99, true);
        var config2 = QuantizationConfiguration.Create(QuantizationType.Int8, 0.99, true);

        // Act & Assert
        config2.Should().Be(config1);
        config1.Equals(config2).Should().BeTrue();
    }

    [Fact]
    public void Equality_DifferentTypes_AreNotEqual()
    {
        // Arrange
        var config1 = QuantizationConfiguration.Create(QuantizationType.Int8);
        var config2 = QuantizationConfiguration.Create(QuantizationType.Binary);

        // Act & Assert
        config2.Should().NotBe(config1);
    }

    [Fact]
    public void GetHashCode_SameValues_SameHash()
    {
        // Arrange
        var config1 = QuantizationConfiguration.Default();
        var config2 = QuantizationConfiguration.Default();

        // Act & Assert
        config2.GetHashCode().Should().Be(config1.GetHashCode());
    }

    [Fact]
    public void ToString_ReturnsDescriptiveString()
    {
        // Arrange
        var config = QuantizationConfiguration.Default();

        // Act
        var result = config.ToString();

        // Assert
        result.Should().ContainEquivalentOf("Int8");
        // Quantile can be formatted with '.' or ',' depending on locale
        (result.Contains("0.99") || result.Contains("0,99")).Should().BeTrue($"Expected quantile value in ToString output: {result}");
    }

    [Fact]
    public void ToString_Disabled_ReturnsDisabledString()
    {
        // Arrange
        var config = QuantizationConfiguration.Disabled();

        // Act
        var result = config.ToString();

        // Assert
        result.Should().ContainEquivalentOf("disabled");
    }
}
