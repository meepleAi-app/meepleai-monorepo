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
internal class EmbeddingService : IEmbeddingService
{
    private readonly IEmbeddingProvider _primaryProvider;
    private readonly IEmbeddingProvider? _fallbackProvider;
    private readonly ILogger<EmbeddingService> _logger;
    private readonly EmbeddingConfiguration _config;
    private readonly IConfigurationService _configurationService;

    /// <summary>
    /// Runtime switch for the e5 <c>query:</c> prefix (#3737). Absent row = off, so deploying the
    /// code changes nothing until someone turns it on. DB-backed and cached 5 minutes by
    /// <see cref="IConfigurationService"/>, which is what makes a red gate a config flip instead of
    /// a revert — the previous attempt cost exactly that (#3747) plus a redeploy.
    /// </summary>
    internal const string E5QueryPrefixEnabledKey = "Embedding:E5QueryPrefixEnabled";

    public EmbeddingService(
        IEmbeddingProviderFactory providerFactory,
        IOptions<EmbeddingConfiguration> config,
        ILogger<EmbeddingService> logger,
        IConfigurationService configurationService)
    {
        // S1450: providerFactory used only locally for initialization
        ArgumentNullException.ThrowIfNull(providerFactory);
        ArgumentNullException.ThrowIfNull(config);
        _config = config.Value;
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        ArgumentNullException.ThrowIfNull(configurationService);
        _configurationService = configurationService;

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
    /// Language sent to the provider when a purpose-aware caller does not specify one. Matches
    /// what <c>HttpEmbeddingProvider</c> already hard-coded on its no-language path.
    /// </summary>
    private const string DefaultLanguage = "en";

    /// <summary>
    /// Generate embeddings for a list of text chunks.
    /// Implies <see cref="EmbeddingPurpose.Passage"/> — see #3737 and the purpose-aware overload.
    /// </summary>
    public Task<EmbeddingResult> GenerateEmbeddingsAsync(
        List<string> texts,
        CancellationToken ct = default)
        => GenerateWithFallbackAsync(
            texts,
            (provider, token) => provider.GenerateBatchEmbeddingsAsync(texts, token),
            "embedding generation",
            ct);

    /// <summary>
    /// Generate embedding for a single text.
    /// Implies <see cref="EmbeddingPurpose.Passage"/> — see #3737.
    /// </summary>
    public virtual Task<EmbeddingResult> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
        => GenerateEmbeddingsAsync(new List<string> { text }, ct);

    /// <inheritdoc />
    public async Task<EmbeddingResult> GenerateEmbeddingsAsync(
        List<string> texts,
        EmbeddingPurpose purpose,
        CancellationToken ct = default)
    {
        var effective = await ResolvePurposeAsync(purpose).ConfigureAwait(false);

        return await GenerateWithFallbackAsync(
            texts,
            (provider, token) => provider.GenerateBatchEmbeddingsAsync(texts, DefaultLanguage, effective, token),
            $"embedding generation for purpose {effective}",
            ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<EmbeddingResult> GenerateEmbeddingAsync(
        string text,
        EmbeddingPurpose purpose,
        CancellationToken ct = default)
        => GenerateEmbeddingsAsync(new List<string> { text }, purpose, ct);

    /// <inheritdoc />
    public async Task<EmbeddingResult> GenerateEmbeddingAsync(
        string text,
        string language,
        EmbeddingPurpose purpose,
        CancellationToken ct = default)
    {
        if (!IsValidLanguage(language))
        {
            _logger.LogWarning("Unsupported language code: {Language}, falling back to 'en'", language);
            language = DefaultLanguage;
        }

        var texts = new List<string> { text };
        var resolvedLanguage = language;
        var effective = await ResolvePurposeAsync(purpose).ConfigureAwait(false);

        return await GenerateWithFallbackAsync(
            texts,
            (provider, token) => provider.GenerateBatchEmbeddingsAsync(texts, resolvedLanguage, effective, token),
            $"embedding generation for language {resolvedLanguage} and purpose {effective}",
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Applies the runtime switch of <see cref="E5QueryPrefixEnabledKey"/> to a requested purpose.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only <see cref="EmbeddingPurpose.Query"/> is gated, and the ingestion path never reaches the
    /// configuration at all: it runs in batches, and a per-chunk read would be a cost paid for a
    /// decision that does not concern it. More importantly, a chunk encoded <c>query:</c> would
    /// require a full re-bake — the worst damage available on this path — so ingestion stays
    /// unconditional by construction rather than by a correctly-set flag.
    /// </para>
    /// <para>
    /// <b>On is the default since 2026-08-25.</b> Off was the fail-safe while the rollout was
    /// undecided: a missing row resolved to the pre-#3737 behaviour, so deploying the code changed
    /// nothing until someone turned it on deliberately. The rollout is now decided and measured —
    /// the prefix plus the per-language correction (#3740, #3764) score 10/11 on the gate against
    /// 9/11 without — and with the old default that decision lived only in two hand-seeded rows:
    /// the staging database, and the gate's own seeding step. A recreated database silently
    /// reverted to the encoding the e5 model card calls wrong, and the gate stayed green because it
    /// writes its own row.
    /// </para>
    /// <para>
    /// Reversibility is unchanged, which was the point of having a switch at all: a row set to
    /// <c>false</c> still turns the prefix off without a code revert or a redeploy.
    /// </para>
    /// </remarks>
    private async Task<EmbeddingPurpose> ResolvePurposeAsync(EmbeddingPurpose requested)
    {
        if (requested != EmbeddingPurpose.Query)
        {
            return requested;
        }

        var enabled = await _configurationService
            .GetValueAsync<bool?>(E5QueryPrefixEnabledKey, defaultValue: true)
            .ConfigureAwait(false);

        // `enabled != false` e non `== true`: un valore nullo restituito dallo store — che
        // GetValueAsync produce solo se qualcuno vi scrive un null esplicito — deve seguire il
        // default appena dichiarato, non ricadere sul ramo opposto.
        return enabled != false ? EmbeddingPurpose.Query : EmbeddingPurpose.Passage;
    }

    /// <summary>
    /// Generate embeddings for texts with language-specific model selection and fallback chain.
    /// AI-09: Multi-language embedding support.
    /// </summary>
    public Task<EmbeddingResult> GenerateEmbeddingsAsync(
        List<string> texts,
        string language,
        CancellationToken ct = default)
    {
        // Validate language code
        if (!IsValidLanguage(language))
        {
            _logger.LogWarning("Unsupported language code: {Language}, falling back to 'en'", language);
            language = DefaultLanguage;
        }

        // For multilingual support, prefer HuggingFace BGE-M3 if available
        if (_config.Provider == EmbeddingProviderType.HuggingFaceBgeM3 ||
            _config.FallbackProvider == EmbeddingProviderType.HuggingFaceBgeM3)
        {
            _logger.LogInformation(
                "Using multilingual-aware provider for language {Language}",
                language);
        }

        var resolvedLanguage = language;

        return GenerateWithFallbackAsync(
            texts,
            (provider, token) => provider.GenerateBatchEmbeddingsAsync(texts, resolvedLanguage, token),
            $"embedding generation for language {resolvedLanguage}",
            ct);
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
    /// Primary-then-fallback provider call, shared by every public overload.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This body existed twice — once per overload — before #3737, which is how the two paths
    /// could send differently-shaped requests to the same service. Adding <c>purpose</c> would
    /// have made it four copies, so it is extracted instead.
    /// </para>
    /// <para>
    /// Each overload passes its own <paramref name="call"/> rather than letting this method pick
    /// a provider overload: the pre-#3737 paths must keep hitting the exact provider method they
    /// hit before (2-arg or 3-arg), because <c>IEmbeddingProvider</c>'s purpose overload is a
    /// default interface implementation and Moq does not invoke those — routing everything
    /// through it would have moved existing behaviour under test without any caller asking for it.
    /// </para>
    /// </remarks>
    private async Task<EmbeddingResult> GenerateWithFallbackAsync(
        List<string> texts,
        Func<IEmbeddingProvider, CancellationToken, Task<EmbeddingProviderResult>> call,
        string operationLabel,
        CancellationToken ct)
    {
        if (texts == null || texts.Count == 0)
        {
            return EmbeddingResult.CreateFailure("No texts provided");
        }

        try
        {
            // Try primary provider
            var result = await call(_primaryProvider, ct).ConfigureAwait(false);

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

                var fallbackResult = await call(_fallbackProvider, ct).ConfigureAwait(false);

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
#pragma warning disable S125 // Sections of code should not be commented out
        // SERVICE BOUNDARY: Wraps multi-provider embedding failures (network, API errors, timeouts) into domain-friendly EmbeddingResult
#pragma warning restore S125
        catch (Exception ex)
        {
            return RagExceptionHandler.HandleServiceException(
                ex, _logger, operationLabel,
                errorMessage => EmbeddingResult.CreateFailure(errorMessage));
        }
#pragma warning restore CA1031
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
internal record EmbeddingResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public List<float[]> Embeddings { get; init; } = new();

    public static EmbeddingResult CreateSuccess(List<float[]> embeddings) =>
        new() { Success = true, Embeddings = embeddings };

    public static EmbeddingResult CreateFailure(string error) =>
        new() { Success = false, ErrorMessage = error };
}
