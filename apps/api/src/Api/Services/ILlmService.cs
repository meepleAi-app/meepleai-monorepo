using System.Collections.Generic;

namespace Api.Services;

/// <summary>
/// Interface for LLM chat completion services
/// </summary>
public interface ILlmService
{
    /// <summary>
    /// Generate a chat completion response
    /// </summary>
    Task<LlmCompletionResult> GenerateCompletionAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken ct = default);

    /// <summary>
    /// CHAT-01: Generate a streaming chat completion response with token-by-token delivery
    /// </summary>
    IAsyncEnumerable<string> GenerateCompletionStreamAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken ct = default);

    /// <summary>
    /// CHAT-02: Generate a JSON-structured response from the LLM, deserializing to the specified type.
    /// </summary>
    /// <typeparam name="T">The target type for deserialization (must be a class)</typeparam>
    /// <param name="systemPrompt">System-level instructions for the LLM</param>
    /// <param name="userPrompt">User's input prompt</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Deserialized object of type T, or null if parsing fails</returns>
    /// <remarks>
    /// This method automatically appends JSON schema instructions to the system prompt
    /// and handles deserialization with graceful error handling.
    /// </remarks>
    Task<T?> GenerateJsonAsync<T>(
        string systemPrompt,
        string userPrompt,
        CancellationToken ct = default) where T : class;
}

/// <summary>
/// Result of LLM completion
/// </summary>
public record LlmUsage(int PromptTokens, int CompletionTokens, int TotalTokens)
{
    public static readonly LlmUsage Empty = new(0, 0, 0);
}

public record LlmCompletionResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string Response { get; init; } = string.Empty;
    public LlmUsage Usage { get; init; } = LlmUsage.Empty;
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();

    public static LlmCompletionResult CreateSuccess(
        string response,
        LlmUsage? usage = null,
        IReadOnlyDictionary<string, string>? metadata = null) =>
        new()
        {
            Success = true,
            Response = response,
            Usage = usage ?? LlmUsage.Empty,
            Metadata = metadata ?? new Dictionary<string, string>()
        };

    public static LlmCompletionResult CreateFailure(string error) =>
        new() { Success = false, ErrorMessage = error, Usage = LlmUsage.Empty, Metadata = new Dictionary<string, string>() };
}
