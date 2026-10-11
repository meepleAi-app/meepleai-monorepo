using System.Collections.Generic;
using Api.Services.LlmClients;

#pragma warning disable MA0048 // File name must match type name - Contains Service with Configuration classes
namespace Api.Services;

/// <summary>
/// ISSUE-1725: Streaming chunk with optional usage metadata
/// Content: Text chunk from LLM (null in final chunk with usage only)
/// Usage: Token usage metadata (only in final chunk)
/// Cost: Cost metadata (only in final chunk)
/// IsFinal: True if this is the last chunk containing usage metadata
/// ErrorCode / ErrorMessage: #4166, set on the only chunk of a call the gateway refused before
/// reaching the provider (budget cap, quota, model not in the catalog)
/// </summary>
internal record StreamChunk(
    string? Content,
    LlmUsage? Usage = null,
    LlmCost? Cost = null,
    bool IsFinal = false,
    string? FinishReason = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

/// <summary>
/// Interface for LLM chat completion services
/// </summary>
internal interface ILlmService
{
    /// <summary>
    /// Generate a chat completion response
    /// </summary>
    Task<LlmCompletionResult> GenerateCompletionAsync(
        string systemPrompt,
        string userPrompt,
        RequestSource source = RequestSource.Manual,
        CancellationToken ct = default);

    /// <summary>
    /// CHAT-01: Generate a streaming chat completion response with token-by-token delivery
    /// ISSUE-1725: Enhanced to return StreamChunk with usage metadata in final chunk
    /// </summary>
    IAsyncEnumerable<StreamChunk> GenerateCompletionStreamAsync(
        string systemPrompt,
        string userPrompt,
        RequestSource source = RequestSource.Manual,
        CancellationToken ct = default);

    /// <summary>
    /// CHAT-02: Generate a JSON-structured response from the LLM, deserializing to the specified type.
    /// </summary>
    /// <typeparam name="T">The target type for deserialization (must be a class)</typeparam>
    /// <param name="systemPrompt">System-level instructions for the LLM</param>
    /// <param name="userPrompt">User's input prompt</param>
    /// <param name="source">Request source for cost tracking</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Deserialized object of type T, or null if parsing fails</returns>
    /// <remarks>
    /// This method automatically appends JSON schema instructions to the system prompt
    /// and handles deserialization with graceful error handling.
    /// </remarks>
    Task<T?> GenerateJsonAsync<T>(
        string systemPrompt,
        string userPrompt,
        RequestSource source = RequestSource.Manual,
        CancellationToken ct = default) where T : class;

    /// <summary>
    /// Generate a multimodal (text + images) completion response.
    /// Falls back to text-only if no vision-capable provider is available.
    /// </summary>
    Task<LlmCompletionResult> GenerateMultimodalCompletionAsync(
        IReadOnlyList<LlmMessage> messages,
        RequestSource source = RequestSource.Manual,
        CancellationToken ct = default);

    /// <summary>
    /// Generate a streaming multimodal (text + images) completion response.
    /// Falls back to text-only if no vision-capable provider is available.
    /// </summary>
    IAsyncEnumerable<StreamChunk> GenerateMultimodalCompletionStreamAsync(
        IReadOnlyList<LlmMessage> messages,
        RequestSource source = RequestSource.Manual,
        CancellationToken ct = default);

    /// <summary>
    /// Issue #4332: Generate completion with explicit model (bypassing routing strategy).
    /// Used for multi-model ensemble evaluation requiring specific models.
    /// </summary>
    /// <param name="explicitModel">Model ID with provider prefix (e.g., "openai/gpt-4", "anthropic/claude-3.5-sonnet")</param>
    /// <param name="systemPrompt">System-level instructions for the LLM</param>
    /// <param name="userPrompt">User's input prompt</param>
    /// <param name="source">Request source for cost tracking</param>
    /// <param name="ct">Cancellation token</param>
    /// <param name="maxTokens">Optional override for the provider's max_tokens cap. When null, the service default is used.</param>
    /// <returns>LLM completion result with usage and cost tracking</returns>
    Task<LlmCompletionResult> GenerateCompletionWithModelAsync(
        string explicitModel,
        string systemPrompt,
        string userPrompt,
        RequestSource source = RequestSource.Manual,
        int? maxTokens = null,
        CancellationToken ct = default);

    /// <summary>
    /// Issue #2961: Generate a completion with an explicit PREFERRED model and, on a hard
    /// failure of that provider (e.g. a 402 credit-exhausted), automatically fall back to the
    /// next available provider's default model via the DB-driven fallback chain, until one
    /// succeeds or the chain is exhausted. Used by the Mechanic Extractor pipeline so a single
    /// provider outage does not abort the whole analysis.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="GenerateCompletionWithModelAsync"/> (single-shot, #4332 ensemble), this
    /// retries across providers. The default interface implementation delegates to the single-shot
    /// method (no fallback), preserving behaviour for mocks/fakes that do not override it;
    /// <c>HybridLlmService</c> overrides it with the real multi-provider fallback loop.
    /// </remarks>
    /// <param name="preferredModel">Model ID tried first; selects the preferred provider via <c>SupportsModel</c>.</param>
    /// <param name="systemPrompt">System-level instructions for the LLM.</param>
    /// <param name="userPrompt">User's input prompt.</param>
    /// <param name="source">Request source for cost tracking.</param>
    /// <param name="maxTokens">Optional override for the provider's max_tokens cap.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<LlmCompletionResult> GenerateCompletionWithModelFallbackAsync(
        string preferredModel,
        string systemPrompt,
        string userPrompt,
        RequestSource source = RequestSource.Manual,
        int? maxTokens = null,
        CancellationToken ct = default)
        => GenerateCompletionWithModelAsync(preferredModel, systemPrompt, userPrompt, source, maxTokens, ct);

    /// <summary>
    /// #4166: streaming con un modello esplicito e parametri propri, attraverso il gateway (tetto di
    /// spesa, registro costi). Serve al playground admin, che prova il modello e i parametri di un
    /// agente e prima chiamava i client direttamente.
    /// </summary>
    /// <remarks>
    /// L'implementazione di default ignora modello e parametri e delega allo streaming instradato,
    /// come <see cref="GenerateCompletionWithModelFallbackAsync"/> per mock e doppi;
    /// <c>HybridLlmService</c> la sostituisce.
    /// </remarks>
    IAsyncEnumerable<StreamChunk> GenerateCompletionStreamWithModelAsync(
        string explicitModel,
        string systemPrompt,
        string userPrompt,
        RequestSource source,
        double? temperature = null,
        int? maxTokens = null,
        CancellationToken ct = default)
        => GenerateCompletionStreamAsync(systemPrompt, userPrompt, source, ct);

    /// <summary>
    /// #4168 (ADR-095 D3, passo 6): la risposta della pipeline in streaming. Modello, riserva,
    /// temperatura e lunghezza arrivano dalla versione pubblicata del profilo, non dal gateway.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Le garanzie (D3): fallback sulla riserva solo PRIMA del primo token, circuit breaker
    /// aggiornato a ogni tentativo. Se lo stream si interrompe dopo il primo token non si passa a un
    /// secondo modello: l'ultimo chunk porta <see cref="LlmAnswerErrorCodes.StreamInterrupted"/>, il
    /// costo dei token generati finisce nel registro e il circuit breaker conta il fallimento. Se
    /// nessun modello risponde, l'unico chunk porta <see cref="LlmAnswerErrorCodes.ProviderUnavailable"/>:
    /// mai uno stream vuoto.
    /// </para>
    /// <para>
    /// L'implementazione di default serve a mock e doppi: chiama il modello principale senza
    /// fallback. <c>HybridLlmService</c> la sostituisce.
    /// </para>
    /// </remarks>
    IAsyncEnumerable<StreamChunk> GenerateAnswerStreamAsync(LlmAnswerRequest request, CancellationToken ct = default)
        => GenerateCompletionStreamWithModelAsync(
            request.PrimaryModelId, request.SystemPrompt, request.UserPrompt, request.Source,
            request.Temperature, request.MaxTokens, ct);

    /// <summary>
    /// #4168: la risposta della pipeline in un colpo solo, con le stesse garanzie dello streaming
    /// (riserva se il principale fallisce, circuit breaker aggiornato). Il risultato porta il
    /// <see cref="LlmCompletionResult.ProfileVersionId"/> della richiesta.
    /// </summary>
    /// <remarks>L'implementazione di default serve a mock e doppi: chiama il modello principale senza fallback.</remarks>
    Task<LlmCompletionResult> GenerateAnswerAsync(LlmAnswerRequest request, CancellationToken ct = default)
        => GenerateCompletionWithModelAsync(
            request.PrimaryModelId, request.SystemPrompt, request.UserPrompt, request.Source, request.MaxTokens, ct);
}

/// <summary>
/// #4168 (ADR-095 D3): una risposta della pipeline. Il chiamante decide modello e parametri,
/// presi dalla versione pubblicata del profilo; il gateway resta il solo a chiamare il provider,
/// con tetto, fallback, circuit breaker e registro.
/// </summary>
/// <param name="SystemPrompt">Il prompt di sistema, già nella lingua dell'utente.</param>
/// <param name="UserPrompt">La domanda con il contesto recuperato.</param>
/// <param name="PrimaryModelId">Il modello principale della versione pubblicata, dal catalogo.</param>
/// <param name="FallbackModelId">La riserva: si usa se il principale non risponde prima del primo
/// token, e al posto del principale quando il tetto è in degrado e la riserva costa meno.</param>
/// <param name="Temperature">La temperatura della versione pubblicata.</param>
/// <param name="MaxTokens">La lunghezza massima della risposta, in token.</param>
/// <param name="ProfileVersionId">La versione del profilo usata davvero: finisce nella riga del registro costi (D2).</param>
/// <param name="Source">La sorgente per il tetto e il registro: una risposta, salvo eccezioni.</param>
internal sealed record LlmAnswerRequest(
    string SystemPrompt,
    string UserPrompt,
    string PrimaryModelId,
    string? FallbackModelId,
    double Temperature,
    int MaxTokens,
    Guid? ProfileVersionId,
    RequestSource Source = RequestSource.RagPipeline);

/// <summary>#4168: i codici d'errore che il gateway mette sull'ultimo chunk di una risposta.</summary>
internal static class LlmAnswerErrorCodes
{
    /// <summary>Nessun modello ha risposto prima del primo token: provider giù, circuito aperto, chiave rifiutata.</summary>
    public const string ProviderUnavailable = "AI_PROVIDER_UNAVAILABLE";

    /// <summary>Lo stream si è interrotto dopo il primo token: la risposta è parziale e non va mostrata come completa.</summary>
    public const string StreamInterrupted = "AI_STREAM_INTERRUPTED";
}

/// <summary>
/// Token usage information from LLM completion
/// </summary>
internal record LlmUsage(int PromptTokens, int CompletionTokens, int TotalTokens)
{
    public static readonly LlmUsage Empty = new(0, 0, 0);
}

/// <summary>
/// Cost information for LLM completion
/// ISSUE-960: BGAI-018 - Financial cost tracking
/// </summary>
internal record LlmCost
{
    /// <summary>
    /// Cost for input/prompt tokens in USD
    /// </summary>
    public required decimal InputCost { get; init; }

    /// <summary>
    /// Cost for output/completion tokens in USD
    /// </summary>
    public required decimal OutputCost { get; init; }

    /// <summary>
    /// Total cost in USD (input + output)
    /// </summary>
    public decimal TotalCost => InputCost + OutputCost;

    /// <summary>
    /// Model identifier used for this request
    /// </summary>
    public required string ModelId { get; init; }

    /// <summary>
    /// Provider name (OpenRouter, Ollama)
    /// </summary>
    public required string Provider { get; init; }

    /// <summary>
    /// #4164 (ADR-095): come è stato prezzato il costo; <c>null</c> se non c'era nulla da prezzare.
    /// </summary>
    public Api.BoundedContexts.KnowledgeBase.Domain.Models.LlmPricingStatus? PricingStatus { get; init; }

    /// <summary>
    /// Empty cost (zero cost)
    /// </summary>
    public static readonly LlmCost Empty = new()
    {
        InputCost = 0,
        OutputCost = 0,
        ModelId = "unknown",
        Provider = "Unknown"
    };
}

/// <summary>
/// Result of LLM completion with token usage and cost information
/// </summary>
internal record LlmCompletionResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string Response { get; init; } = string.Empty;
    public LlmUsage Usage { get; init; } = LlmUsage.Empty;
    public LlmCost Cost { get; init; } = LlmCost.Empty;
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// #4168 (ADR-095 D2): la versione del profilo dell'agente con cui è stata prodotta una risposta
    /// della pipeline; <c>null</c> per ogni altra chiamata. Il registro costi la copia nella riga.
    /// </summary>
    public Guid? ProfileVersionId { get; init; }

    public static LlmCompletionResult CreateSuccess(
        string response,
        LlmUsage? usage = null,
        LlmCost? cost = null,
        IReadOnlyDictionary<string, string>? metadata = null) =>
        new()
        {
            Success = true,
            Response = response,
            Usage = usage ?? LlmUsage.Empty,
            Cost = cost ?? LlmCost.Empty,
            Metadata = metadata ?? new Dictionary<string, string>(StringComparer.Ordinal)
        };

    public static LlmCompletionResult CreateFailure(string error) =>
        new()
        {
            Success = false,
            ErrorMessage = error,
            Usage = LlmUsage.Empty,
            Cost = LlmCost.Empty,
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        };
}
