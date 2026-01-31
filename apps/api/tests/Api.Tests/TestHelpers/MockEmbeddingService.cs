using Api.Services;

namespace Api.Tests.TestHelpers;

/// <summary>
/// Mock implementation of IEmbeddingService for testing.
/// Provides deterministic embeddings based on text content hash.
/// Issue #2599: Centralized mock for AI/embedding tests.
/// </summary>
internal class MockEmbeddingService : IEmbeddingService
{
    private readonly int _dimensions;
    private readonly string _modelName;
    private readonly bool _shouldFail;
    private readonly string? _failureMessage;

    public MockEmbeddingService(
        int dimensions = 384,
        string modelName = "mock-embedding-model",
        bool shouldFail = false,
        string? failureMessage = null)
    {
        _dimensions = dimensions;
        _modelName = modelName;
        _shouldFail = shouldFail;
        _failureMessage = failureMessage;
    }

    public Task<EmbeddingResult> GenerateEmbeddingsAsync(List<string> texts, CancellationToken ct = default)
    {
        if (_shouldFail)
        {
            return Task.FromResult(EmbeddingResult.CreateFailure(_failureMessage ?? "Mock embedding service configured to fail"));
        }

        var embeddings = texts.Select(GenerateDeterministicEmbedding).ToList();
        return Task.FromResult(EmbeddingResult.CreateSuccess(embeddings));
    }

    public Task<EmbeddingResult> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
    {
        if (_shouldFail)
        {
            return Task.FromResult(EmbeddingResult.CreateFailure(_failureMessage ?? "Mock embedding service configured to fail"));
        }

        var embedding = GenerateDeterministicEmbedding(text);
        return Task.FromResult(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));
    }

    public Task<EmbeddingResult> GenerateEmbeddingsAsync(List<string> texts, string language, CancellationToken ct = default)
    {
        // Language parameter ignored in mock - same behavior as standard method
        return GenerateEmbeddingsAsync(texts, ct);
    }

    public Task<EmbeddingResult> GenerateEmbeddingAsync(string text, string language, CancellationToken ct = default)
    {
        // Language parameter ignored in mock - same behavior as standard method
        return GenerateEmbeddingAsync(text, ct);
    }

    public int GetEmbeddingDimensions() => _dimensions;

    public string GetModelName() => _modelName;

    /// <summary>
    /// Generates deterministic embeddings based on text content hash.
    /// Same text always produces same embedding vector.
    /// </summary>
    private float[] GenerateDeterministicEmbedding(string text)
    {
        // Use text hash for deterministic values
        var hash = StringComparer.Ordinal.GetHashCode(text);
#pragma warning disable CA5394 // Random is acceptable in test mocks for deterministic output
        var random = new Random(hash);

        // Generate normalized embedding vector
        var embedding = new float[_dimensions];
        for (var i = 0; i < _dimensions; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2 - 1); // Range: -1 to 1
        }
#pragma warning restore CA5394

        // Normalize to unit vector (common in embedding models)
        var magnitude = Math.Sqrt(embedding.Sum(x => x * x));
        if (magnitude > 0)
        {
            for (var i = 0; i < _dimensions; i++)
            {
                embedding[i] /= (float)magnitude;
            }
        }

        return embedding;
    }

    /// <summary>
    /// Creates a mock with failure configured.
    /// </summary>
    public static MockEmbeddingService CreateFailingMock(string errorMessage = "Mock embedding failure")
    {
        return new MockEmbeddingService(shouldFail: true, failureMessage: errorMessage);
    }

    /// <summary>
    /// Creates a mock with custom dimensions.
    /// </summary>
    public static MockEmbeddingService CreateWithDimensions(int dimensions)
    {
        return new MockEmbeddingService(dimensions: dimensions);
    }
}
