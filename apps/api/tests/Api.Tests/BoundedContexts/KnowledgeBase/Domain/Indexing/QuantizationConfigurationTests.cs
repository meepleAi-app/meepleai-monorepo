using Api.BoundedContexts.KnowledgeBase.Domain.Indexing;
using Xunit;
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
        Assert.Equal(QuantizationType.Int8, config.Type);
        Assert.Equal(0.99, config.Quantile);
        Assert.True(config.AlwaysRam);
        Assert.True(config.IsEnabled);
    }

    [Fact]
    public void Disabled_ReturnsDisabledConfiguration()
    {
        // Act
        var config = QuantizationConfiguration.Disabled();

        // Assert
        Assert.Equal(QuantizationType.None, config.Type);
        Assert.False(config.IsEnabled);
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
        Assert.Equal(QuantizationType.Int8, config.Type);
        Assert.Equal(0.95, config.Quantile);
        Assert.False(config.AlwaysRam);
        Assert.True(config.IsEnabled);
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
        Assert.Equal(QuantizationType.Binary, config.Type);
        Assert.True(config.AlwaysRam);
        Assert.True(config.IsEnabled);
    }

    [Fact]
    public void Create_WithQuantileAboveMax_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert - quantile must be between 0.9 and 0.999
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            QuantizationConfiguration.Create(quantile: 1.0));
    }

    [Fact]
    public void Create_WithQuantileBelowMin_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            QuantizationConfiguration.Create(quantile: 0.8));
    }

    [Fact]
    public void MemoryOptimized_ReturnsMemoryOptimizedConfiguration()
    {
        // Act
        var config = QuantizationConfiguration.MemoryOptimized();

        // Assert
        Assert.Equal(QuantizationType.Int8, config.Type);
        Assert.Equal(0.95, config.Quantile);
        Assert.False(config.AlwaysRam);
    }

    [Fact]
    public void HighAccuracy_ReturnsHighAccuracyConfiguration()
    {
        // Act
        var config = QuantizationConfiguration.HighAccuracy();

        // Assert
        Assert.Equal(QuantizationType.Int8, config.Type);
        Assert.Equal(0.999, config.Quantile);
        Assert.True(config.AlwaysRam);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        // Arrange
        var config1 = QuantizationConfiguration.Create(QuantizationType.Int8, 0.99, true);
        var config2 = QuantizationConfiguration.Create(QuantizationType.Int8, 0.99, true);

        // Act & Assert
        Assert.Equal(config1, config2);
        Assert.True(config1.Equals(config2));
    }

    [Fact]
    public void Equality_DifferentTypes_AreNotEqual()
    {
        // Arrange
        var config1 = QuantizationConfiguration.Create(QuantizationType.Int8);
        var config2 = QuantizationConfiguration.Create(QuantizationType.Binary);

        // Act & Assert
        Assert.NotEqual(config1, config2);
    }

    [Fact]
    public void GetHashCode_SameValues_SameHash()
    {
        // Arrange
        var config1 = QuantizationConfiguration.Default();
        var config2 = QuantizationConfiguration.Default();

        // Act & Assert
        Assert.Equal(config1.GetHashCode(), config2.GetHashCode());
    }

    [Fact]
    public void ToString_ReturnsDescriptiveString()
    {
        // Arrange
        var config = QuantizationConfiguration.Default();

        // Act
        var result = config.ToString();

        // Assert
        Assert.Contains("Int8", result);
        // Quantile can be formatted with '.' or ',' depending on locale
        Assert.True(result.Contains("0.99") || result.Contains("0,99"),
            $"Expected quantile value in ToString output: {result}");
    }

    [Fact]
    public void ToString_Disabled_ReturnsDisabledString()
    {
        // Arrange
        var config = QuantizationConfiguration.Disabled();

        // Act
        var result = config.ToString();

        // Assert
        Assert.Contains("disabled", result);
    }
}
