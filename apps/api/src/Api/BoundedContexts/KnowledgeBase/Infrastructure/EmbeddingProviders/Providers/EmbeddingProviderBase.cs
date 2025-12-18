using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.EmbeddingProviders.Providers;

/// <summary>
/// Base class for embedding providers with common functionality.
/// </summary>
internal abstract class EmbeddingProviderBase : IEmbeddingProvider
{
    protected readonly HttpClient HttpClient;
    protected readonly ILogger Logger;
    protected readonly EmbeddingConfiguration Config;

    protected EmbeddingProviderBase(
        HttpClient httpClient,
        ILogger logger,
        EmbeddingConfiguration config)
    {
        HttpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public abstract string ProviderName { get; }
    public abstract string ModelName { get; }
    public abstract int Dimensions { get; }
    public abstract int MaxContextTokens { get; }

    public async Task<EmbeddingProviderResult> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return EmbeddingProviderResult.CreateFailure("Text cannot be empty");
        }

        return await GenerateBatchEmbeddingsAsync(new[] { text }, cancellationToken).ConfigureAwait(false);
    }

    public abstract Task<EmbeddingProviderResult> GenerateBatchEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default);

    public virtual async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Default health check: try to generate a small embedding
            var result = await GenerateEmbeddingAsync("health check", cancellationToken).ConfigureAwait(false);
            return result.Success;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Health check failed for {Provider}", ProviderName);
            return false;
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Validate that embeddings have correct dimensions
    /// </summary>
    protected bool ValidateEmbeddingDimensions(float[] embedding)
    {
        if (embedding == null || embedding.Length == 0)
        {
            Logger.LogWarning("Empty embedding received from {Provider}", ProviderName);
            return false;
        }

        if (embedding.Length != Dimensions)
        {
            Logger.LogWarning(
                "Unexpected embedding dimensions from {Provider}: expected {Expected}, got {Actual}",
                ProviderName, Dimensions, embedding.Length);
            // Don't fail - some models have variable dimensions
        }

        // Check for NaN or Infinity values
        for (int i = 0; i < embedding.Length; i++)
        {
            if (float.IsNaN(embedding[i]) || float.IsInfinity(embedding[i]))
            {
                Logger.LogWarning("Invalid embedding value at index {Index} from {Provider}", i, ProviderName);
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Split texts into batches respecting BatchSize configuration
    /// </summary>
    protected IEnumerable<IReadOnlyList<string>> BatchTexts(IReadOnlyList<string> texts)
    {
        // FIX: Ensure batchSize is at least 1 to prevent infinite loop if config is invalid
        var batchSize = Math.Max(1, Config.BatchSize);
        for (int i = 0; i < texts.Count; i += batchSize)
        {
            yield return texts.Skip(i).Take(batchSize).ToList();
        }
    }
}

