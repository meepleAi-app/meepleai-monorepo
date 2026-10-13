using System.Text.Json;

#pragma warning disable MA0048 // File name must match type name - the chat contract of the gateway lives together
namespace Api.Services;

/// <summary>ADR-097: i ruoli dei messaggi di una chat con strumenti, nel formato dei provider OpenAI-compatibili.</summary>
internal static class LlmChatRoles
{
    public const string System = "system";
    public const string User = "user";
    public const string Assistant = "assistant";
    public const string Tool = "tool";
}

/// <summary>Una chiamata a uno strumento chiesta dal modello. Gli argomenti restano JSON, come li ha scritti il modello.</summary>
internal sealed record LlmToolCall(string Id, string Name, string ArgumentsJson);

/// <summary>
/// Un messaggio della chat. Un messaggio dell'assistente può portare chiamate a strumenti invece del
/// testo; un messaggio con ruolo <see cref="LlmChatRoles.Tool"/> porta il risultato di una chiamata.
/// </summary>
internal sealed record LlmChatMessage(
    string Role,
    string? Content,
    IReadOnlyList<LlmToolCall>? ToolCalls = null,
    string? ToolCallId = null,
    string? ToolName = null);

/// <summary>Uno strumento offerto al modello: nome, descrizione e schema JSON dei parametri.</summary>
internal sealed record LlmToolDefinition(string Name, string Description, JsonElement ParametersSchema);

/// <summary>
/// ADR-097: una chiamata di chat con strumenti. Come <see cref="LlmAnswerRequest"/>, modello, riserva,
/// temperatura e lunghezza li decide il chiamante; il gateway applica tetto, degrado, fallback,
/// circuit breaker, redazione e costo a ogni chiamata.
/// </summary>
internal sealed record LlmChatRequest(
    IReadOnlyList<LlmChatMessage> Messages,
    IReadOnlyList<LlmToolDefinition> Tools,
    string PrimaryModelId,
    string? FallbackModelId,
    double Temperature,
    int MaxTokens,
    Guid? ProfileVersionId,
    RequestSource Source);
