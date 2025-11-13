using System.Collections.Generic;

namespace Api.Services.LlmClients;

/// <summary>
/// Abstraction for LLM provider clients (OpenRouter, Ollama, etc.)
/// ISSUE-958: Hybrid LLM architecture with provider abstraction
/// </summary>
public interface ILlmClient
{
    /// <summary>
    /// Get the name of this LLM provider (e.g., "OpenRouter", "Ollama")
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Generate a chat completion response from this provider
    /// </summary>
    /// <param name="model">Model identifier (e.g., "openai/gpt-4o-mini", "llama3:8b")</param>
    /// <param name="systemPrompt">System-level instructions</param>
    /// <param name="userPrompt">User's input prompt</param>
    /// <param name="temperature">Sampling temperature (0.0-2.0)</param>
    /// <param name="maxTokens">Maximum tokens to generate</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Completion result with response and usage metrics</returns>
    Task<LlmCompletionResult> GenerateCompletionAsync(
        string model,
        string systemPrompt,
        string userPrompt,
        double temperature,
        int maxTokens,
        CancellationToken ct = default);

    /// <summary>
    /// Generate a streaming chat completion response from this provider
    /// </summary>
    /// <param name="model">Model identifier</param>
    /// <param name="systemPrompt">System-level instructions</param>
    /// <param name="userPrompt">User's input prompt</param>
    /// <param name="temperature">Sampling temperature</param>
    /// <param name="maxTokens">Maximum tokens to generate</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Async stream of text chunks</returns>
    IAsyncEnumerable<string> GenerateCompletionStreamAsync(
        string model,
        string systemPrompt,
        string userPrompt,
        double temperature,
        int maxTokens,
        CancellationToken ct = default);

    /// <summary>
    /// Check if this provider supports the specified model
    /// </summary>
    /// <param name="modelId">Model identifier to check</param>
    /// <returns>True if model is supported by this provider</returns>
    bool SupportsModel(string modelId);
}
