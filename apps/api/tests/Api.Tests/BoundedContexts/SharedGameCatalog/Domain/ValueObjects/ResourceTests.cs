using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// Tests for the Resource value object.
/// Issue #3025: Backend 90% Coverage Target - Phase 9
/// </summary>
[Trait("Category", "Unit")]
public sealed class ResourceTests
{
    #region Create Factory Tests

    [Fact]
    public void Create_WithRequiredData_ReturnsResource()
    {
        // Act
        var resource = Resource.Create(
            name: "Wood",
            type: "Material");

        // Assert
        resource.Name.Should().Be("Wood");
        resource.Type.Should().Be("Material");
        resource.Usage.Should().BeNull();
        resource.IsLimited.Should().BeFalse();
    }

    [Fact]
    public void Create_WithAllData_ReturnsResource()
    {
        // Act
        var resource = Resource.Create(
            name: "Gold",
            type: "Currency",
            usage: "Used to purchase buildings and units",
            isLimited: true);

        // Assert
        resource.Name.Should().Be("Gold");
        resource.Type.Should().Be("Currency");
        resource.Usage.Should().Be("Used to purchase buildings and units");
        resource.IsLimited.Should().BeTrue();
    }

    [Fact]
    public void Create_TrimsValues()
    {
        // Act
        var resource = Resource.Create(
            name: "  Stone  ",
            type: "  Material  ",
            usage: "  Build structures  ");

        // Assert
        resource.Name.Should().Be("Stone");
        resource.Type.Should().Be("Material");
        resource.Usage.Should().Be("Build structures");
    }

    [Fact]
    public void Create_WithNullUsage_SetsUsageToNull()
    {
        // Act
        var resource = Resource.Create("Resource", "Type", usage: null);

        // Assert
        resource.Usage.Should().BeNull();
    }

    #endregion

    #region Validation Tests

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyName_ThrowsArgumentException(string? name)
    {
        // Act
        var action = () => Resource.Create(name!, "Type");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Resource name is required*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyType_ThrowsArgumentException(string? type)
    {
        // Act
        var action = () => Resource.Create("Name", type!);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Resource type is required*");
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsNameAndType()
    {
        // Arrange
        var resource = Resource.Create("Wood", "Material");

        // Act
        var result = resource.ToString();

        // Assert
        result.Should().Be("Wood (Material)");
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void TwoResources_WithSameValues_AreEqual()
    {
        // Arrange
        var resource1 = Resource.Create("Wood", "Material", "Build", true);
        var resource2 = Resource.Create("Wood", "Material", "Build", true);

        // Assert
        resource1.Should().Be(resource2);
        resource1.Equals(resource2).Should().BeTrue();
    }

    [Fact]
    public void TwoResources_WithDifferentNames_AreNotEqual()
    {
        // Arrange
        var resource1 = Resource.Create("Wood", "Material");
        var resource2 = Resource.Create("Stone", "Material");

        // Assert
        resource1.Should().NotBe(resource2);
    }

    [Fact]
    public void TwoResources_WithDifferentTypes_AreNotEqual()
    {
        // Arrange
        var resource1 = Resource.Create("Gold", "Currency");
        var resource2 = Resource.Create("Gold", "Material");

        // Assert
        resource1.Should().NotBe(resource2);
    }

    [Fact]
    public void TwoResources_WithDifferentUsage_AreNotEqual()
    {
        // Arrange
        var resource1 = Resource.Create("Wood", "Material", "Build houses");
        var resource2 = Resource.Create("Wood", "Material", "Build ships");

        // Assert
        resource1.Should().NotBe(resource2);
    }

    [Fact]
    public void TwoResources_WithDifferentIsLimited_AreNotEqual()
    {
        // Arrange
        var resource1 = Resource.Create("Wood", "Material", isLimited: false);
        var resource2 = Resource.Create("Wood", "Material", isLimited: true);

        // Assert
        resource1.Should().NotBe(resource2);
    }

    [Fact]
    public void TwoResources_OneWithNullUsageOneWithValue_AreNotEqual()
    {
        // Arrange
        var resource1 = Resource.Create("Wood", "Material", usage: null);
        var resource2 = Resource.Create("Wood", "Material", usage: "Build");

        // Assert
        resource1.Should().NotBe(resource2);
    }

    [Fact]
    public void TwoResources_BothWithNullUsage_AreEqual()
    {
        // Arrange
        var resource1 = Resource.Create("Wood", "Material", usage: null);
        var resource2 = Resource.Create("Wood", "Material", usage: null);

        // Assert
        resource1.Should().Be(resource2);
    }

    [Fact]
    public void TwoResources_WithSameValues_HaveSameHashCode()
    {
        // Arrange
        var resource1 = Resource.Create("Wood", "Material");
        var resource2 = Resource.Create("Wood", "Material");

        // Assert
        resource1.GetHashCode().Should().Be(resource2.GetHashCode());
    }

    #endregion
}
