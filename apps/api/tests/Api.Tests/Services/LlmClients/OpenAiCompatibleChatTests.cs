using System.Net;
using System.Text;
using System.Text.Json;
using Api.BoundedContexts.Administration.Infrastructure.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Services;
using Api.Services.LlmClients;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Api.Tests.Services.LlmClients;

/// <summary>ADR-097: OpenRouter e DeepSeek parlano lo stesso formato OpenAI per gli strumenti.</summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class OpenAiCompatibleChatTests
{
    private const string ToolCallsResponse = """
        {"id":"gen-1","model":"m","choices":[{"message":{"role":"assistant","content":null,
          "tool_calls":[{"id":"call_1","type":"function","function":{"name":"proponi_punteggio","arguments":"{\"giocatore\":\"Marco\",\"punti\":5}"}}]},
          "finish_reason":"tool_calls"}],
         "usage":{"prompt_tokens":120,"completion_tokens":18,"total_tokens":138}}
        """;

    public static TheoryData<string> Providers => new() { "OpenRouter", "DeepSeek" };

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task ChatAsync_ToolCallsWithNullContent_ReturnsSuccessWithCalls(string provider)
    {
        string? sentBody = null;
        var client = Create(provider, ToolCallsResponse, body => sentBody = body);

        var result = await client.ChatAsync("model-x",
            [new LlmChatMessage(LlmChatRoles.User, "Marco fa 5 punti")],
            [new LlmToolDefinition("proponi_punteggio", "Propone", JsonDocument.Parse("""{"type":"object"}""").RootElement)],
            0.2, 600, TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        result.Response.Should().BeEmpty();
        result.ToolCalls.Should().ContainSingle()
            .Which.Should().Be(new LlmToolCall("call_1", "proponi_punteggio", """{"giocatore":"Marco","punti":5}"""));
        result.Usage.PromptTokens.Should().Be(120);
        sentBody.Should().Contain("\"tools\"");
    }

    /// <summary>
    /// DeepSeek ha il thinking attivo di default e, con «tools», esige che reasoning_content torni a ogni
    /// turno, altrimenti risponde 400 dal secondo giro (api-docs.deepseek.com/guides/thinking_mode).
    /// </summary>
    [Theory]
    [InlineData("DeepSeek", true)]
    [InlineData("OpenRouter", false)]
    public async Task ChatAsync_DisablesThinkingOnlyForDeepSeek(string provider, bool expected)
    {
        string? sentBody = null;
        var client = Create(provider, ToolCallsResponse, body => sentBody = body);

        await client.ChatAsync("model-x", [new LlmChatMessage(LlmChatRoles.User, "x")], [], 0.2, 100,
            TestContext.Current.CancellationToken);

        using var body = JsonDocument.Parse(sentBody!);
        body.RootElement.TryGetProperty("thinking", out var thinking).Should().Be(expected);
        if (expected)
        {
            thinking.GetProperty("type").GetString().Should().Be("disabled");
        }
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task ChatAsync_ProviderError_IsAFailure(string provider)
    {
        var client = Create(provider, """{"error":"boom"}""", _ => { }, HttpStatusCode.InternalServerError);

        var result = await client.ChatAsync("model-x", [new LlmChatMessage(LlmChatRoles.User, "x")], [], 0.2, 100,
            TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.ToolCalls.Should().BeEmpty();
    }

    private static ILlmClient Create(string provider, string responseJson, Action<string> onSend,
        HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage request, CancellationToken ct) =>
            {
                onSend(await request.Content!.ReadAsStringAsync(ct));
                return new HttpResponseMessage(status) { Content = new StringContent(responseJson, Encoding.UTF8, "application/json") };
            });
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler.Object));

        var resolver = new Mock<IProviderCredentialResolver>();
        resolver.Setup(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("sk-test");
        var services = new ServiceCollection();
        services.AddSingleton(resolver.Object);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var calculator = new Mock<ILlmCostCalculator>();
        calculator.Setup(c => c.CalculateCost(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns((string model, string p, int prompt, int completion) => new LlmCostCalculation
            {
                ModelId = model, Provider = p, PromptTokens = prompt, CompletionTokens = completion,
                InputCost = 0.00001m, OutputCost = 0.00002m,
            });

        return provider == "OpenRouter"
            ? new OpenRouterLlmClient(factory.Object, scopeFactory, calculator.Object, Mock.Of<ILogger<OpenRouterLlmClient>>())
            : new DeepSeekLlmClient(factory.Object, scopeFactory, calculator.Object, Mock.Of<ILogger<DeepSeekLlmClient>>());
    }
}
