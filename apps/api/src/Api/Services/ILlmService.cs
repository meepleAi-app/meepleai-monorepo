using System.Collections.Generic;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

#pragma warning disable MA0048 // File name must match type name - Contains Service with Configuration classes
namespace Api.Services;

/// <summary>
/// ISSUE-1725: Streaming chunk with optional usage metadata
/// Content: Text chunk from LLM (null in final chunk with usage only)
/// Usage: Token usage metadata (only in final chunk)
/// Cost: Cost metadata (only in final chunk)
/// IsFinal: True if this is the last chunk containing usage metadata
/// </summary>
internal record StreamChunk(
    string? Content,
    LlmUsage? Usage = null,
    LlmCost? Cost = null,
    bool IsFinal = false);

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
    /// Generate a chat completion response with explicit user context for tier-aware routing.
    /// </summary>
    Task<LlmCompletionResult> GenerateCompletionAsync(
        string systemPrompt,
        string userPrompt,
        LlmUserContext userContext,
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
    /// Issue #4332: Generate completion with explicit model (bypassing routing strategy).
    /// Used for multi-model ensemble evaluation requiring specific models.
    /// </summary>
    /// <param name="explicitModel">Model ID with provider prefix (e.g., "openai/gpt-4", "anthropic/claude-3.5-sonnet")</param>
    /// <param name="systemPrompt">System-level instructions for the LLM</param>
    /// <param name="userPrompt">User's input prompt</param>
    /// <param name="source">Request source for cost tracking</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>LLM completion result with usage and cost tracking</returns>
    Task<LlmCompletionResult> GenerateCompletionWithModelAsync(
        string explicitModel,
        string systemPrompt,
        string userPrompt,
        RequestSource source = RequestSource.Manual,
        CancellationToken ct = default);
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
