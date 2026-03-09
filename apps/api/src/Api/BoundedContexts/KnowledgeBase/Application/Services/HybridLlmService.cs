using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Api.BoundedContexts.Administration.Application.Services;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.LlmManagement;
using Api.BoundedContexts.BusinessSimulations.Domain.Events;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.Services;
using Api.Services.LlmClients;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Hybrid LLM service coordinating multiple providers (Ollama, OpenRouter) with adaptive routing.
/// Issue #5487: Refactored to thin orchestrator — delegates provider selection to ILlmProviderSelector
/// and circuit breaker management to ICircuitBreakerRegistry.
///
/// Responsibilities (select → execute → log):
/// 1. Select provider via ILlmProviderSelector
/// 2. Execute request against selected ILlmClient
/// 3. Record outcome in ICircuitBreakerRegistry
/// 4. Log cost and usage
/// </summary>
internal class HybridLlmService : ILlmService
{
    private readonly List<ILlmClient> _clients;
    private readonly ILlmProviderSelector _providerSelector;
    private readonly ICircuitBreakerRegistry _circuitBreakerRegistry;
    private readonly ILogger<HybridLlmService> _logger;
    private readonly IUserBudgetService? _userBudgetService;
    private readonly IOpenRouterFileLogger? _openRouterFileLogger;
    private readonly IOpenRouterUsageService? _openRouterUsageService;
    private readonly IOpenRouterRateLimitTracker? _rateLimitTracker;
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly IFreeModelQuotaTracker? _freeModelQuotaTracker;

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
        ILlmProviderSelector providerSelector,
        ICircuitBreakerRegistry circuitBreakerRegistry,
        ILogger<HybridLlmService> logger,
        IUserBudgetService? userBudgetService = null,
        IOpenRouterFileLogger? openRouterFileLogger = null,
        IOpenRouterUsageService? openRouterUsageService = null,
        IOpenRouterRateLimitTracker? rateLimitTracker = null,
        IServiceScopeFactory? scopeFactory = null,
        IFreeModelQuotaTracker? freeModelQuotaTracker = null)
    {
        _clients = clients?.ToList() ?? throw new ArgumentNullException(nameof(clients));
        _providerSelector = providerSelector ?? throw new ArgumentNullException(nameof(providerSelector));
        _circuitBreakerRegistry = circuitBreakerRegistry ?? throw new ArgumentNullException(nameof(circuitBreakerRegistry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _userBudgetService = userBudgetService;
        _openRouterFileLogger = openRouterFileLogger;
        _openRouterUsageService = openRouterUsageService;
        _rateLimitTracker = rateLimitTracker;
        _scopeFactory = scopeFactory;
        _freeModelQuotaTracker = freeModelQuotaTracker;

        if (_clients.Count == 0)
        {
            throw new ArgumentException("At least one ILlmClient must be registered", nameof(clients));
        }

        _logger.LogInformation(
            "HybridLlmService initialized with {ClientCount} providers: {Providers} (delegating selection to ILlmProviderSelector)",
            _clients.Count,
            string.Join(", ", _clients.Select(c => c.ProviderName)));
    }

    /// <inheritdoc/>
    public async Task<LlmCompletionResult> GenerateCompletionAsync(
        string systemPrompt,
        string userPrompt,
        RequestSource source = RequestSource.Manual,
        CancellationToken ct = default)
    {
        return await GenerateCompletionAsync(
            systemPrompt,
            userPrompt,
            user: null,
            strategy: RagStrategy.Balanced,
            source: source,
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Generate completion with user context for adaptive routing.
    /// Issue #5487: Delegates provider selection to ILlmProviderSelector.
    /// </summary>
    public async Task<LlmCompletionResult> GenerateCompletionAsync(
        string systemPrompt,
        string userPrompt,
        User? user,
        RagStrategy strategy = RagStrategy.Balanced,
        RequestSource source = RequestSource.Manual,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(systemPrompt);
        ArgumentNullException.ThrowIfNull(userPrompt);
        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            return LlmCompletionResult.CreateFailure("No user prompt provided");
        }

        // Step 1: Select provider (routing + emergency override + RPD check + circuit breaker)
        var selection = await _providerSelector.SelectProviderAsync(user, strategy, source, cancellationToken)
            .ConfigureAwait(false);

        var client = selection.Client;
        var decision = selection.Decision;

        if (client == null)
        {
            return LlmCompletionResult.CreateFailure("Provider error: No providers available");
        }

        // Step 2: Execute with retry/fallback loop
        var attemptedProviders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        LlmCompletionResult? lastFailure = null;

        while (client != null && attemptedProviders.Add(client.ProviderName))
        {
            _logger.LogInformation(
                "Generating completion via {Provider} ({Model}) - Reason: {Reason}",
                client.ProviderName, decision.ModelId, decision.Reason);

            // Issue #5075: Warn if approaching RPM limit
            if (_rateLimitTracker != null)
            {
                var isApproaching = await _rateLimitTracker
                    .IsApproachingLimitAsync(client.ProviderName, thresholdPercent: 80, cancellationToken)
                    .ConfigureAwait(false);
                if (isApproaching)
                    _logger.LogWarning("OpenRouter rate limit approaching 80% for provider {Provider}", client.ProviderName);
            }

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
                    // Step 3: Record success + log cost
                    _circuitBreakerRegistry.RecordSuccess(client.ProviderName, attemptStopwatch.ElapsedMilliseconds);
                    AddRoutingMetadata(result, decision, client, attemptStopwatch.ElapsedMilliseconds);
                    await LogCostAsync(result, user, attemptStopwatch.ElapsedMilliseconds, source, cancellationToken).ConfigureAwait(false);

                    if (_rateLimitTracker != null)
                    {
                        var totalTokens = result.Usage.PromptTokens + result.Usage.CompletionTokens;
                        _ = _rateLimitTracker.RecordRequestAsync(client.ProviderName, decision.ModelId, totalTokens);
                    }

                    return result;
                }

                _circuitBreakerRegistry.RecordFailure(client.ProviderName, attemptStopwatch.ElapsedMilliseconds);

                // Issue #5087/#5088: Record rate limit events for future routing avoidance
                if (string.Equals(client.ProviderName, "OpenRouter", StringComparison.Ordinal)
                    && _freeModelQuotaTracker != null
                    && result.Metadata.TryGetValue("rate_limit_type", out var rlTypeStr)
                    && Enum.TryParse<RateLimitErrorType>(rlTypeStr, ignoreCase: true, out var rlErrorType))
                {
                    result.Metadata.TryGetValue("rate_limit_reset_ms", out var resetMsStr);
                    long? resetMs = long.TryParse(resetMsStr, NumberStyles.None, CultureInfo.InvariantCulture, out var resetMsVal)
                        ? resetMsVal : null;
                    result.Metadata.TryGetValue("rate_limit_model", out var rlModel);
                    var modelForTracking = string.IsNullOrEmpty(rlModel) ? decision.ModelId : rlModel;
                    _ = _freeModelQuotaTracker.RecordRateLimitErrorAsync(modelForTracking, rlErrorType, resetMs, cancellationToken);
                }

                await LogCostFailureAsync(result.ErrorMessage, user, attemptStopwatch.ElapsedMilliseconds, source, cancellationToken).ConfigureAwait(false);
                lastFailure = NormalizeFailureResult(result, client.ProviderName);
            }
            catch (Exception ex)
            {
                attemptStopwatch.Stop();
                _circuitBreakerRegistry.RecordFailure(client.ProviderName, attemptStopwatch.ElapsedMilliseconds);

                _logger.LogError(ex,
                    "Error generating completion with {Provider} ({Model}) - Circuit state: {CircuitState}",
                    client.ProviderName, decision.ModelId,
                    _circuitBreakerRegistry.GetCircuitStateDescription(client.ProviderName));

                await LogCostFailureAsync(ex.Message, user, attemptStopwatch.ElapsedMilliseconds, source, cancellationToken).ConfigureAwait(false);
                lastFailure = LlmCompletionResult.CreateFailure($"Provider error: {ex.Message}");
            }

            // Get next fallback
            var nextSelection = await _providerSelector.GetNextFallbackAsync(client.ProviderName, attemptedProviders, cancellationToken)
                .ConfigureAwait(false);
            client = nextSelection.Client;
            if (client != null)
            {
                decision = nextSelection.Decision;
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
        RequestSource source = RequestSource.Manual,
        CancellationToken ct = default)
    {
        return GenerateCompletionStreamAsync(
            systemPrompt,
            userPrompt,
            user: null,
            strategy: RagStrategy.Balanced,
            source: source,
            ct);
    }

    /// <summary>
    /// Generate streaming completion with user context for adaptive routing.
    /// Issue #5487: Delegates provider selection to ILlmProviderSelector.
    /// </summary>
#pragma warning disable S3400 // Methods should not return constants - async iterator pattern requires this signature
#pragma warning disable S4456 // Parameter validation on async iterator - validated before yield
    public async IAsyncEnumerable<StreamChunk> GenerateCompletionStreamAsync(
        string systemPrompt,
        string userPrompt,
        User? user,
        RagStrategy strategy = RagStrategy.Balanced,
        RequestSource source = RequestSource.Manual,
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

        var selection = await _providerSelector.SelectProviderAsync(user, strategy, source, cancellationToken)
            .ConfigureAwait(false);

        var client = selection.Client;
        if (client == null)
        {
            _logger.LogError("No client found for streaming, falling back to first available");
            client = _clients[0];
        }

        _logger.LogInformation(
            "Starting streaming completion via {Provider} ({Model}) - Reason: {Reason}",
            client.ProviderName, selection.Decision.ModelId, selection.Decision.Reason);

        await foreach (var chunk in client.GenerateCompletionStreamAsync(
            selection.Decision.ModelId,
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
        RequestSource source = RequestSource.Manual,
        CancellationToken ct = default) where T : class
    {
        return await GenerateJsonAsync<T>(
            systemPrompt,
            userPrompt,
            user: null,
            strategy: RagStrategy.Balanced,
            source: source,
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Generate JSON response with user context for adaptive routing.
    /// </summary>
    public async Task<T?> GenerateJsonAsync<T>(
        string systemPrompt,
        string userPrompt,
        User? user,
        RagStrategy strategy = RagStrategy.Balanced,
        RequestSource source = RequestSource.Manual,
        CancellationToken cancellationToken = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(systemPrompt);
        ArgumentNullException.ThrowIfNull(userPrompt);

        var enhancedSystemPrompt = $"""
            {systemPrompt}

            CRITICAL: You must return ONLY valid JSON. No markdown code blocks, no explanations, no additional text.
            Just the raw JSON object that matches the required structure.
            """;

        var result = await GenerateCompletionAsync(enhancedSystemPrompt, userPrompt, user, strategy, source, cancellationToken).ConfigureAwait(false);

        if (!result.Success || string.IsNullOrWhiteSpace(result.Response))
        {
            _logger.LogWarning("LLM completion failed or returned empty response for JSON generation");
            return null;
        }

        try
        {
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

    /// <inheritdoc/>
    public async Task<LlmCompletionResult> GenerateCompletionWithModelAsync(
        string explicitModel,
        string systemPrompt,
        string userPrompt,
        RequestSource source = RequestSource.Manual,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(explicitModel);
        ArgumentException.ThrowIfNullOrWhiteSpace(systemPrompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(userPrompt);

        // Issue #4332: Find client that supports the explicit model
        var client = _clients.FirstOrDefault(c => c.SupportsModel(explicitModel));
        if (client == null)
        {
            _logger.LogError("No client found supporting model {Model}", explicitModel);
            return LlmCompletionResult.CreateFailure($"No provider supports model: {explicitModel}");
        }

        var providerName = client.ProviderName;

        // Check circuit breaker before attempting
        if (_circuitBreakerRegistry.GetState(providerName) == CircuitState.Open)
        {
            _logger.LogWarning("Circuit breaker OPEN for {Provider}, skipping explicit model call", providerName);
            return LlmCompletionResult.CreateFailure($"Provider {providerName} circuit breaker open");
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation(
                "Generating completion with explicit model {Model} via {Provider}",
                explicitModel, providerName);

            var result = await client.GenerateCompletionAsync(
                explicitModel,
                systemPrompt,
                userPrompt,
                DefaultTemperature,
                DefaultMaxTokens,
                ct).ConfigureAwait(false);

            stopwatch.Stop();

            if (!result.Success)
            {
                _logger.LogWarning("Explicit model {Model} call failed: {Error}", explicitModel, result.ErrorMessage);
                _circuitBreakerRegistry.RecordFailure(providerName, stopwatch.ElapsedMilliseconds);
                return result;
            }

            _circuitBreakerRegistry.RecordSuccess(providerName, stopwatch.ElapsedMilliseconds);

            // Log cost asynchronously (fire-and-forget)
            _ = LogCostAsync(result, user: null, stopwatch.ElapsedMilliseconds, source, ct);

            _logger.LogInformation(
                "Explicit model {Model} completion successful (tokens: {Tokens}, cost: ${Cost:F6}, latency: {Latency}ms)",
                explicitModel, result.Usage.TotalTokens, result.Cost.TotalCost, stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Unexpected error generating completion with explicit model {Model}", explicitModel);
            _circuitBreakerRegistry.RecordFailure(providerName, stopwatch.ElapsedMilliseconds);
            return LlmCompletionResult.CreateFailure($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Issue #5487: Monitoring status now delegated to ICircuitBreakerRegistry.
    /// Kept for backward compatibility with GetLlmHealthQueryHandler.
    /// </summary>
    public virtual Dictionary<string, (string circuitState, string latencyStats)> GetMonitoringStatus()
    {
        return _circuitBreakerRegistry.GetMonitoringStatus();
    }

    /// <summary>
    /// Issue #5487: Latency stats now delegated to ICircuitBreakerRegistry.
    /// </summary>
    public string GetLatencyStats(string providerName)
    {
        return _circuitBreakerRegistry.GetLatencyStats(providerName);
    }

    /// <summary>
    /// Issue #5487: Circuit breaker reset now delegated to ICircuitBreakerRegistry.
    /// Kept for backward compatibility with ActivateEmergencyOverrideCommandHandler.
    /// </summary>
    public void ResetCircuitBreaker(string? targetProvider = null)
    {
        _circuitBreakerRegistry.ResetCircuitBreaker(targetProvider);
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
            metadata["circuit_state"] = _circuitBreakerRegistry.GetCircuitStateDescription(client.ProviderName);
        }
    }

    /// <summary>
    /// Log LLM cost and update usage stats.
    /// </summary>
    private async Task LogCostAsync(
        LlmCompletionResult result,
        User? user,
        long latencyMs,
        RequestSource source,
        CancellationToken cancellationToken)
    {
        try
        {
            if (_scopeFactory != null)
            {
                using var scope = _scopeFactory.CreateScope();
                var costLogRepo = scope.ServiceProvider.GetRequiredService<ILlmCostLogRepository>();
                await costLogRepo.LogCostAsync(
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
                    source: source,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            await UpdateModelUsageStatsAsync(result, cancellationToken).ConfigureAwait(false);

            if (_userBudgetService != null && user?.Id != null && _scopeFactory != null)
            {
                using var budgetScope = _scopeFactory.CreateScope();
                var budgetService = budgetScope.ServiceProvider.GetRequiredService<IUserBudgetService>();
                var requestCost = result.Cost.TotalCost;
                var requestTokens = result.Usage.PromptTokens + result.Usage.CompletionTokens;

                await budgetService
                    .RecordUsageAsync(user.Id, requestCost, requestTokens, cancellationToken)
                    .ConfigureAwait(false);
            }

            var totalCost = result.Cost.InputCost + result.Cost.OutputCost;
            if (totalCost > 0 && user?.Id != null && _scopeFactory != null)
            {
                using var publishScope = _scopeFactory.CreateScope();
                var publisher = publishScope.ServiceProvider.GetRequiredService<IPublisher>();
                await publisher.Publish(
                    new TokenUsageLedgerEvent(
                        UserId: user.Id,
                        ModelId: result.Cost.ModelId,
                        TokensConsumed: result.Usage.PromptTokens + result.Usage.CompletionTokens,
                        CostUsd: totalCost,
                        Endpoint: "completion",
                        Timestamp: DateTime.UtcNow),
                    cancellationToken).ConfigureAwait(false);
            }

            _openRouterFileLogger?.LogRequest(
                requestId: Guid.NewGuid().ToString(),
                model: result.Cost.ModelId,
                provider: result.Cost.Provider,
                source: source.ToString(),
                userId: user?.Id,
                promptTokens: result.Usage.PromptTokens,
                completionTokens: result.Usage.CompletionTokens,
                costUsd: result.Cost.TotalCost,
                latencyMs: latencyMs,
                success: true,
                isFreeModel: result.Cost.TotalCost == 0,
                sessionId: null);

            if (_openRouterUsageService != null && result.Cost.TotalCost > 0)
                _ = _openRouterUsageService.RecordRequestCostAsync(result.Cost.TotalCost);
        }
        catch (Exception logEx)
        {
            _logger.LogWarning(logEx, "Failed to log LLM cost");
        }
    }

    private async Task UpdateModelUsageStatsAsync(LlmCompletionResult result, CancellationToken cancellationToken)
    {
        if (_scopeFactory is null)
        {
            _logger.LogDebug("IServiceScopeFactory not available, skipping usage stats update");
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IAiModelConfigurationRepository>();

            var modelConfig = await repository.GetByModelIdAsync(result.Cost.ModelId, cancellationToken).ConfigureAwait(false);
            if (modelConfig != null)
            {
                var inputTokens = result.Usage.PromptTokens;
                var outputTokens = result.Usage.CompletionTokens;
                var totalCost = result.Cost.InputCost + result.Cost.OutputCost;

                modelConfig.TrackUsage(inputTokens, outputTokens, totalCost);
                await repository.UpdateAsync(modelConfig, cancellationToken).ConfigureAwait(false);

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
        RequestSource source,
        CancellationToken cancellationToken)
    {
        try
        {
            if (_scopeFactory != null)
            {
                using var scope = _scopeFactory.CreateScope();
                var costLogRepo = scope.ServiceProvider.GetRequiredService<ILlmCostLogRepository>();
                await costLogRepo.LogCostAsync(
                    user?.Id,
                    user?.Role.Value ?? "Anonymous",
                    LlmCostCalculation.Empty,
                    endpoint: "completion",
                    success: false,
                    errorMessage: errorMessage,
                    latencyMs: (int)latencyMs,
                    ipAddress: null,
                    userAgent: null,
                    source: source,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            _openRouterFileLogger?.LogRequest(
                requestId: Guid.NewGuid().ToString(),
                model: string.Empty,
                provider: string.Empty,
                source: source.ToString(),
                userId: user?.Id,
                promptTokens: 0,
                completionTokens: 0,
                costUsd: 0,
                latencyMs: latencyMs,
                success: false,
                isFreeModel: false,
                sessionId: null,
                errorMessage: errorMessage);
        }
        catch (Exception logEx)
        {
            _logger.LogWarning(logEx, "Failed to log LLM error cost");
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

    private static string CleanJsonResponse(string response)
    {
        var cleaned = response.Trim();

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
}
