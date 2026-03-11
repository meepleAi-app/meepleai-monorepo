using System.Globalization;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.LlmManagement;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.Configuration;
using Api.Services;
using Api.Services.LlmClients;
using Microsoft.Extensions.Options;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Issue #5487: Encapsulates all provider selection logic extracted from HybridLlmService.
/// Combines routing strategy, circuit breaker checks, emergency overrides, RPD quota checks,
/// and fallback chain traversal.
/// </summary>
internal sealed class LlmProviderSelector : ILlmProviderSelector
{
    private readonly List<ILlmClient> _clients;
    private readonly ILlmRoutingStrategy _routingStrategy;
    private readonly ICircuitBreakerRegistry _circuitBreakerRegistry;
    private readonly IOptions<AiProviderSettings> _aiSettings;
    private readonly IAiModelConfigurationRepository _modelConfigRepository;
    private readonly IProviderHealthCheckService? _healthCheckService;
    private readonly IFreeModelQuotaTracker? _freeModelQuotaTracker;
    private readonly IEmergencyOverrideService? _emergencyOverrideService;
    private readonly IOpenRouterRateLimitTracker? _rateLimitTracker;
    private readonly IUserRegionDetector? _userRegionDetector;
    private readonly ILogger<LlmProviderSelector> _logger;

    public LlmProviderSelector(
        IEnumerable<ILlmClient> clients,
        ILlmRoutingStrategy routingStrategy,
        ICircuitBreakerRegistry circuitBreakerRegistry,
        IOptions<AiProviderSettings> aiSettings,
        IAiModelConfigurationRepository modelConfigRepository,
        ILogger<LlmProviderSelector> logger,
        IProviderHealthCheckService? healthCheckService = null,
        IFreeModelQuotaTracker? freeModelQuotaTracker = null,
        IEmergencyOverrideService? emergencyOverrideService = null,
        IOpenRouterRateLimitTracker? rateLimitTracker = null,
        IUserRegionDetector? userRegionDetector = null)
    {
        _clients = clients?.ToList() ?? throw new ArgumentNullException(nameof(clients));
        _routingStrategy = routingStrategy ?? throw new ArgumentNullException(nameof(routingStrategy));
        _circuitBreakerRegistry = circuitBreakerRegistry ?? throw new ArgumentNullException(nameof(circuitBreakerRegistry));
        _aiSettings = aiSettings ?? throw new ArgumentNullException(nameof(aiSettings));
        _modelConfigRepository = modelConfigRepository ?? throw new ArgumentNullException(nameof(modelConfigRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _healthCheckService = healthCheckService;
        _freeModelQuotaTracker = freeModelQuotaTracker;
        _emergencyOverrideService = emergencyOverrideService;
        _rateLimitTracker = rateLimitTracker;
        _userRegionDetector = userRegionDetector;

        if (_clients.Count == 0)
        {
            throw new ArgumentException("At least one ILlmClient must be registered", nameof(clients));
        }

        // Initialize circuit breakers for all known providers
        foreach (var client in _clients)
        {
            _circuitBreakerRegistry.Initialize(client.ProviderName);
        }
    }

    /// <inheritdoc/>
    public async Task<ProviderSelectionResult> SelectProviderAsync(
        User? user,
        RagStrategy strategy,
        RequestSource source,
        CancellationToken ct = default)
    {
        LlmRoutingDecision decision;
        ILlmClient? client;

        // Issue #5089: AutomatedTest requests must never consume free OpenRouter quota
        if (source == RequestSource.AutomatedTest)
        {
            var ollamaModelId = await GetDefaultModelForProviderAsync("Ollama", ct).ConfigureAwait(false);
            decision = LlmRoutingDecision.Ollama(ollamaModelId, "AutomatedTest source: always routed to Ollama (free quota bypass)");
            client = GetClientWithCircuitBreaker("Ollama");
        }
        else
        {
            // Issue #3435: Strategy-based routing
            decision = _routingStrategy.SelectProvider(user, strategy);

            // Issue #5476: Emergency override — force all traffic to Ollama
            if (_emergencyOverrideService != null
                && string.Equals(decision.ProviderName, "OpenRouter", StringComparison.Ordinal))
            {
                var forceOllama = await _emergencyOverrideService
                    .IsForceOllamaOnlyAsync(ct).ConfigureAwait(false);
                if (forceOllama)
                {
                    _logger.LogWarning("Emergency override active: force-ollama-only — rerouting from OpenRouter");
                    var ollamaModelId = await GetDefaultModelForProviderAsync("Ollama", ct).ConfigureAwait(false);
                    decision = LlmRoutingDecision.Ollama(ollamaModelId, "Emergency override: force-ollama-only active");
                }
            }

            // Issue #5089: Proactively skip OpenRouter when RPD quota is known-exhausted
            if (string.Equals(decision.ProviderName, "OpenRouter", StringComparison.Ordinal)
                && _freeModelQuotaTracker != null)
            {
                var isExhausted = await _freeModelQuotaTracker
                    .IsRpdExhaustedAsync(decision.ModelId, ct)
                    .ConfigureAwait(false);
                if (isExhausted)
                {
                    _logger.LogWarning(
                        "Free model {Model} RPD quota exhausted — proactively routing to Ollama", decision.ModelId);
                    var ollamaModelId = await GetDefaultModelForProviderAsync("Ollama", ct).ConfigureAwait(false);
                    decision = LlmRoutingDecision.Ollama(ollamaModelId, $"RPD quota exhausted for {decision.ModelId}");
                }
            }

            client = GetClientWithCircuitBreaker(decision.ProviderName);
        }

        if (client == null)
        {
            _logger.LogError(
                "No available provider found (circuit breakers may be open), using first client as fallback");
            client = _clients[0];

            var originalProvider = decision.ProviderName;
            var emergencyModelId = await GetDefaultModelForProviderAsync(client.ProviderName, ct).ConfigureAwait(false);
            decision = new LlmRoutingDecision(
                ProviderName: client.ProviderName,
                ModelId: emergencyModelId,
                Reason: $"Emergency fallback from {originalProvider} (all providers unavailable)");

            _logger.LogWarning(
                "Created emergency fallback decision: {Provider} ({Model})",
                decision.ProviderName, decision.ModelId);
        }
        else if (!string.Equals(client.ProviderName, decision.ProviderName, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Primary provider {Primary} unavailable, using fallback {Fallback}",
                decision.ProviderName, client.ProviderName);

            var fallbackModelId = await GetDefaultModelForProviderAsync(client.ProviderName, ct).ConfigureAwait(false);
            decision = new LlmRoutingDecision(
                ProviderName: client.ProviderName,
                ModelId: fallbackModelId,
                Reason: $"Fallback from {decision.ProviderName} (circuit open or unhealthy)");
        }

        // Issue #27: Attach region hint from request context (populated but not used for routing)
        var userRegion = _userRegionDetector?.DetectRegion();
        if (userRegion is not null)
        {
            decision = decision with { UserRegion = userRegion };
        }

        return new ProviderSelectionResult(client, decision);
    }

    /// <inheritdoc/>
    public async Task<ProviderSelectionResult> GetNextFallbackAsync(
        string failedProvider,
        HashSet<string> attemptedProviders,
        CancellationToken ct = default)
    {
        var fallbackOrder = await BuildFallbackOrderAsync(ct).ConfigureAwait(false);

        foreach (var providerName in fallbackOrder)
        {
            if (attemptedProviders.Contains(providerName))
                continue;

            var fallbackClient = GetClientWithCircuitBreaker(providerName);
            if (fallbackClient != null)
            {
                var modelId = await GetDefaultModelForProviderAsync(fallbackClient.ProviderName, ct)
                    .ConfigureAwait(false);

                var decision = new LlmRoutingDecision(
                    ProviderName: fallbackClient.ProviderName,
                    ModelId: modelId,
                    Reason: $"Fallback from {failedProvider}")
                {
                    // Issue #27: Preserve region hint across fallback decisions
                    UserRegion = _userRegionDetector?.DetectRegion()
                };
                return new ProviderSelectionResult(fallbackClient, decision);
            }
        }

        return ProviderSelectionResult.NoProvider($"No fallback providers available after {failedProvider}");
    }

    /// <inheritdoc/>
    public void RecordSuccess(string providerName, string modelId, long latencyMs, LlmCompletionResult result)
    {
        _circuitBreakerRegistry.RecordSuccess(providerName, latencyMs);

        if (_rateLimitTracker != null)
        {
            _ = _rateLimitTracker.RecordRequestAsync(providerName, modelId, result.Usage.TotalTokens);
        }
    }

    /// <inheritdoc/>
    public void RecordFailure(string providerName, string modelId, long latencyMs, LlmCompletionResult? result = null)
    {
        _circuitBreakerRegistry.RecordFailure(providerName, latencyMs);

        // Issue #5087/#5088: Record rate limit events for future routing avoidance
        if (result != null
            && string.Equals(providerName, "OpenRouter", StringComparison.Ordinal)
            && _freeModelQuotaTracker != null
            && result.Metadata.TryGetValue("rate_limit_type", out var rlTypeStr)
            && Enum.TryParse<RateLimitErrorType>(rlTypeStr, ignoreCase: true, out var rlErrorType))
        {
            result.Metadata.TryGetValue("rate_limit_reset_ms", out var resetMsStr);
            long? resetMs = long.TryParse(resetMsStr, NumberStyles.None, CultureInfo.InvariantCulture, out var resetMsVal)
                ? resetMsVal : null;
            result.Metadata.TryGetValue("rate_limit_model", out var rlModel);
            var modelForTracking = string.IsNullOrEmpty(rlModel) ? modelId : rlModel;
            _ = _freeModelQuotaTracker.RecordRateLimitErrorAsync(modelForTracking, rlErrorType, resetMs);
        }
    }

    /// <inheritdoc/>
    public async Task WarnIfApproachingLimitAsync(string providerName, CancellationToken ct = default)
    {
        if (_rateLimitTracker == null) return;

        var isApproaching = await _rateLimitTracker
            .IsApproachingLimitAsync(providerName, thresholdPercent: 80, ct)
            .ConfigureAwait(false);
        if (isApproaching)
        {
            _logger.LogWarning(
                "Rate limit approaching 80% for provider {Provider}", providerName);
        }
    }

    /// <summary>
    /// Get LLM client with circuit breaker, health check, and enabled flag awareness.
    /// </summary>
    private ILlmClient? GetClientWithCircuitBreaker(string providerName)
    {
        var client = GetClient(providerName);
        if (client == null) return null;

        if (!IsProviderAvailable(client.ProviderName))
        {
            _logger.LogWarning(
                "Provider {Provider} unavailable (circuit open, unhealthy, or disabled). Trying fallback...",
                client.ProviderName);

            var fallbackOrder = (_aiSettings.Value.FallbackChain?.Count ?? 0) > 0
                ? _aiSettings.Value.FallbackChain!.Where(p => !string.Equals(p, client.ProviderName, StringComparison.Ordinal)).Select(p => p!)
                : _clients.Where(c => !string.Equals(c.ProviderName, client.ProviderName, StringComparison.Ordinal)).Select(c => c.ProviderName);

            foreach (var fallbackProviderName in fallbackOrder)
            {
                var fallbackClient = GetClient(fallbackProviderName);
                if (fallbackClient != null && IsProviderAvailable(fallbackClient.ProviderName))
                {
                    _logger.LogInformation(
                        "Using fallback provider {Fallback} ({Order}) - primary {Primary} unavailable",
                        fallbackClient.ProviderName,
                        (_aiSettings.Value.FallbackChain?.Count ?? 0) > 0 ? "from FallbackChain" : "auto-selected",
                        client.ProviderName);
                    return fallbackClient;
                }
            }

            _logger.LogWarning("No fallback provider available, all providers unavailable");
            return null;
        }

        return client;
    }

    /// <summary>
    /// Check if provider is available (circuit breaker + health check + enabled flag).
    /// </summary>
    private bool IsProviderAvailable(string providerName)
    {
        // Check if provider is enabled in configuration
        var settings = _aiSettings.Value;
        if (settings.Providers?.ContainsKey(providerName) is true &&
            !settings.Providers[providerName].Enabled)
        {
            _logger.LogDebug(
                "Provider {Provider} is disabled in configuration", providerName);
            return false;
        }

        // Check circuit breaker
        if (!_circuitBreakerRegistry.AllowsRequests(providerName))
        {
            return false;
        }

        // Check health status
        if (_healthCheckService != null)
        {
            var healthStatus = _healthCheckService.GetProviderHealth(providerName);
            if (healthStatus != null && !healthStatus.IsAvailable())
            {
                _logger.LogDebug(
                    "Provider {Provider} marked unhealthy: {Status}",
                    providerName, healthStatus.GetStatusSummary());
                return false;
            }
        }

        return true;
    }

    private ILlmClient? GetClient(string providerName)
    {
        return _clients.FirstOrDefault(c =>
            c.ProviderName.Equals(providerName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Build fallback order from DB (AiModelConfiguration) with config fallback.
    /// </summary>
    private async Task<List<string>> BuildFallbackOrderAsync(CancellationToken cancellationToken = default)
    {
        var order = new List<string>();

        try
        {
            var activeModels = await _modelConfigRepository.GetActiveAsync(cancellationToken).ConfigureAwait(false);

            if (activeModels.Count > 0)
            {
                var providerOrder = activeModels
                    .GroupBy(m => m.Provider, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(g => g.Min(m => m.Priority))
                    .Select(g => g.Key)
                    .ToList();

                order.AddRange(providerOrder);
                _logger.LogDebug(
                    "Built fallback order from DB: {Order} ({Count} active models)",
                    string.Join(" → ", providerOrder), activeModels.Count);
            }
            else
            {
                _logger.LogWarning("No active models found in DB, using config fallback");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query active models from DB, falling back to config");
        }

        if (order.Count == 0)
        {
            var configuredFallback = _aiSettings.Value.FallbackChain?
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();

            if ((configuredFallback?.Count ?? 0) > 0)
            {
                order.AddRange(configuredFallback!);
            }
        }

        order.AddRange(_clients
            .Select(c => c.ProviderName)
            .Where(name => !order.Contains(name, StringComparer.OrdinalIgnoreCase)));

        return order;
    }

    /// <summary>
    /// Get default model for a provider from DB or hardcoded fallback.
    /// </summary>
    internal async Task<string> GetDefaultModelForProviderAsync(
        string providerName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var activeModels = await _modelConfigRepository.GetActiveAsync(cancellationToken).ConfigureAwait(false);
            var providerModels = activeModels
                .Where(m => string.Equals(m.Provider, providerName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (providerModels.Count > 0)
            {
                var selectedModel = providerModels.FirstOrDefault(m => m.IsPrimary)
                    ?? providerModels.OrderBy(m => m.Priority).First();

                _logger.LogDebug(
                    "Selected model {ModelId} for provider {Provider} from DB (IsPrimary: {IsPrimary}, Priority: {Priority})",
                    selectedModel.ModelId, providerName, selectedModel.IsPrimary, selectedModel.Priority);

                return selectedModel.ModelId;
            }

            _logger.LogWarning("No active models found for provider {Provider} in DB, using hardcoded fallback", providerName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query default model for provider {Provider} from DB, using hardcoded fallback", providerName);
        }

        return GetHardcodedDefaultModel(providerName);
    }

    private static string GetHardcodedDefaultModel(string providerName)
    {
        return providerName.ToLowerInvariant() switch
        {
            "ollama" => AgentDefaults.OllamaFallbackModel,
            "openrouter" => AgentDefaults.DefaultModel,
            _ => AgentDefaults.OllamaFallbackModel
        };
    }
}
