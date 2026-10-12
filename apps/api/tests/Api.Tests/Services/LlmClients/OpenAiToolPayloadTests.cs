using System.Text.Json;
using Api.Services;
using Api.Services.LlmClients;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Services.LlmClients;

/// <summary>ADR-097: la chat con strumenti nel formato OpenAI, condiviso da OpenRouter e DeepSeek.</summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class OpenAiToolPayloadTests
{
    private static readonly JsonElement EmptySchema =
        JsonDocument.Parse("""{"type":"object","properties":{"giocatore":{"type":"string"}},"required":["giocatore"]}""").RootElement;

    [Fact]
    public async Task CreateRequest_WritesMessagesToolsAndToolResults()
    {
        var messages = new List<LlmChatMessage>
        {
            new(LlmChatRoles.System, "Sei l'assistente."),
            new(LlmChatRoles.User, "Marco fa 5 punti"),
            new(LlmChatRoles.Assistant, null, [new LlmToolCall("call_1", "proponi_punteggio", """{"giocatore":"Marco","punti":5}""")]),
            new(LlmChatRoles.Tool, """{"proposalId":"x"}""", ToolCallId: "call_1", ToolName: "proponi_punteggio"),
        };

        using var request = OpenAiToolPayload.CreateRequest("deepseek-flash", messages,
            [new LlmToolDefinition("proponi_punteggio", "Propone un punteggio", EmptySchema)], 0.2, 600);
        using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var root = body.RootElement;

        request.RequestUri!.ToString().Should().Be("chat/completions");
        root.GetProperty("model").GetString().Should().Be("deepseek-flash");
        root.GetProperty("stream").GetBoolean().Should().BeFalse();
        root.GetProperty("max_tokens").GetInt32().Should().Be(600);
        var tool = root.GetProperty("tools")[0];
        tool.GetProperty("type").GetString().Should().Be("function");
        tool.GetProperty("function").GetProperty("name").GetString().Should().Be("proponi_punteggio");
        tool.GetProperty("function").GetProperty("parameters").GetProperty("required")[0].GetString().Should().Be("giocatore");
        var assistant = root.GetProperty("messages")[2];
        assistant.GetProperty("role").GetString().Should().Be("assistant");
        assistant.GetProperty("content").ValueKind.Should().Be(JsonValueKind.Null);
        var call = assistant.GetProperty("tool_calls")[0];
        call.GetProperty("id").GetString().Should().Be("call_1");
        call.GetProperty("function").GetProperty("arguments").GetString().Should().Be("""{"giocatore":"Marco","punti":5}""");
        var toolResult = root.GetProperty("messages")[3];
        toolResult.GetProperty("role").GetString().Should().Be("tool");
        toolResult.GetProperty("tool_call_id").GetString().Should().Be("call_1");
    }

    [Fact]
    public async Task CreateRequest_WithoutTools_OmitsTheToolsField()
    {
        using var request = OpenAiToolPayload.CreateRequest("deepseek-flash",
            [new LlmChatMessage(LlmChatRoles.User, "ciao")], [], 0.2, 100);
        using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken));

        body.RootElement.TryGetProperty("tools", out _).Should().BeFalse();
    }

    [Fact]
    public void ReadToolCalls_MapsIdNameAndArguments_AndFillsMissingIds()
    {
        var message = JsonSerializer.Deserialize<OpenAiChatMessage>("""
            {"role":"assistant","content":null,"tool_calls":[
              {"id":"call_9","type":"function","function":{"name":"stato_partita","arguments":"{}"}},
              {"id":"","type":"function","function":{"name":"proponi_turno","arguments":""}}]}
            """);

        var calls = OpenAiToolPayload.ReadToolCalls(message);

        calls.Should().HaveCount(2);
        calls[0].Should().Be(new LlmToolCall("call_9", "stato_partita", "{}"));
        calls[1].Id.Should().Be("call_1");
        calls[1].ArgumentsJson.Should().Be("{}");
    }
}
