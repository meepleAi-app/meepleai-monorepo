using System.Text.Json.Serialization;

namespace Api.Services.LlmClients;

/// <summary>
/// Shared OpenAI-compatible chat API response models.
/// Used by providers that expose an OpenAI-compatible endpoint (OpenRouter, DeepSeek, etc.)
/// </summary>
internal record OpenAiChatResponse
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("choices")]
    public List<OpenAiChatChoice> Choices { get; init; } = new();

    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    [JsonPropertyName("usage")]
    public OpenAiChatUsage? Usage { get; init; }
}

internal record OpenAiChatChoice
{
    [JsonPropertyName("message")]
    public OpenAiChatMessage? Message { get; init; }

    [JsonPropertyName("finish_reason")]
    public string FinishReason { get; init; } = string.Empty;
}

internal record OpenAiChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;
}

internal record OpenAiChatUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; init; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; init; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; init; }
}

// Streaming response models
internal record OpenAiStreamChunk
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("choices")]
    public List<OpenAiStreamChoice>? Choices { get; init; }

    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    /// <summary>
    /// Usage metadata in final SSE chunk (OpenRouter: when usage.include=true; DeepSeek: included by default)
    /// </summary>
    [JsonPropertyName("usage")]
    public OpenAiChatUsage? Usage { get; init; }
}

internal record OpenAiStreamChoice
{
    [JsonPropertyName("delta")]
    public OpenAiStreamDelta? Delta { get; init; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; init; }

    [JsonPropertyName("index")]
    public int Index { get; init; }
}

internal record OpenAiStreamDelta
{
    [JsonPropertyName("role")]
    public string? Role { get; init; }

    [JsonPropertyName("content")]
    public string? Content { get; init; }
}
