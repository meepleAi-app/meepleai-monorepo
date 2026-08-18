using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Api.Services;

namespace Api.DevTools.MockImpls;

/// <summary>
/// Deterministic mock of IEmbeddingService.
/// Generates 768-dim normalized vectors from hash(language+text).
/// Stable across calls for identical inputs; no external HTTP calls.
/// </summary>
internal sealed class MockEmbeddingService : IEmbeddingService
{
    private const int Dimensions = 768;
    private const string ModelName = "mock-e5-base";

    /// <inheritdoc />
    public int GetEmbeddingDimensions() => Dimensions;

    /// <inheritdoc />
    public string GetModelName() => ModelName;

    /// <inheritdoc />
    public Task<EmbeddingResult> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
        => GenerateEmbeddingAsync(text, "en", ct);

    /// <inheritdoc />
    public Task<EmbeddingResult> GenerateEmbeddingAsync(string text, string language, CancellationToken ct = default)
    {
        var vec = HashToVector(language + ":" + text);
        return Task.FromResult(EmbeddingResult.CreateSuccess(new List<float[]> { vec }));
    }

    /// <inheritdoc />
    public Task<EmbeddingResult> GenerateEmbeddingsAsync(List<string> texts, CancellationToken ct = default)
        => GenerateEmbeddingsAsync(texts, "en", ct);

    /// <inheritdoc />
    public Task<EmbeddingResult> GenerateEmbeddingsAsync(List<string> texts, string language, CancellationToken ct = default)
    {
        var vectors = texts.Select(t => HashToVector(language + ":" + t)).ToList();
        return Task.FromResult(EmbeddingResult.CreateSuccess(vectors));
    }

    private static float[] HashToVector(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var vec = new float[Dimensions];
        for (int i = 0; i < Dimensions; i++)
        {
            var b = bytes[i % bytes.Length];
            vec[i] = (b / 255f) * 2f - 1f;
        }

        // L2 normalize to unit length
        double norm = Math.Sqrt(vec.Sum(v => (double)v * v));
        if (norm > 0)
        {
            for (int i = 0; i < Dimensions; i++)
            {
                vec[i] = (float)(vec[i] / norm);
            }
        }

        return vec;
    }
}
