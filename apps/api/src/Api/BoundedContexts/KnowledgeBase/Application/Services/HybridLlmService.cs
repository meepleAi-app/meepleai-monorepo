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
    private readonly ILlmCostCalculator? _costCalculator; // #4168: il costo dei token di uno stream interrotto

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
        IAgentReserveModelProvider? reserveModels = null,
        ILlmCostCalculator? costCalculator = null)
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
        _costCalculator = costCalculator;

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

        var routed = (Client: client, Decision: decision);
        (client, decision) = await ApplyBudgetDegradeAsync(client, decision, source, cancellationToken).ConfigureAwait(false);
        var onReserve = !string.Equals(decision.ModelId, routed.Decision.ModelId, StringComparison.Ordinal);

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

            // #4166: la riserva del degrado ha fallito. Si torna alla scelta dell'instradamento prima
            // della catena di fallback: il degrado non deve rendere una risposta meno disponibile.
            if (onReserve && !attemptedProviders.Contains(routed.Client.ProviderName))
            {
                _logger.LogWarning(
                    "Budget degrade: the reserve model {Reserve} failed, falling back to {Model}",
                    decision.ModelId, routed.Decision.ModelId);
                onReserve = false;
                (client, decision) = routed;
                continue;
            }

            onReserve = false;

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

        var answered = false;
        await foreach (var chunk in StreamRoutedAsync(degradedClient, degradedDecision, systemPrompt, userPrompt, userContext, source, cancellationToken)
            .ConfigureAwait(false))
        {
            answered |= !string.IsNullOrEmpty(chunk.Content) || chunk.IsFinal;
            yield return chunk;
        }

        // #4166: lo streaming non ha un fallback suo (C8). Se il degrado ha deviato dalla scelta
        // dell'instradamento e la riserva non ha prodotto nulla (provider giù, chiave rifiutata), la
        // risposta riparte sul modello instradato invece di arrivare vuota.
        if (!answered && !cancellationToken.IsCancellationRequested
            && !string.Equals(degradedDecision.ModelId, selection.Decision.ModelId, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Budget degrade: the reserve model {Reserve} produced no answer, falling back to {Model}",
                degradedDecision.ModelId, selection.Decision.ModelId);
            await foreach (var chunk in StreamRoutedAsync(client, selection.Decision, systemPrompt, userPrompt, userContext, source, cancellationToken)
                .ConfigureAwait(false))
            {
                yield return chunk;
            }
        }
    }

    /// <summary>
    /// Una chiamata in streaming a un client già scelto: redazione dei dati personali per OpenRouter,
    /// verifica del budget, registrazione del costo.
    /// </summary>
    private async IAsyncEnumerable<StreamChunk> StreamRoutedAsync(
        ILlmClient client,
        LlmRoutingDecision decision,
        string systemPrompt,
        string userPrompt,
        LlmUserContext userContext,
        RequestSource source,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Starting streaming completion via {Provider} ({Model}) - Reason: {Reason}",
            client.ProviderName, decision.ModelId, decision.Reason);

        // Issue #5510: Redact PII for OpenRouter-bound streaming prompts
        var effectiveSystemPrompt = systemPrompt;
        var effectiveUserPrompt = userPrompt;
        if (_piiDetector != null && string.Equals(client.ProviderName, "OpenRouter", StringComparison.Ordinal))
        {
            effectiveSystemPrompt = _piiDetector.ScanAndRedact(systemPrompt);
            effectiveUserPrompt = _piiDetector.ScanAndRedact(userPrompt);
        }

        var admission = await AdmitAsync(source, decision.ModelId, client.ProviderName,
            effectiveSystemPrompt.Length + effectiveUserPrompt.Length, DefaultMaxTokens, 0, cancellationToken).ConfigureAwait(false);
        if (!admission.IsAllowed)
        {
            yield return RefusedChunk(admission);
            yield break;
        }

        var stream = client.GenerateCompletionStreamAsync(
            decision.ModelId,
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

    // ─── #4168 (ADR-095 D3, passo 6): la risposta della pipeline ────────────────────────────────

    /// <summary>Il messaggio per l'utente quando nessun modello risponde: i dettagli restano nei log.</summary>
    private const string NoModelAnsweredMessage =
        "No AI model could answer right now. Please try again in a few minutes.";

    /// <summary>La riga del registro di uno stream che il consumatore ha smesso di leggere.</summary>
    private const string ClientStoppedFailure = "Stream stopped by the client before the end";

    /// <summary>Il messaggio per l'utente quando la risposta si interrompe dopo il primo token.</summary>
    private const string StreamInterruptedMessage =
        "The answer was interrupted before it was complete. Please ask again.";

    /// <summary>
    /// #4381: il motivo, per i log e il registro, di un tentativo che ha speso token senza scrivere testo.
    /// <c>length</c> vuol dire che il limite di token è finito prima del testo (per esempio nel ragionamento).
    /// </summary>
    private static string NoTextFailure(string? finishReason) =>
        $"The model spent its tokens without writing any text (finish reason: {finishReason ?? "unknown"})";

    /// <inheritdoc/>
    public async Task<LlmCompletionResult> GenerateAnswerAsync(LlmAnswerRequest request, CancellationToken ct = default)
    {
        // Resta async: un argomento non valido continua a uscire dal Task, come prima del refactor.
        ValidateAnswerRequest(request);
        return await RunAnswerAttemptsAsync(
            new AnswerAttemptPlan(request.PrimaryModelId, request.FallbackModelId, request.MaxTokens, request.ProfileVersionId, request.Source,
                RequiresText: true),
            (client, model) =>
            {
                var (systemPrompt, userPrompt) = RedactFor(client, request.SystemPrompt, request.UserPrompt);
                return new AnswerAttempt(
                    systemPrompt.Length + userPrompt.Length,
                    token => client is ILlmReasoningSwitch withoutReasoning
                        ? withoutReasoning.GenerateCompletionWithoutReasoningAsync(model, systemPrompt, userPrompt, request.Temperature, request.MaxTokens, token)
                        : client.GenerateCompletionAsync(model, systemPrompt, userPrompt, request.Temperature, request.MaxTokens, token));
            },
            "Answer",
            ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<LlmCompletionResult> ChatAsync(LlmChatRequest request, CancellationToken ct = default)
    {
        ValidateChatRequest(request);
        var toolCharacters = request.Tools.Sum(t => t.Name.Length + t.Description.Length + t.ParametersSchema.GetRawText().Length);
        return await RunAnswerAttemptsAsync(
            new AnswerAttemptPlan(request.PrimaryModelId, request.FallbackModelId, request.MaxTokens, request.ProfileVersionId, request.Source,
                RequiresText: false),
            (client, model) =>
            {
                var messages = RedactFor(client, request.Messages);
                var characters = toolCharacters + messages.Sum(m =>
                    (m.Content?.Length ?? 0) + (m.ToolCalls?.Sum(c => c.Name.Length + c.ArgumentsJson.Length) ?? 0));
                return new AnswerAttempt(
                    characters,
                    token => client.ChatAsync(model, messages, request.Tools, request.Temperature, request.MaxTokens, token));
            },
            "Chat",
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Ciò che serve al ciclo dei tentativi, uguale per la risposta e per la chat con strumenti.
    /// <paramref name="RequiresText"/>: una risposta senza testo è un tentativo fallito (#4381). Non vale
    /// per la chat con strumenti, dove un testo vuoto con delle chiamate è una risposta valida.
    /// </summary>
    private sealed record AnswerAttemptPlan(
        string PrimaryModelId, string? FallbackModelId, int MaxTokens, Guid? ProfileVersionId, RequestSource Source,
        bool RequiresText);

    /// <summary>Un tentativo già preparato per un client: i caratteri del prompt (per il tetto) e la chiamata.</summary>
    private sealed record AnswerAttempt(int PromptCharacters, Func<CancellationToken, Task<LlmCompletionResult>> Call);

    /// <summary>
    /// ADR-097: il ciclo dei tentativi di <see cref="GenerateAnswerAsync"/>, estratto perché la chat con
    /// strumenti lo riusi identico. Per ogni modello in ordine: client, circuit breaker, ammissione al
    /// tetto, chiamata, costo, chiusura dell'ammissione. Un tetto o una quota fermano tutto; un modello
    /// fuori catalogo passa al successivo.
    /// </summary>
    private async Task<LlmCompletionResult> RunAnswerAttemptsAsync(
        AnswerAttemptPlan plan,
        Func<ILlmClient, string, AnswerAttempt> prepare,
        string operation,
        CancellationToken ct)
    {
        var costContext = AttributeCost(LlmUserContext.Anonymous);
        LlmCompletionResult? catalogRefusal = null;
        var attemptedAny = false;
        string? lastError = null;

        foreach (var model in await OrderAnswerModelsAsync(plan.PrimaryModelId, plan.FallbackModelId, plan.Source, ct).ConfigureAwait(false))
        {
            var client = _clients.FirstOrDefault(c => c.SupportsModel(model));
            if (client is null)
            {
                _logger.LogWarning("{Operation}: no provider supports model {Model}, trying the next model", operation, model);
                continue;
            }

            if (_circuitBreakerRegistry.GetState(client.ProviderName) == CircuitState.Open)
            {
                _logger.LogWarning("{Operation}: circuit breaker OPEN for {Provider}, skipping model {Model}", operation, client.ProviderName, model);
                continue;
            }

            var attempt = prepare(client, model);
            var admission = await AdmitAsync(plan.Source, model, client.ProviderName,
                attempt.PromptCharacters, plan.MaxTokens, 0, ct).ConfigureAwait(false);
            if (!admission.IsAllowed)
            {
                // Un tetto o una quota fermano la risposta: la riserva spenderebbe dallo stesso budget.
                if (admission.Rejection != LlmBudgetRejection.ModelNotInCatalog)
                {
                    return Refused(admission) with { ProfileVersionId = plan.ProfileVersionId };
                }

                catalogRefusal = Refused(admission);
                continue;
            }

            attemptedAny = true;
            var stopwatch = Stopwatch.StartNew();
            var actualCost = 0m;
            try
            {
                var result = await attempt.Call(ct).ConfigureAwait(false);
                stopwatch.Stop();

                // I client trasformano anche l'annullamento in un risultato fallito: chi ha smesso di
                // aspettare è il chiamante, non il provider, quindi niente circuit breaker né riserva.
                if (!result.Success && ct.IsCancellationRequested)
                {
                    ct.ThrowIfCancellationRequested();
                }

                if (result.Success && plan.RequiresText && string.IsNullOrWhiteSpace(result.Response))
                {
                    // #4381: il modello ha speso i token senza scrivere niente. I token sono pagati e si
                    // registrano; per il resto è un tentativo fallito, e si passa alla riserva.
                    actualCost = result.Cost.TotalCost;
                    var failure = NoTextFailure(result.Metadata.GetValueOrDefault("finish_reason"));
                    _logger.LogWarning("{Operation}: model {Model} via {Provider} failed: {Error}", operation, model, client.ProviderName, failure);
                    _providerSelector.RecordFailure(client.ProviderName, model, stopwatch.ElapsedMilliseconds, result);
                    await _costService.LogSuccessAsync(result with { ProfileVersionId = plan.ProfileVersionId }, costContext,
                        stopwatch.ElapsedMilliseconds, plan.Source, CancellationToken.None).ConfigureAwait(false);
                    lastError = failure;
                    continue;
                }

                if (result.Success)
                {
                    actualCost = result.Cost.TotalCost;
                    result = result with { ProfileVersionId = plan.ProfileVersionId };
                    _providerSelector.RecordSuccess(client.ProviderName, model, stopwatch.ElapsedMilliseconds, result);
                    await _costService.LogSuccessAsync(result, costContext, stopwatch.ElapsedMilliseconds, plan.Source, CancellationToken.None)
                        .ConfigureAwait(false);
                    return result;
                }

                _logger.LogWarning("{Operation}: model {Model} via {Provider} failed: {Error}", operation, model, client.ProviderName, result.ErrorMessage);
                _providerSelector.RecordFailure(client.ProviderName, model, stopwatch.ElapsedMilliseconds, result);
                await _costService.LogFailureAsync(result.ErrorMessage, costContext, stopwatch.ElapsedMilliseconds, plan.Source, CancellationToken.None)
                    .ConfigureAwait(false);
                lastError = result.ErrorMessage;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "{Operation}: model {Model} via {Provider} threw", operation, model, client.ProviderName);
                _providerSelector.RecordFailure(client.ProviderName, model, stopwatch.ElapsedMilliseconds);
                await _costService.LogFailureAsync(ex.Message, costContext, stopwatch.ElapsedMilliseconds, plan.Source, CancellationToken.None)
                    .ConfigureAwait(false);
                lastError = ex.Message;
            }
            finally
            {
                CompleteAdmission(admission, actualCost);
            }
        }

        // Nessun modello è nel catalogo: il motivo è quello, non un provider giù.
        if (!attemptedAny && catalogRefusal is not null)
        {
            return catalogRefusal with { ProfileVersionId = plan.ProfileVersionId };
        }

        _logger.LogWarning("{Operation}: no model answered (last error: {Error})", operation, lastError ?? "none");
        var unavailable = LlmCompletionResult.CreateFailure(NoModelAnsweredMessage);
        return unavailable with
        {
            ProfileVersionId = plan.ProfileVersionId,
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["error_code"] = LlmAnswerErrorCodes.ProviderUnavailable,
            },
        };
    }

    private static void ValidateChatRequest(LlmChatRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.PrimaryModelId))
            throw new ArgumentException("A chat needs a primary model.", nameof(request));
        if (request.Messages is null || request.Messages.Count == 0)
            throw new ArgumentException("A chat needs at least one message.", nameof(request));
        if (request.Tools is null)
            throw new ArgumentException("Tools must not be null: pass an empty list.", nameof(request));
        if (request.Temperature < 0 || request.MaxTokens <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Temperature must be >= 0 and MaxTokens > 0.");
    }

    /// <summary>Issue #5510 per la chat: il testo dei messaggi esce redatto verso OpenRouter; gli argomenti JSON restano intatti.</summary>
    private IReadOnlyList<LlmChatMessage> RedactFor(ILlmClient client, IReadOnlyList<LlmChatMessage> messages) =>
        _piiDetector != null && string.Equals(client.ProviderName, "OpenRouter", StringComparison.Ordinal)
            ? messages.Select(m => m.Content is null ? m : m with { Content = _piiDetector.ScanAndRedact(m.Content) }).ToList()
            : messages;

    /// <inheritdoc/>
#pragma warning disable S4456 // Parameter validation on async iterator - validated before yield
    public async IAsyncEnumerable<StreamChunk> GenerateAnswerStreamAsync(
        LlmAnswerRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
#pragma warning restore S4456
    {
        ValidateAnswerRequest(request);
        var costContext = AttributeCost(LlmUserContext.Anonymous);
        LlmBudgetAdmission? catalogRefusal = null;
        var attemptedAny = false;

        foreach (var model in await OrderAnswerModelsAsync(request.PrimaryModelId, request.FallbackModelId, request.Source, ct).ConfigureAwait(false))
        {
            var client = _clients.FirstOrDefault(c => c.SupportsModel(model));
            if (client is null)
            {
                _logger.LogWarning("Answer stream: no provider supports model {Model}, trying the next model", model);
                continue;
            }

            if (_circuitBreakerRegistry.GetState(client.ProviderName) == CircuitState.Open)
            {
                _logger.LogWarning("Answer stream: circuit breaker OPEN for {Provider}, skipping model {Model}", client.ProviderName, model);
                continue;
            }

            var (systemPrompt, userPrompt) = RedactFor(client, request.SystemPrompt, request.UserPrompt);
            var admission = await AdmitAsync(request.Source, model, client.ProviderName,
                systemPrompt.Length + userPrompt.Length, request.MaxTokens, 0, ct).ConfigureAwait(false);
            if (!admission.IsAllowed)
            {
                if (admission.Rejection != LlmBudgetRejection.ModelNotInCatalog)
                {
                    yield return RefusedChunk(admission);
                    yield break;
                }

                catalogRefusal = admission;
                continue;
            }

            attemptedAny = true;
            var attempt = new AnswerStreamAttempt(client.ProviderName, model, systemPrompt.Length + userPrompt.Length);
            await foreach (var chunk in StreamAnswerAttemptAsync(client, systemPrompt, userPrompt, request, admission, costContext, attempt, ct)
                .ConfigureAwait(false))
            {
                yield return chunk;
            }

            if (attempt.Answered || ct.IsCancellationRequested)
            {
                yield break;
            }

            _logger.LogWarning(
                "Answer stream: model {Model} via {Provider} failed before the first token ({Error}), trying the next model",
                model, client.ProviderName, attempt.Failure);
        }

        if (!attemptedAny && catalogRefusal is not null)
        {
            yield return RefusedChunk(catalogRefusal);
            yield break;
        }

        yield return new StreamChunk(null, IsFinal: true,
            ErrorCode: LlmAnswerErrorCodes.ProviderUnavailable, ErrorMessage: NoModelAnsweredMessage);
    }

    /// <summary>
    /// Un tentativo su un modello. Ne tiene il resoconto in <paramref name="attempt"/>, perché il
    /// chiamante deve sapere se passare alla riserva: solo se il tentativo è fallito prima del primo
    /// token. Ogni esito aggiorna il circuit breaker (C8), tranne l'abbandono da parte del consumatore.
    /// </summary>
    private async IAsyncEnumerable<StreamChunk> StreamAnswerAttemptAsync(
        ILlmClient client,
        string systemPrompt,
        string userPrompt,
        LlmAnswerRequest request,
        LlmBudgetAdmission admission,
        LlmUserContext costContext,
        AnswerStreamAttempt attempt,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        StreamChunk? costChunk = null;
        var generatedCharacters = 0;
        // #4381: «ha scritto qualcosa» vuol dire testo, non token pagati. Finché non c'è testo, il chunk
        // finale con l'uso resta qui: se il tentativo finisce senza testo, il consumatore non lo vede.
        var producedText = false;
        StreamChunk? heldCostChunk = null;
        Exception? error = null;
        var loopExited = false;

        _logger.LogInformation(
            "Answer stream via {Provider} ({Model}), temperature {Temperature}, max tokens {MaxTokens}",
            attempt.Provider, attempt.Model, request.Temperature, request.MaxTokens);

        IAsyncEnumerator<StreamChunk>? enumerator = null;
        try
        {
            // #4381: le risposte chiedono di non ragionare, a un client che lo sa fare.
            var stream = client is ILlmReasoningSwitch withoutReasoning
                ? withoutReasoning.GenerateCompletionStreamWithoutReasoningAsync(
                    attempt.Model, systemPrompt, userPrompt, request.Temperature, request.MaxTokens, ct)
                : client.GenerateCompletionStreamAsync(
                    attempt.Model, systemPrompt, userPrompt, request.Temperature, request.MaxTokens, ct);
            enumerator = stream.GetAsyncEnumerator(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            error = ex;
        }

        if (enumerator is not null)
        {
            try
            {
                while (true)
                {
                    bool hasNext;
                    try
                    {
                        hasNext = await enumerator.MoveNextAsync().ConfigureAwait(false);
                    }
                    // Un annullamento chiesto dal consumatore non è un fallimento del provider; un timeout
                    // di HttpClient sì, anche se arriva come TaskCanceledException.
                    catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
                    {
                        error = ex;
                        loopExited = true;
                        break;
                    }

                    if (!hasNext)
                    {
                        loopExited = true;
                        break;
                    }

                    var chunk = enumerator.Current;
                    if (chunk.Usage is not null && chunk.Cost is not null)
                    {
                        costChunk = chunk;
                        if (!producedText && string.IsNullOrEmpty(chunk.Content))
                        {
                            heldCostChunk = chunk;
                            continue;
                        }
                    }

                    if (!producedText && !string.IsNullOrWhiteSpace(chunk.Content))
                    {
                        producedText = true;
                        if (heldCostChunk is not null)
                        {
                            yield return heldCostChunk;
                            heldCostChunk = null;
                        }
                    }

                    generatedCharacters += chunk.Content?.Length ?? 0;
                    yield return chunk;
                }
            }
            finally
            {
                await DisposeQuietlyAsync(enumerator, attempt).ConfigureAwait(false);
                stopwatch.Stop();

                // Il consumatore ha smesso di leggere a metà (disconnessione): niente circuit breaker,
                // che misura il provider, ma il costo resta da contare.
                if (!loopExited)
                {
                    attempt.Answered = true;
                    await SettleAnswerStreamAsync(attempt, request, admission, costContext, costChunk, generatedCharacters,
                        stopwatch.ElapsedMilliseconds, failure: ClientStoppedFailure).ConfigureAwait(false);
                }
            }
        }

        stopwatch.Stop();

        // Annullamento chiesto dal consumatore. I client non lanciano: escono dal ciclo in silenzio,
        // quindi lo stream sembra finito (vuoto o troncato). Non è un guasto del provider né una
        // risposta riuscita: niente circuit breaker, niente riserva, solo il conto.
        if (ct.IsCancellationRequested)
        {
            attempt.Answered = true;
            await SettleAnswerStreamAsync(attempt, request, admission, costContext, costChunk, generatedCharacters,
                stopwatch.ElapsedMilliseconds, failure: ClientStoppedFailure).ConfigureAwait(false);
            yield break;
        }

        if (error is not null && generatedCharacters > 0)
        {
            // D3: dopo il primo token non si passa a un secondo modello. Lo stream finisce con un
            // errore, il costo dei token generati entra nel registro e il circuit breaker conta il guasto.
            _logger.LogError(error, "Answer stream via {Provider} ({Model}) interrupted after {Characters} characters",
                attempt.Provider, attempt.Model, generatedCharacters);
            attempt.Answered = true;
            _providerSelector.RecordFailure(attempt.Provider, attempt.Model, stopwatch.ElapsedMilliseconds);
            await SettleAnswerStreamAsync(attempt, request, admission, costContext, costChunk, generatedCharacters,
                stopwatch.ElapsedMilliseconds, failure: $"Stream interrupted after the first token: {error.Message}").ConfigureAwait(false);
            yield return new StreamChunk(null, IsFinal: true,
                ErrorCode: LlmAnswerErrorCodes.StreamInterrupted, ErrorMessage: StreamInterruptedMessage);
            yield break;
        }

        if (error is null && producedText)
        {
            attempt.Answered = true;
            _providerSelector.RecordSuccess(attempt.Provider, attempt.Model, stopwatch.ElapsedMilliseconds,
                LlmCompletionResult.CreateSuccess(string.Empty, costChunk?.Usage, costChunk?.Cost));
            await SettleAnswerStreamAsync(attempt, request, admission, costContext, costChunk, generatedCharacters,
                stopwatch.ElapsedMilliseconds, failure: null).ConfigureAwait(false);
            yield break;
        }

        if (error is null && costChunk is not null)
        {
            // #4381: il modello ha speso i token senza scrivere testo (deepseek-flash col thinking che
            // esaurisce max_tokens). Non c'è stato un primo token, quindi è un fallimento di prima del
            // primo token: circuit breaker e riserva. I token però sono pagati, e il conto li registra.
            attempt.Failure = NoTextFailure(costChunk.FinishReason);
            _logger.LogWarning("Answer stream via {Provider} ({Model}): {Failure}", attempt.Provider, attempt.Model, attempt.Failure);
            _providerSelector.RecordFailure(attempt.Provider, attempt.Model, stopwatch.ElapsedMilliseconds);
            await SettleAnswerStreamAsync(attempt, request, admission, costContext, costChunk, generatedCharacters,
                stopwatch.ElapsedMilliseconds, failure: attempt.Failure).ConfigureAwait(false);
            yield break;
        }

        // Fallito prima del primo token: un'eccezione, o uno stream chiuso senza niente — i client
        // scrivono l'errore nei log e chiudono. Il chiamante passa alla riserva.
        attempt.Failure = error?.Message ?? "Streaming ended without content or usage: the provider call failed";
        _providerSelector.RecordFailure(attempt.Provider, attempt.Model, stopwatch.ElapsedMilliseconds);
        await _costService.LogFailureAsync(attempt.Failure, costContext, stopwatch.ElapsedMilliseconds, request.Source, CancellationToken.None)
            .ConfigureAwait(false);
        CompleteAdmission(admission, 0m);
    }

    /// <summary>
    /// Chiude il conto di uno stream che ha prodotto qualcosa: la riga del registro con la versione
    /// del profilo e la prenotazione del gate. Senza il chunk finale (stream interrotto o abbandonato)
    /// il costo si stima dai caratteri, a ~4 per token, sul listino del catalogo.
    /// </summary>
    private async Task SettleAnswerStreamAsync(
        AnswerStreamAttempt attempt,
        LlmAnswerRequest request,
        LlmBudgetAdmission admission,
        LlmUserContext costContext,
        StreamChunk? costChunk,
        int generatedCharacters,
        long latencyMs,
        string? failure)
    {
        try
        {
            await LogAndCompleteAsync(attempt, request, admission, costContext, costChunk, generatedCharacters, latencyMs, failure)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Ogni ramo chiude la prenotazione come ultima istruzione: se si arriva qui, non è chiusa.
            _logger.LogWarning(ex, "Answer stream via {Provider} ({Model}): recording the cost failed, the gate keeps the estimate",
                attempt.Provider, attempt.Model);
            _budgetGate?.Complete(admission, admission.EstimatedCostUsd, recordedInCostLog: false);
        }
    }

    private async Task LogAndCompleteAsync(
        AnswerStreamAttempt attempt,
        LlmAnswerRequest request,
        LlmBudgetAdmission admission,
        LlmUserContext costContext,
        StreamChunk? costChunk,
        int generatedCharacters,
        long latencyMs,
        string? failure)
    {
        if (costChunk is { Usage: { } usage, Cost: { } cost })
        {
            var completed = LlmCompletionResult.CreateSuccess(string.Empty, usage, cost) with
            {
                ProfileVersionId = request.ProfileVersionId,
            };
            await _costService.LogSuccessAsync(completed, costContext, latencyMs, request.Source, CancellationToken.None)
                .ConfigureAwait(false);
            CompleteAdmission(admission, cost.TotalCost);
            return;
        }

        if (generatedCharacters == 0)
        {
            CompleteAdmission(admission, 0m);
            return;
        }

        if (_costCalculator is null)
        {
            _logger.LogWarning("Answer stream via {Provider} ({Model}) has no usage and no cost calculator: its cost is not recorded",
                attempt.Provider, attempt.Model);
            _budgetGate?.Complete(admission, admission.EstimatedCostUsd, recordedInCostLog: false);
            return;
        }

        var promptTokens = Math.Max(1, attempt.PromptCharacters / 4);
        var completionTokens = Math.Max(1, generatedCharacters / 4);
        var estimate = _costCalculator.CalculateCost(attempt.Model, attempt.Provider, promptTokens, completionTokens);
        var estimated = new LlmCompletionResult
        {
            Success = failure is null,
            ErrorMessage = failure,
            Usage = new LlmUsage(promptTokens, completionTokens, promptTokens + completionTokens),
            Cost = new LlmCost
            {
                InputCost = estimate.InputCost,
                OutputCost = estimate.OutputCost,
                ModelId = attempt.Model,
                Provider = attempt.Provider,
                PricingStatus = estimate.PricingStatus,
            },
            ProfileVersionId = request.ProfileVersionId,
        };
        await _costService.LogSuccessAsync(estimated, costContext, latencyMs, request.Source, CancellationToken.None)
            .ConfigureAwait(false);
        CompleteAdmission(admission, estimate.TotalCost);
    }

    /// <summary>
    /// Il dispose di un client non deve impedire di chiudere il conto: un'eccezione qui lascerebbe
    /// aperta la prenotazione del gate e il circuit breaker senza esito.
    /// </summary>
    private async Task DisposeQuietlyAsync(IAsyncEnumerator<StreamChunk> enumerator, AnswerStreamAttempt attempt)
    {
        try
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Answer stream via {Provider} ({Model}): disposing the provider stream failed",
                attempt.Provider, attempt.Model);
        }
    }

    /// <summary>
    /// L'ordine in cui provare i modelli di una risposta: il principale, poi la riserva. Quando il
    /// tetto è in degrado (D6) e la riserva costa meno, la riserva passa davanti; il principale resta
    /// dietro, perché il degrado non deve rendere una risposta meno disponibile.
    /// </summary>
    private async Task<IReadOnlyList<string>> OrderAnswerModelsAsync(
        string primary, string? reserve, RequestSource source, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reserve) || string.Equals(reserve, primary, StringComparison.Ordinal))
        {
            return [primary];
        }

        if (_budgetGate is not null
            && LlmBudgetClassification.For(source) == LlmBudgetClass.Answer
            && await _budgetGate.IsDegradedAsync(ct).ConfigureAwait(false))
        {
            var primaryClient = _clients.FirstOrDefault(c => c.SupportsModel(primary));
            var reserveClient = _clients.FirstOrDefault(c => c.SupportsModel(reserve));
            if (primaryClient is not null && reserveClient is not null
                && await _budgetGate.IsCheaperReserveAsync(
                    primary, primaryClient.ProviderName, reserve, reserveClient.ProviderName, ct).ConfigureAwait(false))
            {
                _logger.LogWarning(
                    "Budget degrade: today's LLM spend reached the degrade threshold, answering with the reserve model {Reserve} instead of {Model}",
                    reserve, primary);
                return [reserve, primary];
            }
        }

        return [primary, reserve];
    }

    private static void ValidateAnswerRequest(LlmAnswerRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.PrimaryModelId))
            throw new ArgumentException("An answer needs a primary model.", nameof(request));
        if (request.SystemPrompt is null || string.IsNullOrWhiteSpace(request.UserPrompt))
            throw new ArgumentException("An answer needs a system prompt and a non-empty user prompt.", nameof(request));
        if (request.Temperature < 0 || request.MaxTokens <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Temperature must be >= 0 and MaxTokens > 0.");
    }

    /// <summary>Issue #5510: i dati personali escono dai prompt diretti a OpenRouter; Ollama è locale.</summary>
    private (string SystemPrompt, string UserPrompt) RedactFor(ILlmClient client, string systemPrompt, string userPrompt) =>
        _piiDetector != null && string.Equals(client.ProviderName, "OpenRouter", StringComparison.Ordinal)
            ? (_piiDetector.ScanAndRedact(systemPrompt), _piiDetector.ScanAndRedact(userPrompt))
            : (systemPrompt, userPrompt);

    /// <summary>Il resoconto di un tentativo di streaming, letto dal chiamante dopo l'enumerazione.</summary>
    private sealed class AnswerStreamAttempt(string provider, string model, int promptCharacters)
    {
        public string Provider { get; } = provider;
        public string Model { get; } = model;
        public int PromptCharacters { get; } = promptCharacters;

        /// <summary>Il tentativo ha prodotto una risposta, completa o interrotta: niente riserva.</summary>
        public bool Answered { get; set; }

        /// <summary>Perché il tentativo è fallito prima del primo token.</summary>
        public string? Failure { get; set; }
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
    /// modello di riserva della versione pubblicata del profilo, se la riserva è nel catalogo e costa
    /// meno del modello instradato. Il background a quel punto è già fermo nel gate; le chiamate a
    /// modello esplicito e quelle multimodali tengono il loro modello.
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

        if (!await _budgetGate.IsCheaperReserveAsync(
                decision.ModelId, decision.ProviderName, reserve, reserveClient.ProviderName, cancellationToken).ConfigureAwait(false))
        {
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
