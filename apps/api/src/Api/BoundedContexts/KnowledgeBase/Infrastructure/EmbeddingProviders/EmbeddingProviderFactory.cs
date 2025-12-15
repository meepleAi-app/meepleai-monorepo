using Api.BoundedContexts.KnowledgeBase.Infrastructure.EmbeddingProviders.Providers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

#pragma warning disable MA0048 // File name must match type name - Contains Interface with supporting types
namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.EmbeddingProviders;

/// <summary>
/// Factory for creating embedding providers based on configuration.
/// Supports primary provider with optional fallback.
/// </summary>
internal sealed class EmbeddingProviderFactory : IEmbeddingProviderFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly EmbeddingConfiguration _config;

    public EmbeddingProviderFactory(
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory,
        IOptions<EmbeddingConfiguration> config)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Get the primary embedding provider based on configuration
    /// </summary>
    public IEmbeddingProvider GetPrimaryProvider()
    {
        return CreateProvider(_config.Provider);
    }

    /// <summary>
    /// Get the fallback embedding provider if configured
    /// </summary>
    public IEmbeddingProvider? GetFallbackProvider()
    {
        if (!_config.EnableFallback || !_config.FallbackProvider.HasValue)
        {
            return null;
        }

        return CreateProvider(_config.FallbackProvider.Value);
    }

    /// <summary>
    /// Create a specific provider by type
    /// </summary>
    public IEmbeddingProvider CreateProvider(EmbeddingProviderType type)
    {
        return type switch
        {
            EmbeddingProviderType.OpenRouterLarge =>
                CreateOpenRouterProvider(EmbeddingProviderType.OpenRouterLarge),

            EmbeddingProviderType.OpenRouterSmall =>
                CreateOpenRouterProvider(EmbeddingProviderType.OpenRouterSmall),

            EmbeddingProviderType.OllamaNomic =>
                CreateOllamaProvider(EmbeddingProviderType.OllamaNomic),

            EmbeddingProviderType.OllamaMxbai =>
                CreateOllamaProvider(EmbeddingProviderType.OllamaMxbai),

            EmbeddingProviderType.HuggingFaceBgeM3 =>
                CreateHuggingFaceProvider(),

            _ => throw new ArgumentException($"Unknown provider type: {type}", nameof(type))
        };
    }

    private OpenRouterEmbeddingProvider CreateOpenRouterProvider(EmbeddingProviderType type)
    {
        var httpClient = _httpClientFactory.CreateClient("OpenRouter");
        var logger = _loggerFactory.CreateLogger<OpenRouterEmbeddingProvider>();
        return new OpenRouterEmbeddingProvider(httpClient, logger, _config, type);
    }

    private OllamaEmbeddingProvider CreateOllamaProvider(EmbeddingProviderType type)
    {
        var httpClient = _httpClientFactory.CreateClient("Ollama");
        var logger = _loggerFactory.CreateLogger<OllamaEmbeddingProvider>();
        return new OllamaEmbeddingProvider(httpClient, logger, _config, type);
    }

    private HuggingFaceEmbeddingProvider CreateHuggingFaceProvider()
    {
        var httpClient = _httpClientFactory.CreateClient("HuggingFace");
        var logger = _loggerFactory.CreateLogger<HuggingFaceEmbeddingProvider>();
        return new HuggingFaceEmbeddingProvider(httpClient, logger, _config);
    }
}

/// <summary>
/// Interface for embedding provider factory
/// </summary>
internal interface IEmbeddingProviderFactory
{
    /// <summary>
    /// Get the primary embedding provider based on configuration
    /// </summary>
    IEmbeddingProvider GetPrimaryProvider();

    /// <summary>
    /// Get the fallback embedding provider if configured
    /// </summary>
    IEmbeddingProvider? GetFallbackProvider();

    /// <summary>
    /// Create a specific provider by type
    /// </summary>
    IEmbeddingProvider CreateProvider(EmbeddingProviderType type);
}
