using System.Net;
using System.Text;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Tools;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Services;
using Api.Services.LlmClients;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Api.Tests.Services.LlmClients;

/// <summary>ADR-097: Ollama (api/chat) ha gli strumenti, ma gli argomenti arrivano come oggetto e senza id.</summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class OllamaChatTests
{
    [Fact]
    public async Task ChatAsync_SendsToolsAndArgumentsAsObjects_AndReadsToolCalls()
    {
        string? sent = null;
        const string response = """
            {"model":"qwen2.5:3b","message":{"role":"assistant","content":"",
              "tool_calls":[{"function":{"name":"proponi_punteggio","arguments":{"giocatore":"Marco","punti":5}}}]},
             "done":true,"done_reason":"stop"}
            """;
        var client = Create(response, body => sent = body);

        var result = await client.ChatAsync("qwen2.5:3b",
        [
            new LlmChatMessage(LlmChatRoles.User, "Marco fa 5 punti"),
            new LlmChatMessage(LlmChatRoles.Assistant, null, [new LlmToolCall("call_0", "stato_partita", "{}")]),
            new LlmChatMessage(LlmChatRoles.Tool, """{"round":2}""", ToolCallId: "call_0", ToolName: "stato_partita"),
        ],
        [new LlmToolDefinition("proponi_punteggio", "Propone", JsonDocument.Parse("""{"type":"object"}""").RootElement)],
        0.2, 300, TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        var call = result.ToolCalls.Should().ContainSingle().Subject;
        call.Name.Should().Be("proponi_punteggio");
        call.Id.Should().StartWith("call_").And.NotBe("call_0", "un id per indice si ripeterebbe a ogni giro");
        JsonDocument.Parse(call.ArgumentsJson).RootElement.GetProperty("punti").GetInt32().Should().Be(5);

        using var body = JsonDocument.Parse(sent!);
        body.RootElement.GetProperty("tools")[0].GetProperty("function").GetProperty("name").GetString().Should().Be("proponi_punteggio");
        var echoed = body.RootElement.GetProperty("messages")[1].GetProperty("tool_calls")[0].GetProperty("function");
        echoed.GetProperty("arguments").ValueKind.Should().Be(JsonValueKind.Object);
        var toolMessage = body.RootElement.GetProperty("messages")[2];
        toolMessage.GetProperty("role").GetString().Should().Be("tool");
        toolMessage.GetProperty("tool_name").GetString().Should().Be("stato_partita");
    }

    [Fact]
    public async Task ChatAsync_NullableParameterType_IsSentAsASingleTypeString()
    {
        // Ollama 0.5.4 legge `type` come stringa: con l'array che descrive un parametro opzionale risponde 400
        // ("cannot unmarshal array into Go struct field ...properties.type of type string") e l'intero turno fallisce.
        string? sent = null;
        const string response = """{"model":"qwen2.5:3b","message":{"role":"assistant","content":"ok"},"done":true,"done_reason":"stop"}""";
        var client = Create(response, body => sent = body);
        var schema = JsonDocument.Parse("""
            {"type":"object",
             "properties":{
               "giocatore":{"description":"Nome del giocatore","type":"string"},
               "dimensione":{"description":"Dimensione","type":["string","null"],"default":null},
               "round":{"type":["null","integer"],"default":null},
               "etichette":{"type":"array","items":{"type":["string","null"]}}},
             "required":["giocatore"]}
            """).RootElement;

        var result = await client.ChatAsync("qwen2.5:3b",
            [new LlmChatMessage(LlmChatRoles.User, "Marco fa 5 punti")],
            [new LlmToolDefinition("proponi_punteggio", "Propone", schema)],
            0.2, 300, TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        using var body = JsonDocument.Parse(sent!);
        var parameters = body.RootElement.GetProperty("tools")[0].GetProperty("function").GetProperty("parameters");
        var properties = parameters.GetProperty("properties");
        properties.GetProperty("giocatore").GetProperty("type").GetString().Should().Be("string");
        properties.GetProperty("dimensione").GetProperty("type").GetString().Should().Be("string");
        properties.GetProperty("round").GetProperty("type").GetString().Should().Be("integer", "il tipo nullo non è mai quello utile");
        properties.GetProperty("etichette").GetProperty("items").GetProperty("type").GetString().Should().Be("string");
        properties.GetProperty("dimensione").GetProperty("description").GetString().Should().Be("Dimensione");
        parameters.GetProperty("required")[0].GetString().Should().Be("giocatore");
    }

    [Fact]
    public async Task ChatAsync_TheAssistantRealTools_ReachOllamaWithoutTypeArrays()
    {
        // I test di A0 passavano uno schema scritto a mano, a tipo singolo, e non hanno visto il 400 che lo
        // spike ha trovato (#4240): qui gli schemi sono quelli che M.E.AI genera dagli strumenti veri.
        string? sent = null;
        const string response = """{"model":"qwen2.5:3b","message":{"role":"assistant","content":"ok"},"done":true,"done_reason":"stop"}""";
        var client = Create(response, body => sent = body);
        var toolset = new SessionAssistantToolset(
            Mock.Of<ISessionAssistantReader>(), Mock.Of<IAssistantProposalSink>(), Mock.Of<IAssistantPlayerMatcher>());
        var tools = toolset.CreateTools(includeRules: true).OfType<AIFunctionDeclaration>()
            .Select(f => new LlmToolDefinition(f.Name, f.Description, f.JsonSchema))
            .ToList();
        tools.SelectMany(t => TypeArrays(t.ParametersSchema, t.Name)).Should().NotBeEmpty(
            "senza un parametro opzionale negli strumenti questo test non esercita nulla");

        var result = await client.ChatAsync("qwen2.5:3b",
            [new LlmChatMessage(LlmChatRoles.User, "Marco fa 5 punti")], tools, 0.2, 300, TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        using var body = JsonDocument.Parse(sent!);
        var sentTools = body.RootElement.GetProperty("tools");
        sentTools.EnumerateArray().Select(t => t.GetProperty("function").GetProperty("name").GetString()).Should().Equal(
            SessionAssistantToolset.StatoPartita, SessionAssistantToolset.CercaRegole,
            SessionAssistantToolset.ProponiPunteggio, SessionAssistantToolset.ProponiTurno);
        TypeArrays(sentTools, "tools").Should().BeEmpty();
    }

    /// <summary>I percorsi in cui `type` è un array, ovunque nello schema.</summary>
    private static IEnumerable<string> TypeArrays(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var (item, index) in element.EnumerateArray().Select((item, index) => (item, index)))
            {
                foreach (var found in TypeArrays(item, $"{path}[{index}]"))
                {
                    yield return found;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals("type") && property.Value.ValueKind == JsonValueKind.Array)
                {
                    yield return $"{path}.type";
                }

                foreach (var found in TypeArrays(property.Value, $"{path}.{property.Name}"))
                {
                    yield return found;
                }
            }
        }
    }

    private static OllamaLlmClient Create(string responseJson, Action<string> onSend)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage request, CancellationToken ct) =>
            {
                onSend(await request.Content!.ReadAsStringAsync(ct));
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(responseJson, Encoding.UTF8, "application/json") };
            });
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler.Object));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["OLLAMA_URL"] = "http://ollama:11434" }).Build();
        var calculator = new Mock<ILlmCostCalculator>();
        calculator.Setup(c => c.CalculateCost(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns((string model, string p, int prompt, int completion) => new LlmCostCalculation
            {
                ModelId = model, Provider = p, PromptTokens = prompt, CompletionTokens = completion, InputCost = 0, OutputCost = 0,
            });
        return new OllamaLlmClient(factory.Object, config, calculator.Object, Mock.Of<ILogger<OllamaLlmClient>>());
    }
}
