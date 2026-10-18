using System.Runtime.CompilerServices;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;
using Api.Services;
using Microsoft.Extensions.AI;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;

/// <summary>
/// ADR-097: <see cref="IChatClient"/> sopra il gateway. Traduce i messaggi M.E.AI in
/// <see cref="LlmChatRequest"/> e la risposta del gateway in <see cref="ChatResponse"/>. Ogni chiamata
/// è un giro: passa da tetto, degrado, fallback, circuit breaker e costo dentro
/// <see cref="ILlmService.ChatAsync"/>. Non conosce client né HTTP.
/// </summary>
internal sealed class GatewayChatClient : IChatClient
{
    private static readonly JsonSerializerOptions ResultJson = new(JsonSerializerDefaults.Web);

    private readonly ILlmService _gateway;
    private readonly AssistantModelSettings _settings;
    private readonly ChatTurnMeter _meter;

    public GatewayChatClient(ILlmService gateway, AssistantModelSettings settings, ChatTurnMeter meter)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _meter = meter ?? throw new ArgumentNullException(nameof(meter));
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var request = new LlmChatRequest(
            ToGatewayMessages(messages.ToList()),
            ToToolDefinitions(options?.Tools),
            _settings.PrimaryModelId,
            _settings.FallbackModelId,
            options?.Temperature ?? _settings.Temperature,
            options?.MaxOutputTokens ?? _settings.MaxTokens,
            _settings.ProfileVersionId,
            _settings.Source);

        var result = await _gateway.ChatAsync(request, cancellationToken).ConfigureAwait(false);
        _meter.Record(result);
        if (!result.Success)
        {
            throw new AssistantGatewayException(
                result.Metadata.TryGetValue("error_code", out var code) ? code : LlmAnswerErrorCodes.ProviderUnavailable);
        }

        var contents = new List<AIContent>();
        if (!string.IsNullOrEmpty(result.Response))
        {
            contents.Add(new TextContent(result.Response));
        }

        contents.AddRange(result.ToolCalls.Select(c => new FunctionCallContent(c.Id, c.Name, ParseArguments(c.ArgumentsJson))));

        return new ChatResponse(new ChatMessage(ChatRole.Assistant, contents))
        {
            ModelId = result.Cost.ModelId,
            FinishReason = result.ToolCalls.Count > 0 ? ChatFinishReason.ToolCalls : ChatFinishReason.Stop,
            Usage = new UsageDetails
            {
                InputTokenCount = result.Usage.PromptTokens,
                OutputTokenCount = result.Usage.CompletionTokens,
                TotalTokenCount = result.Usage.TotalTokens,
            },
        };
    }

    /// <summary>Niente streaming nel gateway con strumenti: l'assistente bufferizza (spec §6.1).</summary>
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
        foreach (var update in response.ToChatResponseUpdates())
        {
            yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose()
    {
        // Nessuna risorsa: il gateway è un servizio del container.
    }

    private static List<LlmChatMessage> ToGatewayMessages(IReadOnlyList<ChatMessage> history)
    {
        // La mappa id → nome si aggiorna mentre si scorre la storia: un provider che non dà id (Ollama)
        // ripete call_0 a ogni giro, e ogni risultato deve prendere il nome della chiamata del suo giro.
        var toolNames = new Dictionary<string, string>(StringComparer.Ordinal);
        var result = new List<LlmChatMessage>();
        foreach (var message in history)
        {
            foreach (var call in message.Contents.OfType<FunctionCallContent>())
            {
                toolNames[call.CallId] = call.Name;
            }

            if (message.Role == ChatRole.Tool)
            {
                result.AddRange(message.Contents.OfType<FunctionResultContent>().Select(r => new LlmChatMessage(
                    LlmChatRoles.Tool, SerializeResult(r.Result), ToolCallId: r.CallId, ToolName: toolNames.GetValueOrDefault(r.CallId))));
                continue;
            }

            var text = string.Concat(message.Contents.OfType<TextContent>().Select(t => t.Text));
            var calls = message.Contents.OfType<FunctionCallContent>()
                .Select(c => new LlmToolCall(c.CallId, c.Name,
                    JsonSerializer.Serialize(c.Arguments ?? new Dictionary<string, object?>(StringComparer.Ordinal))))
                .ToList();
            result.Add(new LlmChatMessage(
                RoleOf(message.Role),
                calls.Count > 0 && text.Length == 0 ? null : text,
                calls.Count > 0 ? calls : null));
        }

        return result;
    }

    private static string RoleOf(ChatRole role) =>
        role == ChatRole.System ? LlmChatRoles.System
        : role == ChatRole.Assistant ? LlmChatRoles.Assistant
        : LlmChatRoles.User;

    private static List<LlmToolDefinition> ToToolDefinitions(IList<AITool>? tools) =>
        tools?.OfType<AIFunctionDeclaration>()
            .Select(f => new LlmToolDefinition(f.Name, f.Description, f.JsonSchema))
            .ToList() ?? [];

    private static string SerializeResult(object? result) => result switch
    {
        null => "null",
        string text => text,
        JsonElement element => element.GetRawText(),
        _ => JsonSerializer.Serialize(result, ResultJson),
    };

    /// <summary>Argomenti non validi (capita con i modelli piccoli): lo strumento li riceve vuoti e risponde con un errore.</summary>
    private static Dictionary<string, object?> ParseArguments(string json)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, object?>>(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            return parsed is null
                ? new Dictionary<string, object?>(StringComparer.Ordinal)
                : new Dictionary<string, object?>(parsed, StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal);
        }
    }
}
