using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Represents an embedding vector value object.
/// Ensures vector dimension validity and provides similarity operations.
/// </summary>
public sealed class Vector : ValueObject
{
    public float[] Values { get; }
    public int Dimensions => Values.Length;

    public Vector(float[] values)
    {
        if (values == null || values.Length == 0)
            throw new ValidationException(nameof(Vector), "Vector cannot be empty");

        if (values.Any(v => float.IsNaN(v) || float.IsInfinity(v)))
            throw new ValidationException(nameof(Vector), "Vector contains invalid values (NaN or Infinity)");

        Values = values;
    }

    /// <summary>
    /// Creates a placeholder vector for search results where the actual vector values are not needed.
    /// Use this when mapping Qdrant search results that don't include vector data.
    /// </summary>
    /// <param name="dimensions">The vector dimensions (default: 768 for nomic-embed-text)</param>
    public static Vector CreatePlaceholder(int dimensions = 768)
    {
        return new Vector(new float[dimensions]);
    }

    /// <summary>
    /// Calculates cosine similarity between this vector and another.
    /// Returns value between -1 (opposite) and 1 (identical).
    /// </summary>
    public double CosineSimilarity(Vector other)
    {
        if (other.Dimensions != Dimensions)
            throw new ArgumentException($"Vector dimensions must match. Expected {Dimensions}, got {other.Dimensions}", nameof(other));

        double dotProduct = 0;
        double magnitudeA = 0;
        double magnitudeB = 0;

        for (int i = 0; i < Dimensions; i++)
        {
            dotProduct += Values[i] * other.Values[i];
            magnitudeA += Values[i] * Values[i];
            magnitudeB += other.Values[i] * other.Values[i];
        }

        var magnitude = Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB);
        return magnitude > 0 ? dotProduct / magnitude : 0;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        // Compare vectors element by element
        foreach (var value in Values)
        {
            yield return value;
        }
    }

    public override string ToString() => $"Vector({Dimensions}D)";
}
