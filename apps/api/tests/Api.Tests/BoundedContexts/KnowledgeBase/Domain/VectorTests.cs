using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain;

public class VectorTests
{
    [Fact]
    public void Vector_Create_WithValidValues_Succeeds()
    {
        // Arrange
        var values = new float[] { 0.1f, 0.2f, 0.3f };

        // Act
        var vector = new Vector(values);

        // Assert
        Assert.Equal(3, vector.Dimensions);
        Assert.Equal(values, vector.Values);
    }

    [Fact]
    public void Vector_Create_WithEmptyArray_ThrowsValidationException()
    {
        // Act & Assert
        Assert.Throws<ValidationException>(() => new Vector(Array.Empty<float>()));
    }

    [Fact]
    public void Vector_Create_WithNaN_ThrowsValidationException()
    {
        // Arrange
        var values = new float[] { 0.1f, float.NaN, 0.3f };

        // Act & Assert
        Assert.Throws<ValidationException>(() => new Vector(values));
    }

    [Fact]
    public void CosineSimilarity_IdenticalVectors_ReturnsOne()
    {
        // Arrange
        var values = new float[] { 1.0f, 0.0f, 0.0f };
        var vector1 = new Vector(values);
        var vector2 = new Vector(values);

        // Act
        var similarity = vector1.CosineSimilarity(vector2);

        // Assert
        Assert.Equal(1.0, similarity, precision: 5);
    }

    [Fact]
    public void CosineSimilarity_OrthogonalVectors_ReturnsZero()
    {
        // Arrange
        var vector1 = new Vector(new float[] { 1.0f, 0.0f });
        var vector2 = new Vector(new float[] { 0.0f, 1.0f });

        // Act
        var similarity = vector1.CosineSimilarity(vector2);

        // Assert
        Assert.Equal(0.0, similarity, precision: 5);
    }

    [Fact]
    public void CosineSimilarity_DifferentDimensions_ThrowsException()
    {
        // Arrange
        var vector1 = new Vector(new float[] { 1.0f, 0.0f });
        var vector2 = new Vector(new float[] { 1.0f, 0.0f, 0.0f });

        // Act & Assert
        Assert.Throws<ArgumentException>(() => vector1.CosineSimilarity(vector2));
    }
}

