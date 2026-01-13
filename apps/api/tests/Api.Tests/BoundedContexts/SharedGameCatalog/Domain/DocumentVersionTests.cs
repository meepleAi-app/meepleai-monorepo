using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain;

[Trait("Category", TestCategories.Unit)]
public class DocumentVersionTests
{
    [Theory]
    [InlineData("1.0")]
    [InlineData("2.5")]
    [InlineData("10.99")]
    [InlineData("0.1")]
    public void Create_WithValidVersion_CreatesSuccessfully(string version)
    {
        // Act
        var docVersion = DocumentVersion.Create(version);

        // Assert
        Assert.Equal(version, docVersion.Value);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("1.0.0")]
    [InlineData("invalid")]
    [InlineData("v1.0")]
    [InlineData("1.0-beta")]
    public void Create_WithInvalidVersion_ThrowsException(string version)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => DocumentVersion.Create(version));
    }

    [Fact]
    public void Create_WithEmptyString_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => DocumentVersion.Create(""));
    }

    [Fact]
    public void Create_WithNegativeNumbers_ThrowsException()
    {
        // This is prevented by the regex, but test it anyway
        // Act & Assert
        Assert.Throws<ArgumentException>(() => DocumentVersion.Create("-1.0"));
    }

    [Fact]
    public void Default_ReturnsVersion1_0()
    {
        // Act
        var version = DocumentVersion.Default;

        // Assert
        Assert.Equal("1.0", version.Value);
        Assert.Equal(1, version.Major);
        Assert.Equal(0, version.Minor);
    }

    [Fact]
    public void CompareTo_WithOlderVersion_ReturnsPositive()
    {
        // Arrange
        var v2_0 = DocumentVersion.Create("2.0");
        var v1_5 = DocumentVersion.Create("1.5");

        // Act
        var result = v2_0.CompareTo(v1_5);

        // Assert
        Assert.True(result > 0);
    }

    [Fact]
    public void CompareTo_WithNewerVersion_ReturnsNegative()
    {
        // Arrange
        var v1_0 = DocumentVersion.Create("1.0");
        var v2_0 = DocumentVersion.Create("2.0");

        // Act
        var result = v1_0.CompareTo(v2_0);

        // Assert
        Assert.True(result < 0);
    }

    [Fact]
    public void CompareTo_WithSameVersion_ReturnsZero()
    {
        // Arrange
        var v1 = DocumentVersion.Create("1.5");
        var v2 = DocumentVersion.Create("1.5");

        // Act
        var result = v1.CompareTo(v2);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void IsNewerThan_WithOlderVersion_ReturnsTrue()
    {
        // Arrange
        var v2_0 = DocumentVersion.Create("2.0");
        var v1_0 = DocumentVersion.Create("1.0");

        // Act & Assert
        Assert.True(v2_0.IsNewerThan(v1_0));
        Assert.False(v1_0.IsNewerThan(v2_0));
    }

    [Fact]
    public void Equality_WithSameVersion_AreEqual()
    {
        // Arrange
        var v1 = DocumentVersion.Create("1.5");
        var v2 = DocumentVersion.Create("1.5");

        // Act & Assert
        Assert.Equal(v1, v2);
        Assert.True(v1 == v2);
        Assert.False(v1 != v2);
    }

    [Fact]
    public void Equality_WithDifferentVersions_AreNotEqual()
    {
        // Arrange
        var v1 = DocumentVersion.Create("1.0");
        var v2 = DocumentVersion.Create("2.0");

        // Act & Assert
        Assert.NotEqual(v1, v2);
        Assert.False(v1 == v2);
        Assert.True(v1 != v2);
    }

    [Fact]
    public void ComparisonOperators_WorkCorrectly()
    {
        // Arrange
        var v1_0 = DocumentVersion.Create("1.0");
        var v1_0_copy = DocumentVersion.Create("1.0");
        var v1_5 = DocumentVersion.Create("1.5");
        var v2_0 = DocumentVersion.Create("2.0");

        // Act & Assert
        Assert.True(v1_0 < v1_5);
        Assert.True(v1_5 < v2_0);
        Assert.True(v2_0 > v1_0);
        Assert.True(v1_0 <= v1_0_copy);
        Assert.True(v2_0 >= v1_5);
    }

    [Fact]
    public void ToString_ReturnsVersionString()
    {
        // Arrange
        var version = DocumentVersion.Create("2.5");

        // Act
        var result = version.ToString();

        // Assert
        Assert.Equal("2.5", result);
    }

    [Fact]
    public void ImplicitConversion_ToString_WorksCorrectly()
    {
        // Arrange
        var version = DocumentVersion.Create("1.0");

        // Act
        string versionString = version;

        // Assert
        Assert.Equal("1.0", versionString);
    }

    [Fact]
    public void GetHashCode_ForEqualVersions_AreEqual()
    {
        // Arrange
        var v1 = DocumentVersion.Create("1.0");
        var v2 = DocumentVersion.Create("1.0");

        // Act & Assert
        Assert.Equal(v1.GetHashCode(), v2.GetHashCode());
    }
}
