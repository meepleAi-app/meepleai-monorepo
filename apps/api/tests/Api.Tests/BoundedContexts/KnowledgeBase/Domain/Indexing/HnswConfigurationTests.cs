using Api.BoundedContexts.KnowledgeBase.Domain.Indexing;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Indexing;

/// <summary>
/// ADR-016 Phase 3: Unit tests for HnswConfiguration value object.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class HnswConfigurationTests
{
    [Fact]
    public void Default_ReturnsAdr016Phase3Settings()
    {
        // Act
        var config = HnswConfiguration.Default();

        // Assert - ADR-016 Phase 3 specified values
        Assert.Equal(16, config.M);
        Assert.Equal(100, config.EfConstruct);
        Assert.Equal(10000, config.FullScanThreshold);
        Assert.False(config.OnDisk);
    }

    [Fact]
    public void Create_WithValidValues_Succeeds()
    {
        // Act
        var config = HnswConfiguration.Create(m: 32, efConstruct: 200, fullScanThreshold: 5000, onDisk: true);

        // Assert
        Assert.Equal(32, config.M);
        Assert.Equal(200, config.EfConstruct);
        Assert.Equal(5000, config.FullScanThreshold);
        Assert.True(config.OnDisk);
    }

    [Fact]
    public void Create_WithMBelowMin_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert - m must be between 4 and 64
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HnswConfiguration.Create(m: 3, efConstruct: 100));
    }

    [Fact]
    public void Create_WithMAboveMax_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert - m must be between 4 and 64
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HnswConfiguration.Create(m: 65, efConstruct: 100));
    }

    [Fact]
    public void Create_WithEfConstructBelowMin_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert - efConstruct must be between 10 and 500
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HnswConfiguration.Create(m: 16, efConstruct: 9));
    }

    [Fact]
    public void Create_WithEfConstructAboveMax_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert - efConstruct must be between 10 and 500
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HnswConfiguration.Create(m: 16, efConstruct: 501));
    }

    [Fact]
    public void Create_WithNegativeFullScanThreshold_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HnswConfiguration.Create(m: 16, efConstruct: 100, fullScanThreshold: -1));
    }

    [Fact]
    public void HighAccuracy_ReturnsHighAccuracyConfiguration()
    {
        // Act
        var config = HnswConfiguration.HighAccuracy();

        // Assert
        Assert.Equal(24, config.M);
        Assert.Equal(200, config.EfConstruct);
        Assert.False(config.OnDisk);
    }

    [Fact]
    public void MemoryOptimized_ReturnsMemoryOptimizedConfiguration()
    {
        // Act
        var config = HnswConfiguration.MemoryOptimized();

        // Assert
        Assert.Equal(12, config.M);
        Assert.Equal(64, config.EfConstruct);
        Assert.True(config.OnDisk);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        // Arrange
        var config1 = HnswConfiguration.Create(16, 100, 10000, false);
        var config2 = HnswConfiguration.Create(16, 100, 10000, false);

        // Act & Assert
        Assert.Equal(config1, config2);
        Assert.True(config1.Equals(config2));
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        // Arrange
        var config1 = HnswConfiguration.Create(16, 100);
        var config2 = HnswConfiguration.Create(32, 100);

        // Act & Assert
        Assert.NotEqual(config1, config2);
    }

    [Fact]
    public void GetHashCode_SameValues_SameHash()
    {
        // Arrange
        var config1 = HnswConfiguration.Default();
        var config2 = HnswConfiguration.Default();

        // Act & Assert
        Assert.Equal(config1.GetHashCode(), config2.GetHashCode());
    }

    [Fact]
    public void ToString_ReturnsDescriptiveString()
    {
        // Arrange
        var config = HnswConfiguration.Default();

        // Act
        var result = config.ToString();

        // Assert
        Assert.Contains("m=16", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ef_construct=100", result, StringComparison.OrdinalIgnoreCase);
    }
}
