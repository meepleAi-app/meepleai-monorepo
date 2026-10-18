using System.Text;
using System.Text.Json;

namespace Api.Services.LlmClients;

/// <summary>
/// ADR-097: la chat con strumenti nel formato OpenAI, condivisa da OpenRouter e DeepSeek. Costruisce
/// la richiesta e legge le chiamate a strumenti; l'invio, le credenziali e gli errori restano ai client.
/// </summary>
internal static class OpenAiToolPayload
{
    public static HttpRequestMessage CreateRequest(
        string model,
        IReadOnlyList<LlmChatMessage> messages,
        IReadOnlyList<LlmToolDefinition> tools,
        double temperature,
        int maxTokens,
        bool disableThinking = false)
    {
        var payload = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["model"] = model,
            ["messages"] = messages.Select(Message).ToList(),
            ["temperature"] = temperature,
            ["max_tokens"] = maxTokens,
            ["stream"] = false,
        };

        // DeepSeek: con «tools» e thinking attivo (il default) reasoning_content deve tornare a ogni turno,
        // altrimenti il secondo giro riceve 400. Spento, la chat con strumenti resta a un solo formato e
        // la temperatura torna ad avere effetto (api-docs.deepseek.com/guides/thinking_mode).
        if (disableThinking)
        {
            DisableThinking(payload);
        }

        if (tools.Count > 0)
        {
            payload["tools"] = tools.Select(t => (object)new
            {
                type = "function",
                function = new { name = t.Name, description = t.Description, parameters = t.ParametersSchema },
            }).ToList();
        }

        return new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };
    }

    /// <summary>
    /// Spegne il ragionamento di DeepSeek in un corpo di richiesta (api-docs.deepseek.com/guides/thinking_mode).
    /// Una sola forma per la chat con strumenti e per le risposte (#4381): se DeepSeek cambia il parametro,
    /// cambia qui.
    /// </summary>
    public static void DisableThinking(IDictionary<string, object> payload) =>
        payload["thinking"] = new { type = "disabled" };

    public static IReadOnlyList<LlmToolCall> ReadToolCalls(OpenAiChatMessage? message)
    {
        if (message?.ToolCalls is not { Count: > 0 } calls)
        {
            return [];
        }

        return calls
            .Where(c => c.Function is not null && !string.IsNullOrWhiteSpace(c.Function.Name))
            .Select(c => new LlmToolCall(
                string.IsNullOrWhiteSpace(c.Id) ? $"call_{Guid.NewGuid():N}" : c.Id,
                c.Function!.Name,
                string.IsNullOrWhiteSpace(c.Function.Arguments) ? "{}" : c.Function.Arguments))
            .ToList();
    }

    private static Dictionary<string, object?> Message(LlmChatMessage message)
    {
        if (string.Equals(message.Role, LlmChatRoles.Tool, StringComparison.Ordinal))
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["role"] = LlmChatRoles.Tool,
                ["tool_call_id"] = message.ToolCallId,
                ["content"] = message.Content ?? string.Empty,
            };
        }

        if (message.ToolCalls is { Count: > 0 } calls)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["role"] = LlmChatRoles.Assistant,
                ["content"] = string.IsNullOrEmpty(message.Content) ? null : message.Content,
                ["tool_calls"] = calls.Select(c => new
                {
                    id = c.Id,
                    type = "function",
                    function = new { name = c.Name, arguments = c.ArgumentsJson },
                }).ToList(),
            };
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["role"] = message.Role,
            ["content"] = message.Content ?? string.Empty,
        };
    }
}
