using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.LlmManagement;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Services;
using Api.Services.LlmClients;
using System.Globalization;
using RagStrategy = Api.BoundedContexts.KnowledgeBase.Domain.Enums.RagStrategy;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Hybrid LLM service coordinating multiple providers (Ollama, OpenRouter) with adaptive routing.
/// Issue #5487: Delegates provider selection to ILlmProviderSelector and circuit breaker to ICircuitBreakerRegistry.
/// Issue #5489: Delegates cost logging to ILlmCostService.
///
/// Responsibilities (select → execute → log):
/// 1. Select provider via ILlmProviderSelector
/// 2. Execute request against selected ILlmClient
/// 3. Record outcome in ICircuitBreakerRegistry
/// 4. Log cost via ILlmCostService
/// </summary>
internal class HybridLlmService : ILlmService
{
    private readonly List<ILlmClient> _clients;
    private readonly ILlmProviderSelector _providerSelector;
    private readonly ICircuitBreakerRegistry _circuitBreakerRegistry;
    private readonly ILlmCostService _costService;
    private readonly ILogger<HybridLlmService> _logger;
    private readonly IPiiDetector? _piiDetector; // Issue #5510: PII redaction for OpenRouter-bound prompts
    private readonly ILlmCallerAccessor? _callerAccessor; // #4164: attribuzione del costo all'utente della richiesta
    private readonly ILlmBudgetGate? _budgetGate; // #4166: tetto di spesa e fail closed, prima di ogni chiamata
    private readonly IAgentReserveModelProvider? _reserveModels; // #4166: modello di riserva per il degrado

    // Default LLM parameters
    private const double DefaultTemperature = 0.3;
    private const int DefaultMaxTokens = 1500;

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
        ILlmCostService costService,
        ILogger<HybridLlmService> logger,
        IPiiDetector? piiDetector = null,
        ILlmCallerAccessor? callerAccessor = null,
        ILlmBudgetGate? budgetGate = null,
        IAgentReserveModelProvider? reserveModels = null)
    {
        _clients = clients?.ToList() ?? throw new ArgumentNullException(nameof(clients));
        _providerSelector = providerSelector ?? throw new ArgumentNullException(nameof(providerSelector));
        _circuitBreakerRegistry = circuitBreakerRegistry ?? throw new ArgumentNullException(nameof(circuitBreakerRegistry));
        _costService = costService ?? throw new ArgumentNullException(nameof(costService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _piiDetector = piiDetector; // Issue #5510: Optional - PII redaction for OpenRouter
        _callerAccessor = callerAccessor;
        _budgetGate = budgetGate;
        _reserveModels = reserveModels;

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

        // Map User entity to KB Domain value object (Application layer ACL)
        var userContext = LlmUserContextMapper.FromUser(user);
        var costContext = AttributeCost(userContext);

        // Step 1: Select provider (routing + emergency override + RPD check + circuit breaker)
        var selection = await _providerSelector.SelectProviderAsync(userContext, strategy, source, cancellationToken)
            .ConfigureAwait(false);

        var client = selection.Client;
        var decision = selection.Decision;

        if (client == null)
        {
            return LlmCompletionResult.CreateFailure("Provider error: No providers available");
        }

        (client, decision) = await ApplyBudgetDegradeAsync(client, decision, source, cancellationToken).ConfigureAwait(false);

        // Step 2: Execute with retry/fallback loop
        var attemptedProviders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        LlmCompletionResult? lastFailure = null;

        while (client != null && attemptedProviders.Add(client.ProviderName))
        {
            _logger.LogInformation(
                "Generating completion via {Provider} ({Model}) - Reason: {Reason}",
                client.ProviderName, decision.ModelId, decision.Reason);

            // Issue #5492: Warn if approaching RPM limit (delegated to selector)
            await _providerSelector.WarnIfApproachingLimitAsync(client.ProviderName, cancellationToken)
                .ConfigureAwait(false);

            // Issue #5510: Redact PII for OpenRouter-bound prompts (not needed for local Ollama)
            var effectiveSystemPrompt = systemPrompt;
            var effectiveUserPrompt = userPrompt;
            if (_piiDetector != null && string.Equals(client.ProviderName, "OpenRouter", StringComparison.Ordinal))
            {
                effectiveSystemPrompt = _piiDetector.ScanAndRedact(systemPrompt);
                effectiveUserPrompt = _piiDetector.ScanAndRedact(userPrompt);
            }

            // #4166: il tetto prima della chiamata. Un modello fuori catalogo passa al provider
            // successivo; un tetto o una quota raggiunti fermano la richiesta, perché il fallback
            // spenderebbe dallo stesso budget.
            var admission = await AdmitAsync(source, decision.ModelId, client.ProviderName,
                effectiveSystemPrompt.Length + effectiveUserPrompt.Length, DefaultMaxTokens, 0, cancellationToken).ConfigureAwait(false);
            if (!admission.IsAllowed && admission.Rejection != LlmBudgetRejection.ModelNotInCatalog)
            {
                return Refused(admission);
            }

            var attemptStopwatch = Stopwatch.StartNew();
            var actualCost = 0m;
            try
            {
                if (!admission.IsAllowed)
                {
                    lastFailure = Refused(admission);
                }
                else
                {
                    var result = await client.GenerateCompletionAsync(
                        decision.ModelId,
                        effectiveSystemPrompt,
                        effectiveUserPrompt,
                        DefaultTemperature,
                        DefaultMaxTokens,
                        cancellationToken).ConfigureAwait(false);

                    attemptStopwatch.Stop();

                    if (result.Success)
                    {
                        actualCost = result.Cost.TotalCost;
                        // Step 3: Record success + log cost (Issue #5492: delegated to selector)
                        _providerSelector.RecordSuccess(client.ProviderName, decision.ModelId, attemptStopwatch.ElapsedMilliseconds, result);
                        AddRoutingMetadata(result, decision, client, attemptStopwatch.ElapsedMilliseconds);
                        // #4164: CancellationToken.None, come nei percorsi fire-and-forget: con il token della
                        // richiesta una disconnessione del client faceva perdere la riga.
                        await _costService.LogSuccessAsync(result, costContext, attemptStopwatch.ElapsedMilliseconds, source, CancellationToken.None).ConfigureAwait(false);

                        return result;
                    }

                    // Issue #5492: Record failure (circuit breaker + rate limit tracking delegated to selector)
                    _providerSelector.RecordFailure(client.ProviderName, decision.ModelId, attemptStopwatch.ElapsedMilliseconds, result);

                    await _costService.LogFailureAsync(result.ErrorMessage, costContext, attemptStopwatch.ElapsedMilliseconds, source, CancellationToken.None).ConfigureAwait(false);
                    lastFailure = NormalizeFailureResult(result, client.ProviderName);
                }
            }
            catch (Exception ex)
            {
                attemptStopwatch.Stop();
                _providerSelector.RecordFailure(client.ProviderName, decision.ModelId, attemptStopwatch.ElapsedMilliseconds);

                _logger.LogError(ex,
                    "Error generating completion with {Provider} ({Model}) - Circuit state: {CircuitState}",
                    client.ProviderName, decision.ModelId,
                    _circuitBreakerRegistry.GetCircuitStateDescription(client.ProviderName));

                await _costService.LogFailureAsync(ex.Message, costContext, attemptStopwatch.ElapsedMilliseconds, source, CancellationToken.None).ConfigureAwait(false);
                lastFailure = LlmCompletionResult.CreateFailure($"Provider error: {ex.Message}");
            }
            finally
            {
                CompleteAdmission(admission, actualCost);
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

        // Map User entity to KB Domain value object (Application layer ACL)
        var userContext = LlmUserContextMapper.FromUser(user);

        var selection = await _providerSelector.SelectProviderAsync(userContext, strategy, source, cancellationToken)
            .ConfigureAwait(false);

        var client = selection.Client;
        if (client == null)
        {
            _logger.LogError("No client found for streaming, falling back to first available");
            client = _clients[0];
        }

        var (degradedClient, degradedDecision) = await ApplyBudgetDegradeAsync(client, selection.Decision, source, cancellationToken)
            .ConfigureAwait(false);
        client = degradedClient;
        selection = selection with { Decision = degradedDecision };

        _logger.LogInformation(
            "Starting streaming completion via {Provider} ({Model}) - Reason: {Reason}",
            client.ProviderName, selection.Decision.ModelId, selection.Decision.Reason);

        // Issue #5510: Redact PII for OpenRouter-bound streaming prompts
        var effectiveSystemPrompt = systemPrompt;
        var effectiveUserPrompt = userPrompt;
        if (_piiDetector != null && string.Equals(client.ProviderName, "OpenRouter", StringComparison.Ordinal))
        {
            effectiveSystemPrompt = _piiDetector.ScanAndRedact(systemPrompt);
            effectiveUserPrompt = _piiDetector.ScanAndRedact(userPrompt);
        }

        var admission = await AdmitAsync(source, selection.Decision.ModelId, client.ProviderName,
            effectiveSystemPrompt.Length + effectiveUserPrompt.Length, DefaultMaxTokens, 0, cancellationToken).ConfigureAwait(false);
        if (!admission.IsAllowed)
        {
            yield return RefusedChunk(admission);
            yield break;
        }

        var stream = client.GenerateCompletionStreamAsync(
            selection.Decision.ModelId,
            effectiveSystemPrompt,
            effectiveUserPrompt,
            DefaultTemperature,
            DefaultMaxTokens,
            cancellationToken);

        await foreach (var chunk in WithCostLoggingAsync(stream, AttributeCost(userContext), source, admission, cancellationToken)
            .ConfigureAwait(false))
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
        int? maxTokens = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(explicitModel);
        ArgumentException.ThrowIfNullOrWhiteSpace(systemPrompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(userPrompt);

        var effectiveMaxTokens = maxTokens ?? DefaultMaxTokens;

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

        var admission = await AdmitAsync(source, explicitModel, providerName,
            systemPrompt.Length + userPrompt.Length, effectiveMaxTokens, 0, ct).ConfigureAwait(false);
        if (!admission.IsAllowed)
        {
            return Refused(admission);
        }

        var stopwatch = Stopwatch.StartNew();
        var actualCost = 0m;

        try
        {
            _logger.LogInformation(
                "Generating completion with explicit model {Model} via {Provider} (max_tokens={MaxTokens})",
                explicitModel, providerName, effectiveMaxTokens);

            var result = await client.GenerateCompletionAsync(
                explicitModel,
                systemPrompt,
                userPrompt,
                DefaultTemperature,
                effectiveMaxTokens,
                ct).ConfigureAwait(false);

            stopwatch.Stop();

            if (!result.Success)
            {
                _logger.LogWarning("Explicit model {Model} call failed: {Error}", explicitModel, result.ErrorMessage);
                _providerSelector.RecordFailure(providerName, explicitModel, stopwatch.ElapsedMilliseconds, result);
                _ = _costService.LogFailureAsync(result.ErrorMessage, AttributeCost(LlmUserContext.Anonymous), stopwatch.ElapsedMilliseconds, source, CancellationToken.None);
                return result;
            }

            _providerSelector.RecordSuccess(providerName, explicitModel, stopwatch.ElapsedMilliseconds, result);
            actualCost = result.Cost.TotalCost;

            // Log cost asynchronously (fire-and-forget — use CancellationToken.None to survive request cancellation)
            _ = _costService.LogSuccessAsync(result, AttributeCost(LlmUserContext.Anonymous), stopwatch.ElapsedMilliseconds, source, CancellationToken.None);

            _logger.LogInformation(
                "Explicit model {Model} completion successful (tokens: {Tokens}, cost: ${Cost:F6}, latency: {Latency}ms)",
                explicitModel, result.Usage.TotalTokens, result.Cost.TotalCost, stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Unexpected error generating completion with explicit model {Model}", explicitModel);
            _providerSelector.RecordFailure(providerName, explicitModel, stopwatch.ElapsedMilliseconds);
            _ = _costService.LogFailureAsync(ex.Message, AttributeCost(LlmUserContext.Anonymous), stopwatch.ElapsedMilliseconds, source, CancellationToken.None);
            return LlmCompletionResult.CreateFailure($"Error: {ex.Message}");
        }
        finally
        {
            CompleteAdmission(admission, actualCost);
        }
    }

    /// <inheritdoc/>
#pragma warning disable S3400 // Methods should not return constants - async iterator pattern requires this signature
#pragma warning disable S4456 // Parameter validation on async iterator - validated before yield
    public async IAsyncEnumerable<StreamChunk> GenerateCompletionStreamWithModelAsync(
        string explicitModel,
        string systemPrompt,
        string userPrompt,
        RequestSource source,
        double? temperature = null,
        int? maxTokens = null,
        [EnumeratorCancellation] CancellationToken ct = default)
#pragma warning restore S4456
#pragma warning restore S3400
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(explicitModel);
        ArgumentNullException.ThrowIfNull(systemPrompt);
        ArgumentNullException.ThrowIfNull(userPrompt);

        var effectiveMaxTokens = maxTokens ?? DefaultMaxTokens;
        var client = _clients.FirstOrDefault(c => c.SupportsModel(explicitModel));
        if (client == null)
        {
            _logger.LogError("No client found supporting model {Model}", explicitModel);
            yield return new StreamChunk(null, IsFinal: true,
                ErrorCode: "AI_MODEL_UNSUPPORTED", ErrorMessage: $"No provider supports model: {explicitModel}");
            yield break;
        }

        if (_circuitBreakerRegistry.GetState(client.ProviderName) == CircuitState.Open)
        {
            _logger.LogWarning("Circuit breaker OPEN for {Provider}, skipping explicit model stream", client.ProviderName);
            yield return new StreamChunk(null, IsFinal: true,
                ErrorCode: "AI_PROVIDER_UNAVAILABLE", ErrorMessage: $"Provider {client.ProviderName} circuit breaker open");
            yield break;
        }

        var admission = await AdmitAsync(source, explicitModel, client.ProviderName,
            systemPrompt.Length + userPrompt.Length, effectiveMaxTokens, 0, ct).ConfigureAwait(false);
        if (!admission.IsAllowed)
        {
            yield return RefusedChunk(admission);
            yield break;
        }

        _logger.LogInformation(
            "Starting streaming completion with explicit model {Model} via {Provider} (max_tokens={MaxTokens})",
            explicitModel, client.ProviderName, effectiveMaxTokens);

        var stream = client.GenerateCompletionStreamAsync(
            explicitModel, systemPrompt, userPrompt, temperature ?? DefaultTemperature, effectiveMaxTokens, ct);

        await foreach (var chunk in WithCostLoggingAsync(stream, AttributeCost(LlmUserContext.Anonymous), source, admission, ct)
            .ConfigureAwait(false))
        {
            yield return chunk;
        }
    }

    /// <inheritdoc/>
    public async Task<LlmCompletionResult> GenerateCompletionWithModelFallbackAsync(
        string preferredModel,
        string systemPrompt,
        string userPrompt,
        RequestSource source = RequestSource.Manual,
        int? maxTokens = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(preferredModel);
        ArgumentException.ThrowIfNullOrWhiteSpace(systemPrompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(userPrompt);

        var effectiveMaxTokens = maxTokens ?? DefaultMaxTokens;

        // Issue #2961: the first attempt uses the explicit PREFERRED model (selects its provider
        // via SupportsModel, like GenerateCompletionWithModelAsync). On a hard failure we then
        // reuse the SAME DB-driven fallback engine as the tier path (ILlmProviderSelector.
        // GetNextFallbackAsync) instead of duplicating the provider ordering — so a single
        // provider outage (e.g. DeepSeek 402) does not abort the Mechanic Extractor pipeline.
        var client = _clients.FirstOrDefault(c => c.SupportsModel(preferredModel));
        if (client == null)
        {
            _logger.LogError("No client found supporting preferred model {Model}", preferredModel);
            return LlmCompletionResult.CreateFailure($"No provider supports model: {preferredModel}");
        }

        var currentModel = preferredModel;
        var attemptedProviders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        LlmCompletionResult? lastFailure = null;

        while (client != null && attemptedProviders.Add(client.ProviderName))
        {
            // Review #2990: fail fast when the current provider's circuit is already OPEN, matching
            // GenerateCompletionWithModelAsync. The first (preferred) client is selected via
            // SupportsModel — bypassing the selector's circuit gating — so without this check a
            // known-broken provider (e.g. DeepSeek tripped by sustained 402s) would incur a wasted
            // live call on every section before falling back. Subsequent fallback hops are already
            // circuit-gated by GetNextFallbackAsync, so this is a harmless recheck for them.
            if (_circuitBreakerRegistry.GetState(client.ProviderName) == CircuitState.Open)
            {
                _logger.LogWarning(
                    "Circuit breaker OPEN for {Provider}, skipping call and trying fallback", client.ProviderName);
                lastFailure = LlmCompletionResult.CreateFailure($"Provider {client.ProviderName} circuit breaker open");
            }
            else
            {
                var admission = await AdmitAsync(source, currentModel, client.ProviderName,
                    systemPrompt.Length + userPrompt.Length, effectiveMaxTokens, 0, ct).ConfigureAwait(false);
                if (!admission.IsAllowed && admission.Rejection != LlmBudgetRejection.ModelNotInCatalog)
                {
                    return Refused(admission);
                }

                var stopwatch = Stopwatch.StartNew();
                var actualCost = 0m;
                try
                {
                    if (!admission.IsAllowed)
                    {
                        lastFailure = Refused(admission);
                    }
                    else
                    {
                        _logger.LogInformation(
                            "Generating completion with model {Model} via {Provider} (max_tokens={MaxTokens}, multi-provider fallback enabled)",
                            currentModel, client.ProviderName, effectiveMaxTokens);

                        var result = await client.GenerateCompletionAsync(
                            currentModel, systemPrompt, userPrompt, DefaultTemperature, effectiveMaxTokens, ct)
                            .ConfigureAwait(false);

                        stopwatch.Stop();

                        if (result.Success)
                        {
                            actualCost = result.Cost.TotalCost;
                            _providerSelector.RecordSuccess(client.ProviderName, currentModel, stopwatch.ElapsedMilliseconds, result);
                            // Fire-and-forget cost logging (CancellationToken.None to survive request cancellation).
                            _ = _costService.LogSuccessAsync(result, AttributeCost(LlmUserContext.Anonymous), stopwatch.ElapsedMilliseconds, source, CancellationToken.None);
                            return result;
                        }

                        _logger.LogWarning(
                            "Model {Model} via {Provider} failed: {Error} — attempting provider fallback",
                            currentModel, client.ProviderName, result.ErrorMessage);
                        _providerSelector.RecordFailure(client.ProviderName, currentModel, stopwatch.ElapsedMilliseconds, result);
                        _ = _costService.LogFailureAsync(result.ErrorMessage, AttributeCost(LlmUserContext.Anonymous), stopwatch.ElapsedMilliseconds, source, CancellationToken.None);
                        lastFailure = result;
                    }
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    _logger.LogError(ex, "Error generating completion with model {Model} via {Provider}", currentModel, client.ProviderName);
                    _providerSelector.RecordFailure(client.ProviderName, currentModel, stopwatch.ElapsedMilliseconds);
                    _ = _costService.LogFailureAsync(ex.Message, AttributeCost(LlmUserContext.Anonymous), stopwatch.ElapsedMilliseconds, source, CancellationToken.None);
                    lastFailure = LlmCompletionResult.CreateFailure($"Error: {ex.Message}");
                }
                finally
                {
                    CompleteAdmission(admission, actualCost);
                }
            }

            // Advance to the next available provider in the DB-driven fallback chain. The selector
            // skips already-attempted providers and honours circuit-breaker/health/enabled state.
            var nextSelection = await _providerSelector.GetNextFallbackAsync(client.ProviderName, attemptedProviders, ct)
                .ConfigureAwait(false);
            client = nextSelection.Client;
            if (client != null)
            {
                currentModel = nextSelection.Decision.ModelId;
            }
            else
            {
                _logger.LogWarning(
                    "No additional fallback providers available after failures: {Providers}",
                    string.Join(", ", attemptedProviders));
            }
        }

        return lastFailure ?? LlmCompletionResult.CreateFailure("Provider error: No providers available");
    }

    /// <inheritdoc/>
    public async Task<LlmCompletionResult> GenerateMultimodalCompletionAsync(
        IReadOnlyList<LlmMessage> messages,
        RequestSource source = RequestSource.Manual,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(messages);
        if (messages.Count == 0)
        {
            return LlmCompletionResult.CreateFailure("No messages provided");
        }

        var needsVision = messages.Any(m => m.HasImages);

        // Try to find a vision-capable provider first
        if (needsVision)
        {
            var visionClient = _clients.FirstOrDefault(c => c.SupportsVision);
            if (visionClient != null)
            {
                var selection = await _providerSelector.SelectProviderAsync(
                    LlmUserContext.Anonymous, RagStrategy.Balanced, source, ct).ConfigureAwait(false);
                var decision = selection.Decision;

                // Prefer the routed provider if it supports vision, else use first vision provider
                var client = selection.Client?.SupportsVision == true ? selection.Client : visionClient;

                _logger.LogInformation(
                    "Generating multimodal completion via {Provider} ({Model})",
                    client.ProviderName, decision.ModelId);

                var admission = await AdmitAsync(source, decision.ModelId, client.ProviderName,
                    TextCharacters(messages), DefaultMaxTokens, ImageCount(messages), ct).ConfigureAwait(false);
                if (!admission.IsAllowed)
                {
                    return Refused(admission);
                }

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var actualCost = 0m;
                try
                {
                    var result = await client.GenerateCompletionAsync(
                        decision.ModelId, messages, DefaultTemperature, DefaultMaxTokens, ct).ConfigureAwait(false);

                    stopwatch.Stop();

                    if (result.Success)
                    {
                        actualCost = result.Cost.TotalCost;
                        _providerSelector.RecordSuccess(client.ProviderName, decision.ModelId, stopwatch.ElapsedMilliseconds, result);
                        await _costService.LogSuccessAsync(result, AttributeCost(LlmUserContext.Anonymous), stopwatch.ElapsedMilliseconds, source, CancellationToken.None).ConfigureAwait(false);
                        return result;
                    }

                    _providerSelector.RecordFailure(client.ProviderName, decision.ModelId, stopwatch.ElapsedMilliseconds, result);
                    await _costService.LogFailureAsync(result.ErrorMessage, AttributeCost(LlmUserContext.Anonymous), stopwatch.ElapsedMilliseconds, source, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    _logger.LogError(ex, "Error generating multimodal completion with {Provider}", client.ProviderName);
                    _providerSelector.RecordFailure(client.ProviderName, decision.ModelId, stopwatch.ElapsedMilliseconds);
                    await _costService.LogFailureAsync(ex.Message, AttributeCost(LlmUserContext.Anonymous), stopwatch.ElapsedMilliseconds, source, CancellationToken.None).ConfigureAwait(false);
                }
                finally
                {
                    CompleteAdmission(admission, actualCost);
                }
            }
            else
            {
                _logger.LogWarning("No vision-capable provider available, falling back to text-only");
            }
        }

        // Fallback: extract text from messages and use standard text-only completion
        var systemParts = messages.Where(m => string.Equals(m.Role, "system", StringComparison.Ordinal))
            .SelectMany(m => m.Content.OfType<TextContentPart>().Select(t => t.Text));
        var userParts = messages.Where(m => string.Equals(m.Role, "user", StringComparison.Ordinal))
            .SelectMany(m => m.Content.OfType<TextContentPart>().Select(t => t.Text));

        var systemPrompt = string.Join("\n", systemParts);
        var userPrompt = string.Join("\n", userParts);

        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            return LlmCompletionResult.CreateFailure("No text content found in messages");
        }

        return await GenerateCompletionAsync(systemPrompt, userPrompt, source, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
#pragma warning disable S3400 // Methods should not return constants - async iterator pattern requires this signature
#pragma warning disable S4456 // Parameter validation on async iterator - validated before yield
    public async IAsyncEnumerable<StreamChunk> GenerateMultimodalCompletionStreamAsync(
        IReadOnlyList<LlmMessage> messages,
        RequestSource source = RequestSource.Manual,
        [EnumeratorCancellation] CancellationToken ct = default)
#pragma warning restore S4456
#pragma warning restore S3400
    {
        ArgumentNullException.ThrowIfNull(messages);
        if (messages.Count == 0)
        {
            _logger.LogWarning("No messages provided for multimodal streaming");
            yield break;
        }

        var needsVision = messages.Any(m => m.HasImages);

        if (needsVision)
        {
            var visionClient = _clients.FirstOrDefault(c => c.SupportsVision);
            if (visionClient != null)
            {
                var selection = await _providerSelector.SelectProviderAsync(
                    LlmUserContext.Anonymous, RagStrategy.Balanced, source, ct).ConfigureAwait(false);
                var decision = selection.Decision;

                var client = selection.Client?.SupportsVision == true ? selection.Client : visionClient;

                _logger.LogInformation(
                    "Starting multimodal streaming via {Provider} ({Model})",
                    client.ProviderName, decision.ModelId);

                var admission = await AdmitAsync(source, decision.ModelId, client.ProviderName,
                    TextCharacters(messages), DefaultMaxTokens, ImageCount(messages), ct).ConfigureAwait(false);
                if (!admission.IsAllowed)
                {
                    yield return RefusedChunk(admission);
                    yield break;
                }

                var stream = client.GenerateCompletionStreamAsync(
                    decision.ModelId, messages, DefaultTemperature, DefaultMaxTokens, ct);

                await foreach (var chunk in WithCostLoggingAsync(stream, AttributeCost(LlmUserContext.Anonymous), source, admission, ct)
                    .ConfigureAwait(false))
                {
                    yield return chunk;
                }

                yield break;
            }

            _logger.LogWarning("No vision-capable provider available for streaming, falling back to text-only");
        }

        // Fallback: extract text and use standard streaming
        var systemParts = messages.Where(m => string.Equals(m.Role, "system", StringComparison.Ordinal))
            .SelectMany(m => m.Content.OfType<TextContentPart>().Select(t => t.Text));
        var userParts = messages.Where(m => string.Equals(m.Role, "user", StringComparison.Ordinal))
            .SelectMany(m => m.Content.OfType<TextContentPart>().Select(t => t.Text));

        var systemPrompt = string.Join("\n", systemParts);
        var userPrompt = string.Join("\n", userParts);

        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            _logger.LogWarning("No text content found in messages for streaming");
            yield break;
        }

        await foreach (var chunk in GenerateCompletionStreamAsync(systemPrompt, userPrompt, source, ct).ConfigureAwait(false))
        {
            yield return chunk;
        }
    }

    /// <summary>
    /// #4164 (ADR-095 fetta 1b): a chi va il costo. Il contesto usato per instradare resta quello
    /// ricevuto; per il registro, se non porta un utente, si usa l'utente della richiesta in corso.
    /// </summary>
    private LlmUserContext AttributeCost(LlmUserContext routed) =>
        routed.UserId is not null ? routed : _callerAccessor?.Current ?? routed;

    /// <summary>
    /// #4166 (ADR-095 D6): il controllo del tetto prima di una chiamata. Senza gate, cioè in un
    /// gateway costruito a mano nei test, la chiamata passa: in DI il gate c'è sempre.
    /// </summary>
    private Task<LlmBudgetAdmission> AdmitAsync(
        RequestSource source,
        string modelId,
        string provider,
        int promptCharacters,
        int maxOutputTokens,
        int imageCount,
        CancellationToken cancellationToken) =>
        _budgetGate is null
            ? Task.FromResult(LlmBudgetAdmission.Unchecked(LlmBudgetClassification.For(source)))
            : _budgetGate.AdmitAsync(
                new LlmBudgetRequest(source, modelId, provider, promptCharacters, maxOutputTokens, imageCount),
                cancellationToken);

    private void CompleteAdmission(LlmBudgetAdmission admission, decimal actualCostUsd) =>
        _budgetGate?.Complete(admission, actualCostUsd);

    /// <summary>
    /// #4166 (ADR-095 D6): fra la soglia di degrado e il tetto, una risposta instradata passa al
    /// modello di riserva della versione pubblicata del profilo. Il background a quel punto è già
    /// fermo nel gate; le chiamate a modello esplicito tengono il loro modello.
    /// </summary>
    private async Task<(ILlmClient Client, LlmRoutingDecision Decision)> ApplyBudgetDegradeAsync(
        ILlmClient client,
        LlmRoutingDecision decision,
        RequestSource source,
        CancellationToken cancellationToken)
    {
        if (_budgetGate is null || _reserveModels is null
            || LlmBudgetClassification.For(source) != LlmBudgetClass.Answer
            || !await _budgetGate.IsDegradedAsync(cancellationToken).ConfigureAwait(false))
        {
            return (client, decision);
        }

        var reserve = await _reserveModels.GetReserveModelAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(reserve) || string.Equals(reserve, decision.ModelId, StringComparison.Ordinal))
        {
            return (client, decision);
        }

        var reserveClient = _clients.FirstOrDefault(c => c.SupportsModel(reserve));
        if (reserveClient is null || _circuitBreakerRegistry.GetState(reserveClient.ProviderName) == CircuitState.Open)
        {
            _logger.LogWarning(
                "Budget degrade: the reserve model {Reserve} has no available provider, keeping {Model}",
                reserve, decision.ModelId);
            return (client, decision);
        }

        _logger.LogWarning(
            "Budget degrade: today's LLM spend reached the degrade threshold, answering with the reserve model {Reserve} instead of {Model}",
            reserve, decision.ModelId);
        return (reserveClient, decision with
        {
            ProviderName = reserveClient.ProviderName,
            ModelId = reserve,
            Reason = $"{decision.Reason} (budget degrade: reserve model of the agent profile)",
        });
    }

    /// <summary>Il risultato di una chiamata che il gateway non ha fatto partire.</summary>
    private static LlmCompletionResult Refused(LlmBudgetAdmission admission)
    {
        var failure = LlmCompletionResult.CreateFailure(admission.Message ?? "The AI call was not made.");
        return failure with
        {
            Metadata = new Dictionary<string, string>(failure.Metadata, StringComparer.Ordinal)
            {
                ["budget_rejection"] = admission.Rejection.ToString(),
                ["error_code"] = admission.ErrorCode ?? LlmBudgetAdmission.DailyBudgetErrorCode,
            },
        };
    }

    /// <summary>
    /// Lo stream di una chiamata che il gateway non ha fatto partire: un solo chunk finale, senza
    /// contenuto, con il motivo. Un consumatore che lo ignora vede uno stream vuoto, come per un
    /// provider che fallisce.
    /// </summary>
    private static StreamChunk RefusedChunk(LlmBudgetAdmission admission) =>
        new(Content: null, IsFinal: true, ErrorCode: admission.ErrorCode, ErrorMessage: admission.Message);

    private static int TextCharacters(IReadOnlyList<LlmMessage> messages) =>
        messages.Sum(m => m.Content.OfType<TextContentPart>().Sum(t => t.Text.Length));

    private static int ImageCount(IReadOnlyList<LlmMessage> messages) =>
        messages.Sum(m => m.Content.Count(part => part is ImageContentPart));

    /// <summary>
    /// #4164: registra il costo di uno stream, che prima non finiva mai nel registro. Il costo arriva
    /// con il chunk finale (<see cref="StreamChunk.Usage"/> e <see cref="StreamChunk.Cost"/>), che
    /// tutti e tre i client emettono a fine stream.
    /// </summary>
    /// <remarks>
    /// I client non lanciano: su un errore lo scrivono nei log e chiudono lo stream. Per questo uno
    /// stream che finisce senza contenuto e senza costo conta come fallimento. Uno stream con
    /// contenuto ma senza costo (Ollama senza conteggi), o interrotto dal consumatore prima della
    /// fine, non ha un costo da registrare: resta un warning.
    /// </remarks>
    private async IAsyncEnumerable<StreamChunk> WithCostLoggingAsync(
        IAsyncEnumerable<StreamChunk> stream,
        LlmUserContext costContext,
        RequestSource source,
        LlmBudgetAdmission admission,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        StreamChunk? costChunk = null;
        var sawContent = false;
        var completed = false;
        string? failure = null;

        var enumerator = stream.GetAsyncEnumerator(cancellationToken);
        try
        {
            while (true)
            {
                bool hasNext;
                try
                {
                    hasNext = await enumerator.MoveNextAsync().ConfigureAwait(false);
                }
                // Un annullamento chiesto dal consumatore non è un fallimento del provider; un
                // timeout di HttpClient sì, anche se arriva come TaskCanceledException.
                catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
                {
#pragma warning disable S1854 // Falso positivo: «failure» lo legge il finally qui sotto, dopo il throw
                    failure = ex.Message;
#pragma warning restore S1854
                    throw;
                }

                if (!hasNext)
                {
                    completed = true;
                    break;
                }

                var chunk = enumerator.Current;
                if (chunk.Usage is not null && chunk.Cost is not null)
                {
                    costChunk = chunk;
                }
                else if (!string.IsNullOrEmpty(chunk.Content))
                {
                    sawContent = true;
                }

                yield return chunk;
            }
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
            stopwatch.Stop();
            // I client escono dal loop in silenzio quando il token viene annullato: senza questo
            // controllo una disconnessione prima del primo contenuto risulterebbe un fallimento.
            var cancelledByConsumer = cancellationToken.IsCancellationRequested;
            await LogStreamOutcomeAsync(costChunk, sawContent, completed && !cancelledByConsumer, failure, costContext, stopwatch.ElapsedMilliseconds, source)
                .ConfigureAwait(false);
            // #4166: uno stream interrotto dopo aver prodotto testo è stato fatturato, ma il suo costo
            // non arriva (manca il chunk finale): il gate lo conta con la stima.
            if (costChunk?.Cost is { } streamCost)
            {
                CompleteAdmission(admission, streamCost.TotalCost);
            }
            else if (sawContent)
            {
                _budgetGate?.Complete(admission, admission.EstimatedCostUsd, recordedInCostLog: false);
            }
            else
            {
                CompleteAdmission(admission, 0m);
            }
        }
    }

    private async Task LogStreamOutcomeAsync(
        StreamChunk? costChunk,
        bool sawContent,
        bool completed,
        string? failure,
        LlmUserContext costContext,
        long latencyMs,
        RequestSource source)
    {
        // CancellationToken.None: la registrazione deve sopravvivere alla fine della richiesta.
        if (costChunk is { Usage: { } usage, Cost: { } cost })
        {
            await _costService.LogSuccessAsync(
                LlmCompletionResult.CreateSuccess(string.Empty, usage, cost),
                costContext, latencyMs, source, CancellationToken.None).ConfigureAwait(false);
        }
        else if (failure is not null)
        {
            await _costService.LogFailureAsync(failure, costContext, latencyMs, source, CancellationToken.None)
                .ConfigureAwait(false);
        }
        else if (completed && !sawContent)
        {
            await _costService.LogFailureAsync(
                "Streaming ended without content or usage: the provider call failed",
                costContext, latencyMs, source, CancellationToken.None).ConfigureAwait(false);
        }
        else
        {
            _logger.LogWarning(
                "Streaming {Outcome} without usage metadata: its cost is not recorded",
                completed ? "completed" : "stopped before the end");
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
            metadata["circuit_state"] = _circuitBreakerRegistry.GetCircuitStateDescription(client.ProviderName);

            if (decision.UserRegion is not null)
            {
                metadata["user_region"] = decision.UserRegion;
            }
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
