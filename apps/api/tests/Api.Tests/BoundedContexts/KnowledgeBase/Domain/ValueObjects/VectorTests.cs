using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Tests for the Vector value object.
/// Issue #3025: Backend 90% Coverage Target - Phase 18
/// </summary>
[Trait("Category", "Unit")]
public sealed class VectorTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidValues_CreatesVector()
    {
        // Arrange
        var values = new[] { 1.0f, 2.0f, 3.0f };

        // Act
        var vector = new Vector(values);

        // Assert
        vector.Values.Should().BeEquivalentTo(values);
        vector.Dimensions.Should().Be(3);
    }

    [Fact]
    public void Constructor_WithSingleValue_CreatesVector()
    {
        // Arrange
        var values = new[] { 1.0f };

        // Act
        var vector = new Vector(values);

        // Assert
        vector.Dimensions.Should().Be(1);
    }

    [Fact]
    public void Constructor_WithNullValues_ThrowsValidationException()
    {
        // Act
        var action = () => new Vector(null!);

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("*Vector cannot be empty*");
    }

    [Fact]
    public void Constructor_WithEmptyValues_ThrowsValidationException()
    {
        // Act
        var action = () => new Vector([]);

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("*Vector cannot be empty*");
    }

    [Fact]
    public void Constructor_WithNaN_ThrowsValidationException()
    {
        // Arrange
        var values = new[] { 1.0f, float.NaN, 3.0f };

        // Act
        var action = () => new Vector(values);

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("*Vector contains invalid values*");
    }

    [Fact]
    public void Constructor_WithPositiveInfinity_ThrowsValidationException()
    {
        // Arrange
        var values = new[] { 1.0f, float.PositiveInfinity, 3.0f };

        // Act
        var action = () => new Vector(values);

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("*Vector contains invalid values*");
    }

    [Fact]
    public void Constructor_WithNegativeInfinity_ThrowsValidationException()
    {
        // Arrange
        var values = new[] { 1.0f, float.NegativeInfinity, 3.0f };

        // Act
        var action = () => new Vector(values);

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("*Vector contains invalid values*");
    }

    [Fact]
    public void Constructor_WithZeroValues_Succeeds()
    {
        // Arrange
        var values = new[] { 0.0f, 0.0f, 0.0f };

        // Act
        var vector = new Vector(values);

        // Assert
        vector.Values.Should().AllSatisfy(v => v.Should().Be(0.0f));
    }

    [Fact]
    public void Constructor_WithNegativeValues_Succeeds()
    {
        // Arrange
        var values = new[] { -1.0f, -2.0f, -3.0f };

        // Act
        var vector = new Vector(values);

        // Assert
        vector.Values.Should().BeEquivalentTo(values);
    }

    #endregion

    #region CreatePlaceholder Tests

    [Fact]
    public void CreatePlaceholder_WithDefaultDimensions_Creates768DVector()
    {
        // Act
        var vector = Vector.CreatePlaceholder();

        // Assert
        vector.Dimensions.Should().Be(768);
        vector.Values.Should().AllSatisfy(v => v.Should().Be(0.0f));
    }

    [Fact]
    public void CreatePlaceholder_WithCustomDimensions_CreatesCorrectVector()
    {
        // Act
        var vector = Vector.CreatePlaceholder(1536);

        // Assert
        vector.Dimensions.Should().Be(1536);
    }

    #endregion

    #region CosineSimilarity Tests

    [Fact]
    public void CosineSimilarity_WithIdenticalVectors_ReturnsOne()
    {
        // Arrange
        var vector1 = new Vector([1.0f, 0.0f, 0.0f]);
        var vector2 = new Vector([1.0f, 0.0f, 0.0f]);

        // Act
        var similarity = vector1.CosineSimilarity(vector2);

        // Assert
        similarity.Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public void CosineSimilarity_WithOrthogonalVectors_ReturnsZero()
    {
        // Arrange
        var vector1 = new Vector([1.0f, 0.0f, 0.0f]);
        var vector2 = new Vector([0.0f, 1.0f, 0.0f]);

        // Act
        var similarity = vector1.CosineSimilarity(vector2);

        // Assert
        similarity.Should().BeApproximately(0.0, 0.001);
    }

    [Fact]
    public void CosineSimilarity_WithOppositeVectors_ReturnsNegativeOne()
    {
        // Arrange
        var vector1 = new Vector([1.0f, 0.0f, 0.0f]);
        var vector2 = new Vector([-1.0f, 0.0f, 0.0f]);

        // Act
        var similarity = vector1.CosineSimilarity(vector2);

        // Assert
        similarity.Should().BeApproximately(-1.0, 0.001);
    }

    [Fact]
    public void CosineSimilarity_WithSimilarVectors_ReturnsHighPositive()
    {
        // Arrange
        var vector1 = new Vector([0.9f, 0.1f, 0.0f]);
        var vector2 = new Vector([0.8f, 0.2f, 0.0f]);

        // Act
        var similarity = vector1.CosineSimilarity(vector2);

        // Assert
        similarity.Should().BeGreaterThan(0.9);
    }

    [Fact]
    public void CosineSimilarity_WithDifferentDimensions_ThrowsArgumentException()
    {
        // Arrange
        var vector1 = new Vector([1.0f, 0.0f, 0.0f]);
        var vector2 = new Vector([1.0f, 0.0f]);

        // Act
        var action = () => vector1.CosineSimilarity(vector2);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Vector dimensions must match*");
    }

    [Fact]
    public void CosineSimilarity_WithZeroVector_ReturnsZero()
    {
        // Arrange
        var vector1 = new Vector([1.0f, 0.0f, 0.0f]);
        var vector2 = new Vector([0.0f, 0.0f, 0.0f]);

        // Act
        var similarity = vector1.CosineSimilarity(vector2);

        // Assert
        similarity.Should().Be(0.0);
    }

    [Fact]
    public void CosineSimilarity_WithScaledVector_ReturnsSameAsOriginal()
    {
        // Arrange - same direction, different magnitude
        var vector1 = new Vector([1.0f, 2.0f, 3.0f]);
        var vector2 = new Vector([2.0f, 4.0f, 6.0f]);

        // Act
        var similarity = vector1.CosineSimilarity(vector2);

        // Assert - cosine similarity ignores magnitude
        similarity.Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public void CosineSimilarity_IsCommutative()
    {
        // Arrange
        var vector1 = new Vector([1.0f, 2.0f, 3.0f]);
        var vector2 = new Vector([4.0f, 5.0f, 6.0f]);

        // Act
        var similarity1 = vector1.CosineSimilarity(vector2);
        var similarity2 = vector2.CosineSimilarity(vector1);

        // Assert
        similarity1.Should().BeApproximately(similarity2, 0.001);
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_WithSameValues_ReturnsTrue()
    {
        // Arrange
        var vector1 = new Vector([1.0f, 2.0f, 3.0f]);
        var vector2 = new Vector([1.0f, 2.0f, 3.0f]);

        // Assert
        vector1.Should().Be(vector2);
    }

    [Fact]
    public void Equals_WithDifferentValues_ReturnsFalse()
    {
        // Arrange
        var vector1 = new Vector([1.0f, 2.0f, 3.0f]);
        var vector2 = new Vector([1.0f, 2.0f, 4.0f]);

        // Assert
        vector1.Should().NotBe(vector2);
    }

    [Fact]
    public void Equals_WithDifferentDimensions_ReturnsFalse()
    {
        // Arrange
        var vector1 = new Vector([1.0f, 2.0f, 3.0f]);
        var vector2 = new Vector([1.0f, 2.0f]);

        // Assert
        vector1.Should().NotBe(vector2);
    }

    [Fact]
    public void GetHashCode_WithSameValues_ReturnsSameHash()
    {
        // Arrange
        var vector1 = new Vector([1.0f, 2.0f, 3.0f]);
        var vector2 = new Vector([1.0f, 2.0f, 3.0f]);

        // Assert
        vector1.GetHashCode().Should().Be(vector2.GetHashCode());
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        // Arrange
        var vector = new Vector([1.0f, 2.0f, 3.0f]);

        // Act
        var result = vector.ToString();

        // Assert
        result.Should().Be("Vector(3D)");
    }

    [Fact]
    public void ToString_WithPlaceholder_ShowsCorrectDimensions()
    {
        // Arrange
        var vector = Vector.CreatePlaceholder(768);

        // Act
        var result = vector.ToString();

        // Assert
        result.Should().Be("Vector(768D)");
    }

    #endregion

    #region Dimensions Tests

    [Theory]
    [InlineData(1)]
    [InlineData(384)]
    [InlineData(768)]
    [InlineData(1536)]
    public void Dimensions_ReturnsCorrectCount(int dimensions)
    {
        // Arrange
        var values = new float[dimensions];

        // Act
        var vector = new Vector(values);

        // Assert
        vector.Dimensions.Should().Be(dimensions);
    }

    #endregion
}
