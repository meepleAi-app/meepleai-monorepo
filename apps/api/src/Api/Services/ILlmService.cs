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
}

/// <summary>
/// Result of LLM completion
/// </summary>
public record LlmCompletionResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string Response { get; init; } = string.Empty;

    public static LlmCompletionResult CreateSuccess(string response) =>
        new() { Success = true, Response = response };

    public static LlmCompletionResult CreateFailure(string error) =>
        new() { Success = false, ErrorMessage = error };
}
