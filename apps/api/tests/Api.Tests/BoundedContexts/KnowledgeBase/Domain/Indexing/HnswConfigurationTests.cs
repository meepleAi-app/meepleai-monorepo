using Api.BoundedContexts.KnowledgeBase.Domain.Indexing;
using Xunit;
using FluentAssertions;
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
        config.M.Should().Be(16);
        config.EfConstruct.Should().Be(100);
        config.FullScanThreshold.Should().Be(10000);
        config.OnDisk.Should().BeFalse();
    }

    [Fact]
    public void Create_WithValidValues_Succeeds()
    {
        // Act
        var config = HnswConfiguration.Create(m: 32, efConstruct: 200, fullScanThreshold: 5000, onDisk: true);

        // Assert
        config.M.Should().Be(32);
        config.EfConstruct.Should().Be(200);
        config.FullScanThreshold.Should().Be(5000);
        config.OnDisk.Should().BeTrue();
    }

    [Fact]
    public void Create_WithMBelowMin_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert - m must be between 4 and 64
        Action act = () =>
            HnswConfiguration.Create(m: 3, efConstruct: 100);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_WithMAboveMax_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert - m must be between 4 and 64
        Action act = () =>
            HnswConfiguration.Create(m: 65, efConstruct: 100);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_WithEfConstructBelowMin_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert - efConstruct must be between 10 and 500
        Action act = () =>
            HnswConfiguration.Create(m: 16, efConstruct: 9);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_WithEfConstructAboveMax_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert - efConstruct must be between 10 and 500
        Action act = () =>
            HnswConfiguration.Create(m: 16, efConstruct: 501);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_WithNegativeFullScanThreshold_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        Action act = () =>
            HnswConfiguration.Create(m: 16, efConstruct: 100, fullScanThreshold: -1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void HighAccuracy_ReturnsHighAccuracyConfiguration()
    {
        // Act
        var config = HnswConfiguration.HighAccuracy();

        // Assert
        config.M.Should().Be(24);
        config.EfConstruct.Should().Be(200);
        config.OnDisk.Should().BeFalse();
    }

    [Fact]
    public void MemoryOptimized_ReturnsMemoryOptimizedConfiguration()
    {
        // Act
        var config = HnswConfiguration.MemoryOptimized();

        // Assert
        config.M.Should().Be(12);
        config.EfConstruct.Should().Be(64);
        config.OnDisk.Should().BeTrue();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        // Arrange
        var config1 = HnswConfiguration.Create(16, 100, 10000, false);
        var config2 = HnswConfiguration.Create(16, 100, 10000, false);

        // Act & Assert
        config2.Should().Be(config1);
        config1.Equals(config2).Should().BeTrue();
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        // Arrange
        var config1 = HnswConfiguration.Create(16, 100);
        var config2 = HnswConfiguration.Create(32, 100);

        // Act & Assert
        config2.Should().NotBe(config1);
    }

    [Fact]
    public void GetHashCode_SameValues_SameHash()
    {
        // Arrange
        var config1 = HnswConfiguration.Default();
        var config2 = HnswConfiguration.Default();

        // Act & Assert
        config2.GetHashCode().Should().Be(config1.GetHashCode());
    }

    [Fact]
    public void ToString_ReturnsDescriptiveString()
    {
        // Arrange
        var config = HnswConfiguration.Default();

        // Act
        var result = config.ToString();

        // Assert
        result.Should().ContainEquivalentOf("m=16");
        result.Should().ContainEquivalentOf("ef_construct=100");
    }
}
