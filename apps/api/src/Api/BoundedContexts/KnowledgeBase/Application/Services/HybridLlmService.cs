using System.Diagnostics;
using System.Runtime.CompilerServices;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Services;
using Api.Services.LlmClients;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Hybrid LLM service coordinating multiple providers (Ollama, OpenRouter) with adaptive routing
/// ISSUE-958: Implements hybrid architecture with user-tier based routing and traffic split
/// ISSUE-962 (BGAI-020): Enhanced with circuit breaker, health monitoring, and latency tracking
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
/// </remarks>
public class HybridLlmService : ILlmService
{
    private readonly IEnumerable<ILlmClient> _clients;
    private readonly ILlmRoutingStrategy _routingStrategy;
    private readonly ILlmCostLogRepository _costLogRepository;
    private readonly ILogger<HybridLlmService> _logger;
    private readonly ProviderHealthCheckService? _healthCheckService;

    // ISSUE-962 (BGAI-020): Circuit breaker and monitoring
    private readonly Dictionary<string, CircuitBreakerState> _circuitBreakers = new();
    private readonly Dictionary<string, LatencyStats> _latencyStats = new();
    private readonly object _monitoringLock = new();

    // Default LLM parameters
    private const double DefaultTemperature = 0.3;
    private const int DefaultMaxTokens = 500;

    public HybridLlmService(
        IEnumerable<ILlmClient> clients,
        ILlmRoutingStrategy routingStrategy,
        ILlmCostLogRepository costLogRepository,
        ILogger<HybridLlmService> logger,
        ProviderHealthCheckService? healthCheckService = null)
    {
        _clients = clients ?? throw new ArgumentNullException(nameof(clients));
        _routingStrategy = routingStrategy ?? throw new ArgumentNullException(nameof(routingStrategy));
        _costLogRepository = costLogRepository ?? throw new ArgumentNullException(nameof(costLogRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _healthCheckService = healthCheckService; // Optional - may not be registered in tests

        if (!_clients.Any())
        {
            throw new ArgumentNullException("At least one ILlmClient must be registered");
        }

        // ISSUE-962: Initialize circuit breakers and latency stats for each provider
        foreach (var client in _clients)
        {
            _circuitBreakers[client.ProviderName] = new CircuitBreakerState();
            _latencyStats[client.ProviderName] = new LatencyStats();
        }

        var healthCheckStatus = _healthCheckService != null ? "enabled" : "disabled";
        _logger.LogInformation(
            "HybridLlmService initialized with {ClientCount} providers: {Providers} (cost tracking + circuit breaker + latency tracking + health checks {HealthCheckStatus})",
            _clients.Count(),
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
            ct);
    }

    /// <summary>
    /// Generate completion with user context for adaptive routing
    /// </summary>
    public async Task<LlmCompletionResult> GenerateCompletionAsync(
        string systemPrompt,
        string userPrompt,
        User? user,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            return LlmCompletionResult.CreateFailure("No user prompt provided");
        }

        // ISSUE-962: Route with circuit breaker awareness
        var decision = _routingStrategy.SelectProvider(user);
        var client = GetClientWithCircuitBreaker(decision.ProviderName);

        // P1: If fallback provider used, update decision to match actual provider
        if (client == null)
        {
            _logger.LogError(
                "No available provider found (circuit breakers may be open), using first client as fallback");
            client = _clients.First();

            // P1 FIX: Create decision specifically for fallback provider
            var originalProvider = decision.ProviderName;
            decision = new LlmRoutingDecision(
                ProviderName: client.ProviderName,
                ModelId: GetDefaultModelForProvider(client.ProviderName),
                Reason: $"Emergency fallback from {originalProvider} (all providers unavailable)"
            );

            _logger.LogWarning(
                "Created emergency fallback decision: {Provider} ({Model})",
                decision.ProviderName, decision.ModelId);
        }
        else if (client.ProviderName != decision.ProviderName)
        {
            // P1: Fallback provider was selected - update decision to match
            _logger.LogWarning(
                "Primary provider {Primary} unavailable, using fallback {Fallback}",
                decision.ProviderName, client.ProviderName);

            // Get new decision for the actual provider being used
            var fallbackUser = user; // Preserve user context
            // Create a decision that matches the fallback provider
            decision = new LlmRoutingDecision(
                ProviderName: client.ProviderName,
                ModelId: GetDefaultModelForProvider(client.ProviderName),
                Reason: $"Fallback from {decision.ProviderName} (circuit open or unhealthy)"
            );
        }

        _logger.LogInformation(
            "Generating completion via {Provider} ({Model}) - Reason: {Reason}",
            client.ProviderName, decision.ModelId, decision.Reason);

        // ISSUE-962: Track latency and circuit breaker state
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await client.GenerateCompletionAsync(
                decision.ModelId,
                systemPrompt,
                userPrompt,
                DefaultTemperature,
                DefaultMaxTokens,
                ct);

            stopwatch.Stop();

            // ISSUE-962: Record success metrics
            RecordSuccess(client.ProviderName, stopwatch.ElapsedMilliseconds);

            // Add routing metadata
            if (result.Success && result.Metadata is Dictionary<string, string> metadata)
            {
                metadata["routing_decision"] = decision.Reason;
                metadata["selected_provider"] = client.ProviderName;
                metadata["selected_model"] = decision.ModelId;
                metadata["latency_ms"] = stopwatch.ElapsedMilliseconds.ToString();
                metadata["circuit_state"] = GetCircuitState(client.ProviderName);
            }

            // ISSUE-960: Log cost to database (fire and forget - don't block response)
            _ = Task.Run(async () =>
            {
                try
                {
                    await _costLogRepository.LogCostAsync(
                        user?.Id,
                        user?.Role.Value ?? "Anonymous",
                        new Domain.Models.LlmCostCalculation
                        {
                            ModelId = result.Cost.ModelId,
                            Provider = result.Cost.Provider,
                            PromptTokens = result.Usage.PromptTokens,
                            CompletionTokens = result.Usage.CompletionTokens,
                            InputCost = result.Cost.InputCost,
                            OutputCost = result.Cost.OutputCost
                        },
                        endpoint: "completion",
                        success: result.Success,
                        errorMessage: result.ErrorMessage,
                        latencyMs: (int)stopwatch.ElapsedMilliseconds,
                        ipAddress: null, // Not available here - would come from HTTP context
                        userAgent: null,
                        ct: CancellationToken.None);
                }
                catch (Exception logEx)
                {
                    _logger.LogWarning(logEx, "Failed to log LLM cost (non-blocking)");
                }
            }, CancellationToken.None);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // ISSUE-962: Record failure metrics
            RecordFailure(client.ProviderName, stopwatch.ElapsedMilliseconds);

            _logger.LogError(ex,
                "Error generating completion with {Provider} ({Model}) - Circuit state: {CircuitState}",
                client.ProviderName, decision.ModelId, GetCircuitState(client.ProviderName));

            // ISSUE-960: Log failed request cost (fire and forget)
            _ = Task.Run(async () =>
            {
                try
                {
                    await _costLogRepository.LogCostAsync(
                        user?.Id,
                        user?.Role.Value ?? "Anonymous",
                        Domain.Models.LlmCostCalculation.Empty,
                        endpoint: "completion",
                        success: false,
                        errorMessage: ex.Message,
                        latencyMs: (int)stopwatch.ElapsedMilliseconds,
                        ipAddress: null,
                        userAgent: null,
                        ct: CancellationToken.None);
                }
                catch (Exception logEx)
                {
                    _logger.LogWarning(logEx, "Failed to log LLM error cost (non-blocking)");
                }
            }, CancellationToken.None);

            return LlmCompletionResult.CreateFailure($"Provider error: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<string> GenerateCompletionStreamAsync(
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
    /// </summary>
    public async IAsyncEnumerable<string> GenerateCompletionStreamAsync(
        string systemPrompt,
        string userPrompt,
        User? user,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
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
            client = _clients.First();
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
            ct))
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
            ct);
    }

    /// <summary>
    /// Generate JSON response with user context for adaptive routing
    /// </summary>
    public async Task<T?> GenerateJsonAsync<T>(
        string systemPrompt,
        string userPrompt,
        User? user,
        CancellationToken ct = default) where T : class
    {
        // Enhance system prompt for JSON mode
        var enhancedSystemPrompt = $"""
            {systemPrompt}

            CRITICAL: You must return ONLY valid JSON. No markdown code blocks, no explanations, no additional text.
            Just the raw JSON object that matches the required structure.
            """;

        var result = await GenerateCompletionAsync(enhancedSystemPrompt, userPrompt, user, ct);

        if (!result.Success || string.IsNullOrWhiteSpace(result.Response))
        {
            _logger.LogWarning("LLM completion failed or returned empty response for JSON generation");
            return null;
        }

        try
        {
            // Clean common LLM formatting artifacts
            var jsonText = CleanJsonResponse(result.Response);

            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip
            };

            var parsed = System.Text.Json.JsonSerializer.Deserialize<T>(jsonText, options);

            if (parsed == null)
            {
                _logger.LogWarning("LLM returned valid JSON but deserialization produced null");
            }

            return parsed;
        }
        catch (System.Text.Json.JsonException ex)
        {
            var truncated = result.Response.Length > 500
                ? result.Response.Substring(0, 500) + "..."
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
        if (cleaned.StartsWith("```"))
        {
            var firstNewline = cleaned.IndexOf('\n');
            var lastBackticks = cleaned.LastIndexOf("```");

            if (firstNewline > 0 && lastBackticks > firstNewline)
            {
                cleaned = cleaned.Substring(firstNewline + 1, lastBackticks - firstNewline - 1).Trim();
            }
        }

        return cleaned;
    }

    /// <summary>
    /// ISSUE-962: Get LLM client with circuit breaker and health awareness
    /// </summary>
    private ILlmClient? GetClientWithCircuitBreaker(string providerName)
    {
        var client = GetClient(providerName);
        if (client == null) return null;

        // Check if provider is available (circuit breaker + health status)
        if (!IsProviderAvailable(client.ProviderName))
        {
            _logger.LogWarning(
                "Provider {Provider} unavailable (circuit open or unhealthy). Trying fallback...",
                client.ProviderName);

            // Try to find alternative provider
            foreach (var fallbackClient in _clients.Where(c => c.ProviderName != client.ProviderName))
            {
                if (IsProviderAvailable(fallbackClient.ProviderName))
                {
                    _logger.LogInformation(
                        "Using fallback provider {Fallback} (primary {Primary} unavailable)",
                        fallbackClient.ProviderName, client.ProviderName);
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
    /// </summary>
    private bool IsProviderAvailable(string providerName)
    {
        lock (_monitoringLock)
        {
            // Check circuit breaker
            if (_circuitBreakers.TryGetValue(providerName, out var breaker))
            {
                if (!breaker.AllowsRequests())
                {
                    return false; // Circuit open - provider unavailable
                }
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
    public Dictionary<string, (string circuitState, string latencyStats)> GetMonitoringStatus()
    {
        lock (_monitoringLock)
        {
            var status = new Dictionary<string, (string, string)>();
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
    /// P1: Get default model for a provider (fallback scenario)
    /// </summary>
    private static string GetDefaultModelForProvider(string providerName)
    {
        return providerName.ToLowerInvariant() switch
        {
            "ollama" => "llama3.3:70b", // Free tier default
            "openrouter" => "meta-llama/llama-3.3-70b-instruct:free", // Free tier default
            _ => "llama3.3:70b" // Safe default
        };
    }
}
