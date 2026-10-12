using System.Net;
using System.Text;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Services;
using Api.Services.LlmClients;
using Api.Tests.Constants;
using FluentAssertions;
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
