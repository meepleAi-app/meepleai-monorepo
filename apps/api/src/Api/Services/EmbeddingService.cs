using Api.BoundedContexts.KnowledgeBase.Infrastructure.EmbeddingProviders;
using Api.Helpers;
using Microsoft.Extensions.Options;

#pragma warning disable MA0048 // File name must match type name - Contains Service with Configuration classes
namespace Api.Services;

/// <summary>
/// Service for generating text embeddings using multi-provider abstraction.
/// Supports OpenRouter, Ollama, and HuggingFace with fallback chain.
/// Refactored per ADR-016 Phase 2 to use IEmbeddingProvider abstraction.
/// </summary>
public class EmbeddingService : IEmbeddingService
{
    private readonly IEmbeddingProvider _primaryProvider;
    private readonly IEmbeddingProvider? _fallbackProvider;
    private readonly ILogger<EmbeddingService> _logger;
    private readonly EmbeddingConfiguration _config;

    public EmbeddingService(
        IEmbeddingProviderFactory providerFactory,
        IOptions<EmbeddingConfiguration> config,
        ILogger<EmbeddingService> logger)
    {
        // S1450: providerFactory used only locally for initialization
        ArgumentNullException.ThrowIfNull(providerFactory);
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Initialize providers
        _primaryProvider = providerFactory.GetPrimaryProvider();
        _fallbackProvider = providerFactory.GetFallbackProvider();

        _logger.LogInformation(
            "EmbeddingService initialized with primary provider {Primary} ({Model}, {Dimensions}d){Fallback}",
            _primaryProvider.ProviderName,
            _primaryProvider.ModelName,
            _primaryProvider.Dimensions,
            _fallbackProvider != null ? $", fallback: {_fallbackProvider.ProviderName}" : "");
    }

    /// <summary>
    /// Get the configured embedding dimensions for the current model
    /// </summary>
    public int GetEmbeddingDimensions() => _primaryProvider.Dimensions;

    /// <summary>
    /// Get the configured embedding model name
    /// </summary>
    public string GetModelName() => $"{_primaryProvider.ProviderName.ToLowerInvariant()}/{_primaryProvider.ModelName}";

    /// <summary>
    /// Generate embeddings for a list of text chunks
    /// </summary>
    public async Task<EmbeddingResult> GenerateEmbeddingsAsync(
        List<string> texts,
        CancellationToken ct = default)
    {
        if (texts == null || texts.Count == 0)
        {
            return EmbeddingResult.CreateFailure("No texts provided");
        }

        try
        {
            // Try primary provider
            var result = await _primaryProvider.GenerateBatchEmbeddingsAsync(texts, ct).ConfigureAwait(false);

            if (result.Success)
            {
                return EmbeddingResult.CreateSuccess(result.Embeddings.ToList());
            }

            // Try fallback if configured and primary failed
            if (_fallbackProvider != null && _config.EnableFallback)
            {
                // FIX: Check cancellation before attempting fallback to avoid unnecessary work
                ct.ThrowIfCancellationRequested();

                _logger.LogWarning(
                    "Primary provider {Primary} failed: {Error}. Trying fallback {Fallback}",
                    _primaryProvider.ProviderName,
                    result.ErrorMessage,
                    _fallbackProvider.ProviderName);

                var fallbackResult = await _fallbackProvider.GenerateBatchEmbeddingsAsync(texts, ct).ConfigureAwait(false);

                if (fallbackResult.Success)
                {
                    _logger.LogInformation("Fallback provider {Provider} succeeded", _fallbackProvider.ProviderName);
                    return EmbeddingResult.CreateSuccess(fallbackResult.Embeddings.ToList());
                }

                _logger.LogError(
                    "Fallback provider {Provider} also failed: {Error}",
                    _fallbackProvider.ProviderName,
                    fallbackResult.ErrorMessage);

                // FIX: Include both error messages when both providers fail
                return EmbeddingResult.CreateFailure(
                    $"Primary ({_primaryProvider.ProviderName}): {result.ErrorMessage}; " +
                    $"Fallback ({_fallbackProvider.ProviderName}): {fallbackResult.ErrorMessage}");
            }

            return EmbeddingResult.CreateFailure(result.ErrorMessage ?? "Embedding generation failed");
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            return RagExceptionHandler.HandleServiceException(
                ex, _logger, "embedding generation",
                errorMessage => EmbeddingResult.CreateFailure(errorMessage));
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Generate embedding for a single text
    /// </summary>
    public virtual async Task<EmbeddingResult> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
    {
        var result = await GenerateEmbeddingsAsync(new List<string> { text }, ct).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// Generate embeddings for texts with language-specific model selection and fallback chain.
    /// AI-09: Multi-language embedding support.
    /// </summary>
    public async Task<EmbeddingResult> GenerateEmbeddingsAsync(
        List<string> texts,
        string language,
        CancellationToken ct = default)
    {
        if (texts == null || texts.Count == 0)
        {
            return EmbeddingResult.CreateFailure("No texts provided");
        }

        // Validate language code
        if (!IsValidLanguage(language))
        {
            _logger.LogWarning("Unsupported language code: {Language}, falling back to 'en'", language);
            language = "en";
        }

        try
        {
            // For multilingual support, prefer HuggingFace BGE-M3 if available
            if (_config.Provider == EmbeddingProviderType.HuggingFaceBgeM3 ||
                _config.FallbackProvider == EmbeddingProviderType.HuggingFaceBgeM3)
            {
                _logger.LogInformation(
                    "Using multilingual-aware provider for language {Language}",
                    language);
            }

            // Use standard embedding generation (providers handle language internally if supported)
            return await GenerateEmbeddingsAsync(texts, ct).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            return RagExceptionHandler.HandleServiceException(
                ex, _logger, $"embedding generation for language {language}",
                errorMessage => EmbeddingResult.CreateFailure(errorMessage));
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Generate embedding for a single text with language-specific model
    /// </summary>
    public async Task<EmbeddingResult> GenerateEmbeddingAsync(
        string text,
        string language,
        CancellationToken ct = default)
    {
        return await GenerateEmbeddingsAsync(new List<string> { text }, language, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Validate language code
    /// </summary>
    private static bool IsValidLanguage(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
            return false;

        var supportedLanguages = new[] { "en", "it", "de", "fr", "es" };
        return supportedLanguages.Contains(languageCode.ToLowerInvariant(), StringComparer.Ordinal);
    }
}

/// <summary>
/// Result of embedding generation
/// </summary>
public record EmbeddingResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public List<float[]> Embeddings { get; init; } = new();

    public static EmbeddingResult CreateSuccess(List<float[]> embeddings) =>
        new() { Success = true, Embeddings = embeddings };

    public static EmbeddingResult CreateFailure(string error) =>
        new() { Success = false, ErrorMessage = error };
}
