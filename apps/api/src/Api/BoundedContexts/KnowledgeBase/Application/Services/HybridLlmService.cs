using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.Configuration;
using Api.Services;
using Api.Services.LlmClients;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Hybrid LLM service coordinating multiple providers (Ollama, OpenRouter) with adaptive routing
/// ISSUE-958: Implements hybrid architecture with user-tier based routing and traffic split
/// ISSUE-962 (BGAI-020): Enhanced with circuit breaker, health monitoring, and latency tracking
/// BGAI-022 (Issue #1153): Integrated with AI:Provider runtime configuration
/// </summary>
/// <remarks>
/// Architecture:
/// - Coordinates ILlmClient implementations (OllamaLlmClient, OpenRouterLlmClient)
/// - Uses ILlmRoutingStrategy for provider selection (user-type + traffic split)
/// - Implements ILlmService interface for seamless integration with existing code
/// - Supports both completion and streaming modes
///
/// Cost Optimization:
/// - Target: 80% free tier (Ollama/Llama 3.3 70B free), 20% paid (GPT-4o-mini)
/// - Anonymous/User: Heavy Ollama usage
/// - Editor: Balanced
/// - Admin: Premium models prioritized
///
/// Reliability (BGAI-020):
/// - Circuit breaker: Prevents cascading failures (5 failures → open for 30s)
/// - Health monitoring: Integration with ProviderHealthCheckService
/// - Latency tracking: Real-time performance metrics (avg, P50, P95, P99)
/// - Automatic failover: Routes to healthy providers
///
/// Runtime Configuration (BGAI-022):
/// - AI:Providers[x].Enabled: Runtime provider enable/disable
/// - AI:FallbackChain: Circuit breaker fallback order
/// - Backward compatible: Works without AI section (uses defaults)
/// </remarks>
internal class HybridLlmService : ILlmService
{
    private readonly List<ILlmClient> _clients;
    private readonly ILlmRoutingStrategy _routingStrategy;
    private readonly ILlmCostLogRepository _costLogRepository;
    private readonly ILogger<HybridLlmService> _logger;
    private readonly IProviderHealthCheckService? _healthCheckService;
    private readonly IOptions<AiProviderSettings> _aiSettings;
    private readonly IAiModelConfigurationRepository _modelConfigRepository;

    // ISSUE-962 (BGAI-020): Circuit breaker and monitoring
    private readonly Dictionary<string, CircuitBreakerState> _circuitBreakers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, LatencyStats> _latencyStats = new(StringComparer.Ordinal);
    private readonly System.Threading.Lock _monitoringLock = new();

    // Default LLM parameters
    private const double DefaultTemperature = 0.3;
    private const int DefaultMaxTokens = 500;

    // CA1869: Cache JsonSerializerOptions for better performance
    private static readonly System.Text.Json.JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip
    };

    public HybridLlmService(
        IEnumerable<ILlmClient> clients,
        ILlmRoutingStrategy routingStrategy,
        ILlmCostLogRepository costLogRepository,
        ILogger<HybridLlmService> logger,
        IOptions<AiProviderSettings> aiSettings,
        IAiModelConfigurationRepository modelConfigRepository,
        IProviderHealthCheckService? healthCheckService = null)
    {
        _clients = clients?.ToList() ?? throw new ArgumentNullException(nameof(clients));
        _routingStrategy = routingStrategy ?? throw new ArgumentNullException(nameof(routingStrategy));
        _costLogRepository = costLogRepository ?? throw new ArgumentNullException(nameof(costLogRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _aiSettings = aiSettings ?? throw new ArgumentNullException(nameof(aiSettings));
        _modelConfigRepository = modelConfigRepository ?? throw new ArgumentNullException(nameof(modelConfigRepository));
        _healthCheckService = healthCheckService; // Optional - may not be registered in tests

        if (_clients.Count == 0)
        {
            throw new ArgumentException("At least one ILlmClient must be registered", nameof(clients));
        }

        // ISSUE-962: Initialize circuit breakers and latency stats for each provider
        // ISSUE-962: Initialize circuit breakers and latency stats for each provider
        foreach (var providerName in _clients.Select(client => client.ProviderName))
        {
            _circuitBreakers[providerName] = new CircuitBreakerState();
            _latencyStats[providerName] = new LatencyStats();
        }

        var healthCheckStatus = _healthCheckService != null ? "enabled" : "disabled";
        _logger.LogInformation(
            "HybridLlmService initialized with {ClientCount} providers: {Providers} (cost tracking + circuit breaker + latency tracking + health checks {HealthCheckStatus} + AI config integration)",
            _clients.Count,
            string.Join(", ", _clients.Select(c => c.ProviderName)),
            healthCheckStatus);
    }
    /// <inheritdoc/>
    public async Task<LlmCompletionResult> GenerateCompletionAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken ct = default)
    {
        return await GenerateCompletionAsync(
            systemPrompt,
            userPrompt,
            user: null, // No user context (anonymous)
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Generate completion with user context for adaptive routing
    /// </summary>
    public async Task<LlmCompletionResult> GenerateCompletionAsync(
        string systemPrompt,
        string userPrompt,
        User? user,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(systemPrompt);
        ArgumentNullException.ThrowIfNull(userPrompt);
        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            return LlmCompletionResult.CreateFailure("No user prompt provided");
        }

        // ISSUE-962: Route with circuit breaker awareness and support fallback retries
        var decision = _routingStrategy.SelectProvider(user);
        var client = GetClientWithCircuitBreaker(decision.ProviderName);

        if (client == null)
        {
            _logger.LogError(
                "No available provider found (circuit breakers may be open), using first client as fallback");
            client = _clients[0]; // Safe: constructor ensures _clients.Count > 0

            var originalProvider = decision.ProviderName;
            var emergencyModelId = await GetDefaultModelForProviderAsync(client.ProviderName, cancellationToken).ConfigureAwait(false);
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

            var fallbackModelId = await GetDefaultModelForProviderAsync(client.ProviderName, cancellationToken).ConfigureAwait(false);
            decision = new LlmRoutingDecision(
                ProviderName: client.ProviderName,
                ModelId: fallbackModelId,
                Reason: $"Fallback from {decision.ProviderName} (circuit open or unhealthy)");
        }

        var attemptedProviders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        LlmCompletionResult? lastFailure = null;

        while (client != null && attemptedProviders.Add(client.ProviderName))
        {
            _logger.LogInformation(
                "Generating completion via {Provider} ({Model}) - Reason: {Reason}",
                client.ProviderName, decision.ModelId, decision.Reason);

            var attemptStopwatch = Stopwatch.StartNew();
            try
            {
                var result = await client.GenerateCompletionAsync(
                    decision.ModelId,
                    systemPrompt,
                    userPrompt,
                    DefaultTemperature,
                    DefaultMaxTokens,
                    cancellationToken).ConfigureAwait(false);

                attemptStopwatch.Stop();

                if (result.Success)
                {
                    RecordSuccess(client.ProviderName, attemptStopwatch.ElapsedMilliseconds);
                    AddRoutingMetadata(result, decision, client, attemptStopwatch.ElapsedMilliseconds);
                    await LogCostAsync(result, user, attemptStopwatch.ElapsedMilliseconds, cancellationToken).ConfigureAwait(false);
                    return result;
                }

                RecordFailure(client.ProviderName, attemptStopwatch.ElapsedMilliseconds);
                await LogCostFailureAsync(result.ErrorMessage, user, attemptStopwatch.ElapsedMilliseconds, cancellationToken).ConfigureAwait(false);
                lastFailure = NormalizeFailureResult(result, client.ProviderName);
            }
            catch (Exception ex)
            {
                attemptStopwatch.Stop();
                RecordFailure(client.ProviderName, attemptStopwatch.ElapsedMilliseconds);

                _logger.LogError(ex,
                    "Error generating completion with {Provider} ({Model}) - Circuit state: {CircuitState}",
                    client.ProviderName, decision.ModelId, GetCircuitState(client.ProviderName));

                await LogCostFailureAsync(ex.Message, user, attemptStopwatch.ElapsedMilliseconds, cancellationToken).ConfigureAwait(false);
                lastFailure = LlmCompletionResult.CreateFailure($"Provider error: {ex.Message}");
            }

            var (nextClient, nextDecision) = await GetNextFallbackClientAsync(client.ProviderName, attemptedProviders, cancellationToken).ConfigureAwait(false);
            client = nextClient;
            if (client != null)
            {
                decision = nextDecision;
            }

            if (client == null)
            {
                _logger.LogWarning(
                    "No additional fallback providers available after failures: {Providers}",
                    string.Join(", ", attemptedProviders));
            }
        }

        return lastFailure ?? LlmCompletionResult.CreateFailure("Provider error: No providers available");
    }
    /// <inheritdoc/>
    public IAsyncEnumerable<StreamChunk> GenerateCompletionStreamAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken ct = default)
    {
        return GenerateCompletionStreamAsync(
            systemPrompt,
            userPrompt,
            user: null, // No user context (anonymous)
            ct);
    }

    /// <summary>
    /// Generate streaming completion with user context for adaptive routing
    /// ISSUE-1725: Enhanced to return StreamChunk with usage metadata
    /// </summary>
#pragma warning disable S3400 // Methods should not return constants - async iterator pattern requires this signature
#pragma warning disable S4456 // Parameter validation on async iterator - validated before yield
    public async IAsyncEnumerable<StreamChunk> GenerateCompletionStreamAsync(
        string systemPrompt,
        string userPrompt,
        User? user,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
#pragma warning restore S4456
#pragma warning restore S3400
    {
        ArgumentNullException.ThrowIfNull(systemPrompt);
        ArgumentNullException.ThrowIfNull(userPrompt);
        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            _logger.LogWarning("Empty user prompt for streaming completion");
            yield break;
        }

        // Route to appropriate provider
        var decision = _routingStrategy.SelectProvider(user);
        var client = GetClient(decision.ProviderName);

        if (client == null)
        {
            _logger.LogError(
                "No client found for provider {Provider}, falling back to first available",
                decision.ProviderName);
            client = _clients[0]; // Safe: constructor ensures _clients.Count > 0
        }

        _logger.LogInformation(
            "Starting streaming completion via {Provider} ({Model}) - Reason: {Reason}",
            client.ProviderName, decision.ModelId, decision.Reason);

        await foreach (var chunk in client.GenerateCompletionStreamAsync(
            decision.ModelId,
            systemPrompt,
            userPrompt,
            DefaultTemperature,
            DefaultMaxTokens,
            cancellationToken).ConfigureAwait(false))
        {
            yield return chunk;
        }
    }

    /// <inheritdoc/>
    public async Task<T?> GenerateJsonAsync<T>(
        string systemPrompt,
        string userPrompt,
        CancellationToken ct = default) where T : class
    {
        return await GenerateJsonAsync<T>(
            systemPrompt,
            userPrompt,
            user: null, // No user context (anonymous)
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Generate JSON response with user context for adaptive routing
    /// </summary>
    public async Task<T?> GenerateJsonAsync<T>(
        string systemPrompt,
        string userPrompt,
        User? user,
        CancellationToken cancellationToken = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(systemPrompt);
        ArgumentNullException.ThrowIfNull(userPrompt);
        // Enhance system prompt for JSON mode
        var enhancedSystemPrompt = $"""
            {systemPrompt}

            CRITICAL: You must return ONLY valid JSON. No markdown code blocks, no explanations, no additional text.
            Just the raw JSON object that matches the required structure.
            """;

        var result = await GenerateCompletionAsync(enhancedSystemPrompt, userPrompt, user, cancellationToken).ConfigureAwait(false);

        if (!result.Success || string.IsNullOrWhiteSpace(result.Response))
        {
            _logger.LogWarning("LLM completion failed or returned empty response for JSON generation");
            return null;
        }

        try
        {
            // Clean common LLM formatting artifacts
            var jsonText = CleanJsonResponse(result.Response);

            var parsed = System.Text.Json.JsonSerializer.Deserialize<T>(jsonText, s_jsonOptions);

            if (parsed == null)
            {
                _logger.LogWarning("LLM returned valid JSON but deserialization produced null");
            }

            return parsed;
        }
        catch (System.Text.Json.JsonException ex)
        {
            var truncated = result.Response.Length > 500
                ? string.Concat(result.Response.AsSpan(0, 500), "...")
                : result.Response;
            _logger.LogWarning(ex, "Failed to parse LLM JSON response. Raw: {Response}", truncated);
            return null;
        }
    }

    /// <summary>
    /// Get LLM client by provider name
    /// </summary>
    private ILlmClient? GetClient(string providerName)
    {
        return _clients.FirstOrDefault(c =>
            c.ProviderName.Equals(providerName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Clean LLM response to extract pure JSON
    /// </summary>
    private static string CleanJsonResponse(string response)
    {
        var cleaned = response.Trim();

        // Remove markdown code blocks (```json ... ``` or ``` ... ```)
        if (cleaned.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = cleaned.IndexOf('\n');
            var lastBackticks = cleaned.LastIndexOf("```", StringComparison.Ordinal);

            if (firstNewline > 0 && lastBackticks > firstNewline)
            {
                cleaned = cleaned.Substring(firstNewline + 1, lastBackticks - firstNewline - 1).Trim();
            }
        }

        return cleaned;
    }

    /// <summary>
    /// ISSUE-962: Get LLM client with circuit breaker and health awareness
    /// BGAI-022: Enhanced with FallbackChain support
    /// </summary>
    private ILlmClient? GetClientWithCircuitBreaker(string providerName)
    {
        var client = GetClient(providerName);
        if (client == null) return null;

        // Check if provider is available (circuit breaker + health status + enabled flag)
        if (!IsProviderAvailable(client.ProviderName))
        {
            _logger.LogWarning(
                "Provider {Provider} unavailable (circuit open, unhealthy, or disabled). Trying fallback...",
                client.ProviderName);

            // BGAI-022: Use FallbackChain if configured, otherwise use all clients
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
    /// ISSUE-962: Check if provider is available (circuit breaker + health check)
    /// BGAI-022: Enhanced with Enabled flag check
    /// </summary>
    private bool IsProviderAvailable(string providerName)
    {
        lock (_monitoringLock)
        {
            // BGAI-022: Check if provider is enabled in configuration
            var settings = _aiSettings.Value;
            if (settings.Providers?.ContainsKey(providerName) is true &&
                !settings.Providers[providerName].Enabled)
            {
                _logger.LogDebug(
                    "Provider {Provider} is disabled in configuration (AI:Providers:{ProviderName}:Enabled = false)",
                    providerName, providerName);
                return false; // Disabled in config - provider unavailable
            }

            // Check circuit breaker
            if (_circuitBreakers.TryGetValue(providerName, out var breaker) && !breaker.AllowsRequests())
            {
                return false; // Circuit open - provider unavailable
            }

            // Check health status (if health check service is enabled)
            if (_healthCheckService != null)
            {
                var healthStatus = _healthCheckService.GetProviderHealth(providerName);
                if (healthStatus != null && !healthStatus.IsAvailable())
                {
                    _logger.LogDebug(
                        "Provider {Provider} marked unhealthy: {Status}",
                        providerName, healthStatus.GetStatusSummary());
                    return false; // Unhealthy - provider unavailable
                }
            }

            return true; // Provider is available
        }
    }

    private void AddRoutingMetadata(
        LlmCompletionResult result,
        LlmRoutingDecision decision,
        ILlmClient client,
        long latencyMs)
    {
        if (result.Metadata is Dictionary<string, string> metadata)
        {
            metadata["routing_decision"] = decision.Reason;
            metadata["selected_provider"] = client.ProviderName;
            metadata["selected_model"] = decision.ModelId;
            metadata["latency_ms"] = latencyMs.ToString(CultureInfo.InvariantCulture);
            metadata["circuit_state"] = GetCircuitState(client.ProviderName);
        }
    }

    private async Task LogCostAsync(
        LlmCompletionResult result,
        User? user,
        long latencyMs,
        CancellationToken cancellationToken)
    {
        try
        {
            await _costLogRepository.LogCostAsync(
                user?.Id,
                user?.Role.Value ?? "Anonymous",
                new LlmCostCalculation
                {
                    ModelId = result.Cost.ModelId,
                    Provider = result.Cost.Provider,
                    PromptTokens = result.Usage.PromptTokens,
                    CompletionTokens = result.Usage.CompletionTokens,
                    InputCost = result.Cost.InputCost,
                    OutputCost = result.Cost.OutputCost
                },
                endpoint: "completion",
                success: true,
                errorMessage: null,
                latencyMs: (int)latencyMs,
                ipAddress: null,
                userAgent: null,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            // Issue #2520: Update model usage statistics
            await UpdateModelUsageStatsAsync(result, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception logEx)
        {
            _logger.LogWarning(logEx, "Failed to log LLM cost");
        }
    }

    /// <summary>
    /// Issue #2520: Update AiModelConfiguration usage statistics after successful request
    /// </summary>
    private async Task UpdateModelUsageStatsAsync(LlmCompletionResult result, CancellationToken cancellationToken)
    {
        try
        {
            var modelConfig = await _modelConfigRepository.GetByModelIdAsync(result.Cost.ModelId, cancellationToken).ConfigureAwait(false);
            if (modelConfig != null)
            {
                var inputTokens = result.Usage.PromptTokens;
                var outputTokens = result.Usage.CompletionTokens;
                var totalCost = result.Cost.InputCost + result.Cost.OutputCost;

                modelConfig.TrackUsage(inputTokens, outputTokens, totalCost); // Issue #2580: Use TrackUsage API
                await _modelConfigRepository.UpdateAsync(modelConfig, cancellationToken).ConfigureAwait(false);

                _logger.LogDebug(
                    "Updated usage stats for model {ModelId}: +{Tokens} tokens, +${Cost:F6}",
                    result.Cost.ModelId, inputTokens + outputTokens, totalCost);
            }
            else
            {
                _logger.LogWarning("Model {ModelId} not found in DB for usage stats update", result.Cost.ModelId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update usage stats for model {ModelId}", result.Cost.ModelId);
        }
    }

    private async Task LogCostFailureAsync(
        string? errorMessage,
        User? user,
        long latencyMs,
        CancellationToken cancellationToken)
    {
        try
        {
            await _costLogRepository.LogCostAsync(
                user?.Id,
                user?.Role.Value ?? "Anonymous",
                LlmCostCalculation.Empty,
                endpoint: "completion",
                success: false,
                errorMessage: errorMessage,
                latencyMs: (int)latencyMs,
                ipAddress: null,
                userAgent: null,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception logEx)
        {
            _logger.LogWarning(logEx, "Failed to log LLM error cost");
        }
    }

    /// <summary>
    /// Issue #2520: Get next fallback client with DB-driven fallback chain
    /// </summary>
    private async Task<(ILlmClient? client, LlmRoutingDecision decision)> GetNextFallbackClientAsync(
        string failedProvider,
        HashSet<string> attemptedProviders,
        CancellationToken cancellationToken = default)
    {
        var fallbackOrder = await BuildFallbackOrderAsync(cancellationToken).ConfigureAwait(false);

        foreach (var providerName in fallbackOrder)
        {
            if (attemptedProviders.Contains(providerName))
                continue;

            var fallbackClient = GetClientWithCircuitBreaker(providerName);
            if (fallbackClient != null)
            {
                var modelId = await GetDefaultModelForProviderAsync(fallbackClient.ProviderName, cancellationToken)
                    .ConfigureAwait(false);

                var decision = new LlmRoutingDecision(
                    ProviderName: fallbackClient.ProviderName,
                    ModelId: modelId,
                    Reason: $"Fallback from {failedProvider}");
                return (fallbackClient, decision);
            }
        }

        return (null, default!);
    }

    /// <summary>
    /// Issue #2520: Build fallback order from DB (AiModelConfiguration) with config fallback
    /// </summary>
    private async Task<List<string>> BuildFallbackOrderAsync(CancellationToken cancellationToken = default)
    {
        var order = new List<string>();

        try
        {
            // Issue #2520: Query active models from DB ordered by Priority (ASC = higher priority first)
            var activeModels = await _modelConfigRepository.GetActiveAsync(cancellationToken).ConfigureAwait(false);

            if (activeModels.Count > 0)
            {
                // Group by Provider, take highest priority model per provider
                var providerOrder = activeModels
                    .GroupBy(m => m.Provider, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(g => g.Min(m => m.Priority)) // Lowest Priority value = highest priority
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

        // Fallback to config if DB query failed or empty
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

        // Add any remaining providers not in DB or config
        order.AddRange(_clients
            .Select(c => c.ProviderName)
            .Where(name => !order.Contains(name, StringComparer.OrdinalIgnoreCase)));

        return order;
    }

    /// <summary>
    /// ISSUE-962: Record successful request (circuit breaker + latency)
    /// </summary>
    private void RecordSuccess(string providerName, long latencyMs)
    {
        lock (_monitoringLock)
        {
            if (_circuitBreakers.TryGetValue(providerName, out var breaker))
            {
                breaker.RecordSuccess();
            }

            if (_latencyStats.TryGetValue(providerName, out var stats))
            {
                stats.RecordLatency(latencyMs);
            }

            _logger.LogDebug(
                "Request success: {Provider} - Latency: {Latency}ms, Circuit: {CircuitState}",
                providerName, latencyMs, breaker?.GetStatus() ?? "unknown");
        }
    }

    /// <summary>
    /// ISSUE-962: Record failed request (circuit breaker + latency)
    /// </summary>
    private void RecordFailure(string providerName, long latencyMs)
    {
        lock (_monitoringLock)
        {
            if (_circuitBreakers.TryGetValue(providerName, out var breaker))
            {
                breaker.RecordFailure();
            }

            if (_latencyStats.TryGetValue(providerName, out var stats))
            {
                stats.RecordLatency(latencyMs); // Record even failures for complete picture
            }

            _logger.LogWarning(
                "Request failure: {Provider} - Latency: {Latency}ms, Circuit: {CircuitState}",
                providerName, latencyMs, breaker?.GetStatus() ?? "unknown");
        }
    }

    private static LlmCompletionResult NormalizeFailureResult(
        LlmCompletionResult result,
        string providerName)
    {
        var message = string.IsNullOrWhiteSpace(result.ErrorMessage)
            ? $"{providerName} provider error"
            : result.ErrorMessage;

        if (!message.Contains("error", StringComparison.OrdinalIgnoreCase))
        {
            message = $"Provider error: {message}";
        }

        return result with { ErrorMessage = message };
    }

    /// <summary>
    /// ISSUE-962: Get circuit breaker state for a provider
    /// </summary>
    private string GetCircuitState(string providerName)
    {
        lock (_monitoringLock)
        {
            return _circuitBreakers.TryGetValue(providerName, out var breaker)
                ? breaker.GetStatus()
                : "unknown";
        }
    }

    /// <summary>
    /// ISSUE-962: Get latency statistics for a provider
    /// </summary>
    public string GetLatencyStats(string providerName)
    {
        lock (_monitoringLock)
        {
            return _latencyStats.TryGetValue(providerName, out var stats)
                ? stats.GetSummary()
                : "No data";
        }
    }

    /// <summary>
    /// ISSUE-962: Get monitoring status for all providers
    /// </summary>
    public virtual Dictionary<string, (string circuitState, string latencyStats)> GetMonitoringStatus()
    {
        lock (_monitoringLock)
        {
            var status = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
            foreach (var client in _clients)
            {
                var circuit = _circuitBreakers.TryGetValue(client.ProviderName, out var breaker)
                    ? breaker.GetStatus()
                    : "unknown";

                var latency = _latencyStats.TryGetValue(client.ProviderName, out var stats)
                    ? stats.GetSummary()
                    : "No data";

                status[client.ProviderName] = (circuit, latency);
            }
            return status;
        }
    }

    /// <summary>
    /// Issue #2520: Get default model for a provider from DB (primary or highest priority)
    /// </summary>
    private async Task<string> GetDefaultModelForProviderAsync(
        string providerName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Query primary model for this provider
            var activeModels = await _modelConfigRepository.GetActiveAsync(cancellationToken).ConfigureAwait(false);
            var providerModels = activeModels
                .Where(m => string.Equals(m.Provider, providerName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (providerModels.Count > 0)
            {
                // Prefer IsPrimary, otherwise take highest priority (lowest Priority value)
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

        // Hardcoded fallback (backward compatibility)
        return GetHardcodedDefaultModel(providerName);
    }

    /// <summary>
    /// Hardcoded fallback for backward compatibility
    /// </summary>
    private static string GetHardcodedDefaultModel(string providerName)
    {
        return providerName.ToLowerInvariant() switch
        {
            "ollama" => "llama3.3:70b", // Free tier default
            "openrouter" => "meta-llama/llama-3.3-70b-instruct:free", // Free tier default
            _ => "llama3.3:70b" // Safe default
        };
    }
}

