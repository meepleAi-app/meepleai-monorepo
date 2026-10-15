using Api.BoundedContexts.Administration.Infrastructure.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.Services.LlmClients;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.Services.LlmClients;

/// <summary>
/// Unit tests for DeepSeekLlmClient
/// ISSUE-419: Direct DeepSeek provider — unconfigured state and model routing
/// ISSUE-1859: API key now resolved via IProviderCredentialResolver (DB→env-var cascade)
/// </summary>
public class DeepSeekLlmClientTests
{
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    // ─── ProviderName ────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void ProviderName_ReturnsDeepSeek()
    {
        // Arrange & Act
        var client = CreateUnconfiguredClient();

        // Assert
        client.ProviderName.Should().Be("DeepSeek");
    }

    // ─── SupportsModel ───────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void SupportsModel_DeepSeekModels_ReturnsTrue()
    {
        // Arrange
        var client = CreateUnconfiguredClient();

        // Act & Assert
        client.SupportsModel("deepseek-chat").Should().BeTrue();
        client.SupportsModel("deepseek-coder").Should().BeTrue();
        client.SupportsModel("deepseek-reasoner").Should().BeTrue();
        client.SupportsModel("DeepSeek-chat").Should().BeTrue(); // case-insensitive
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void SupportsModel_NonDeepSeekModels_ReturnsFalse()
    {
        // Arrange
        var client = CreateUnconfiguredClient();

        // Act & Assert
        client.SupportsModel("gpt-4o").Should().BeFalse();
        client.SupportsModel("meta-llama/llama-3.3-70b-instruct").Should().BeFalse();
        client.SupportsModel("ollama:llama3").Should().BeFalse();
        client.SupportsModel("openai/gpt-4o-mini").Should().BeFalse();
    }

    // ─── GenerateCompletionAsync — unconfigured ──────────────────────────────

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task GenerateCompletionAsync_WhenNotConfigured_ReturnsFailure()
    {
        // Arrange
        var client = CreateUnconfiguredClient();

        // Act
        var result = await client.GenerateCompletionAsync(
            "deepseek-chat",
            "You are helpful",
            "Hello",
            0.7,
            100,
            TestCancellationToken);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    // ─── GenerateCompletionStreamAsync — unconfigured ────────────────────────

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task GenerateCompletionStreamAsync_WhenNotConfigured_YieldsNothing()
    {
        // Arrange
        var client = CreateUnconfiguredClient();
        var chunks = new List<StreamChunk>();

        // Act
        await foreach (var chunk in client.GenerateCompletionStreamAsync(
            "deepseek-chat",
            "You are helpful",
            "Hello",
            0.7,
            100,
            TestCancellationToken))
        {
            chunks.Add(chunk);
        }

        // Assert
        chunks.Should().BeEmpty();
    }

    // ─── CheckHealthAsync — unconfigured ─────────────────────────────────────

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task CheckHealthAsync_WhenNotConfigured_ReturnsFalse()
    {
        // Arrange
        var client = CreateUnconfiguredClient();

        // Act
        var healthy = await client.CheckHealthAsync(TestCancellationToken);

        // Assert
        healthy.Should().BeFalse();
    }

    // ─── CheckHealthAsync — balance-aware (#2953 #5) ─────────────────────────

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task CheckHealthAsync_WhenBalanceAvailable_ReturnsTrue()
    {
        // Arrange
        var client = CreateClientWithResponse(JsonResponse(
            HttpStatusCode.OK,
            "{\"is_available\": true, \"balance_infos\": [{\"currency\": \"USD\", \"total_balance\": \"10.00\"}]}"));

        // Act
        var healthy = await client.CheckHealthAsync(TestCancellationToken);

        // Assert
        healthy.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task CheckHealthAsync_WhenBalanceUnavailable_ReturnsFalse()
    {
        // #2953 (#5): credit exhausted → is_available=false. GET /models would still return 200
        // and hide the exhaustion; /user/balance surfaces it so the provider is marked unhealthy.
        // Arrange
        var client = CreateClientWithResponse(JsonResponse(
            HttpStatusCode.OK,
            "{\"is_available\": false, \"balance_infos\": [{\"currency\": \"USD\", \"total_balance\": \"0.00\"}]}"));

        // Act
        var healthy = await client.CheckHealthAsync(TestCancellationToken);

        // Assert
        healthy.Should().BeFalse();
    }

    // ─── Ragionamento (#4381) ────────────────────────────────────────────────
    // DeepSeek ha il thinking attivo per default: i token di ragionamento arrivano in
    // reasoning_content, contano in max_tokens e la temperatura è ignorata. Le risposte della
    // pipeline lo spengono; le altre chiamate restano sul default del modello.

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task GenerateCompletionStreamWithoutReasoningAsync_AsksDeepSeekToSkipThinking()
    {
        // Arrange
        var sent = new List<string>();
        var client = CreateClientCapturing(sent, SseResponse(ContentOnlyStream));

        // Act
        await DrainAsync(((ILlmReasoningSwitch)client).GenerateCompletionStreamWithoutReasoningAsync(
            "deepseek-flash", "system", "How many cards?", 0.3, 1500, TestCancellationToken));

        // Assert
        ThinkingTypeOf(sent.Should().ContainSingle().Which).Should().Be("disabled");
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task GenerateCompletionWithoutReasoningAsync_AsksDeepSeekToSkipThinking()
    {
        // Arrange
        var sent = new List<string>();
        var client = CreateClientCapturing(sent, JsonResponse(HttpStatusCode.OK, CompletionBody));

        // Act
        var result = await ((ILlmReasoningSwitch)client).GenerateCompletionWithoutReasoningAsync(
            "deepseek-flash", "system", "How many cards?", 0.3, 1500, TestCancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        ThinkingTypeOf(sent.Should().ContainSingle().Which).Should().Be("disabled");
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task GenerateCompletionStreamAsync_LeavesThinkingToTheModelDefault()
    {
        // Arrange: le chiamate che non sono risposte della pipeline non cambiano.
        var sent = new List<string>();
        var client = CreateClientCapturing(sent, SseResponse(ContentOnlyStream));

        // Act
        await DrainAsync(client.GenerateCompletionStreamAsync(
            "deepseek-flash", "system", "How many cards?", 0.3, 1500, TestCancellationToken));

        // Assert
        ThinkingTypeOf(sent.Should().ContainSingle().Which).Should().BeNull();
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task GenerateCompletionStreamAsync_ReasoningOnlyStream_YieldsNoText_AndEndsWithLength()
    {
        // Arrange: la forma registrata il 2026-10-15 da una risposta vuota di deepseek-flash, con
        // il thinking attivo — ogni token pagato è ragionamento, nessuno è testo.
        var client = CreateClientCapturing([], SseResponse(ReasoningOnlyStream));

        // Act
        var chunks = await DrainAsync(client.GenerateCompletionStreamAsync(
            "deepseek-flash", "system", "How many cards?", 0.3, 80, TestCancellationToken));

        // Assert
        chunks.Should().NotContain(c => !string.IsNullOrEmpty(c.Content));
        var final = chunks.Should().ContainSingle(c => c.IsFinal).Which;
        final.FinishReason.Should().Be("length");
        final.Usage!.CompletionTokens.Should().Be(80);
    }

    private const string CompletionBody =
        "{\"id\":\"x\",\"object\":\"chat.completion\",\"model\":\"deepseek-flash\"," +
        "\"choices\":[{\"index\":0,\"message\":{\"role\":\"assistant\",\"content\":\"Five cards.\"},\"finish_reason\":\"stop\"}]," +
        "\"usage\":{\"prompt_tokens\":47,\"completion_tokens\":3,\"total_tokens\":50}}";

    private const string ContentOnlyStream =
        "data: {\"id\":\"x\",\"model\":\"deepseek-flash\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\"Five\"},\"finish_reason\":null}]}\n\n" +
        "data: {\"id\":\"x\",\"model\":\"deepseek-flash\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\" cards.\"},\"finish_reason\":\"stop\"}]," +
        "\"usage\":{\"prompt_tokens\":47,\"completion_tokens\":3,\"total_tokens\":50}}\n\n" +
        "data: [DONE]\n\n";

    private const string ReasoningOnlyStream =
        "data: {\"id\":\"x\",\"model\":\"deepseek-flash\",\"choices\":[{\"index\":0,\"delta\":{\"role\":\"assistant\",\"content\":null,\"reasoning_content\":\"\"},\"finish_reason\":null}]}\n\n" +
        "data: {\"id\":\"x\",\"model\":\"deepseek-flash\",\"choices\":[{\"index\":0,\"delta\":{\"content\":null,\"reasoning_content\":\"We need to\"},\"finish_reason\":null}]}\n\n" +
        "data: {\"id\":\"x\",\"model\":\"deepseek-flash\",\"choices\":[{\"index\":0,\"delta\":{\"content\":null,\"reasoning_content\":\" count the cards.\"},\"finish_reason\":null}]}\n\n" +
        "data: {\"id\":\"x\",\"model\":\"deepseek-flash\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\"\",\"reasoning_content\":null},\"finish_reason\":\"length\"}]," +
        "\"usage\":{\"prompt_tokens\":72,\"completion_tokens\":80,\"total_tokens\":152,\"completion_tokens_details\":{\"reasoning_tokens\":80}}}\n\n" +
        "data: [DONE]\n\n";

    /// <summary>Il valore di <c>thinking.type</c> nel corpo inviato, o <c>null</c> se il campo manca.</summary>
    private static string? ThinkingTypeOf(string requestBody)
    {
        using var document = System.Text.Json.JsonDocument.Parse(requestBody);
        return document.RootElement.TryGetProperty("thinking", out var thinking)
            ? thinking.GetProperty("type").GetString()
            : null;
    }

    private static async Task<List<StreamChunk>> DrainAsync(IAsyncEnumerable<StreamChunk> stream)
    {
        var chunks = new List<StreamChunk>();
        await foreach (var chunk in stream)
        {
            chunks.Add(chunk);
        }

        return chunks;
    }

    private static HttpResponseMessage SseResponse(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "text/event-stream") };

    /// <summary>
    /// Un client configurato che risponde con <paramref name="response"/> e annota in
    /// <paramref name="sentBodies"/> il corpo di ogni richiesta, con un listino che prezza ogni chiamata.
    /// </summary>
    private static DeepSeekLlmClient CreateClientCapturing(List<string> sentBodies, HttpResponseMessage response)
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>(async (request, ct) =>
            {
                sentBodies.Add(await request.Content!.ReadAsStringAsync(ct));
                return response;
            });

        var httpClient = new HttpClient(handler.Object);
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var resolver = new Mock<IProviderCredentialResolver>();
        resolver
            .Setup(r => r.ResolveAsync(DeepSeekLlmClient.ProviderKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-api-key");

        var services = new ServiceCollection();
        services.AddSingleton(resolver.Object);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var calculator = new Mock<ILlmCostCalculator>();
        calculator
            .Setup(c => c.CalculateCost(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns((string modelId, string provider, int prompt, int completion) => new LlmCostCalculation
            {
                ModelId = modelId,
                Provider = provider,
                PromptTokens = prompt,
                CompletionTokens = completion,
                InputCost = prompt * 0.0000003m,
                OutputCost = completion * 0.0000012m,
            });

        return new DeepSeekLlmClient(mockFactory.Object, scopeFactory, calculator.Object, Mock.Of<ILogger<DeepSeekLlmClient>>());
    }

    // ─── Factory helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Creates a DeepSeekLlmClient backed by a resolver that always throws
    /// <see cref="ProviderCredentialNotConfiguredException"/> — simulates a deployment
    /// where neither a DB row nor the <c>DEEPSEEK_API_KEY</c> env var is configured.
    /// </summary>
    private static DeepSeekLlmClient CreateUnconfiguredClient()
    {
        var mockFactory = new Mock<IHttpClientFactory>();
        var httpClient = new HttpClient();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        // Resolver throws ProviderCredentialNotConfiguredException — clients must degrade gracefully.
        var resolver = new Mock<IProviderCredentialResolver>();
        resolver
            .Setup(r => r.ResolveAsync(DeepSeekLlmClient.ProviderKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProviderCredentialNotConfiguredException("deepseek"));

        var services = new ServiceCollection();
        services.AddSingleton(resolver.Object);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var mockCostCalculator = new Mock<ILlmCostCalculator>();
        mockCostCalculator
            .Setup(c => c.CalculateCost(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>()))
            .Returns((string modelId, string provider, int prompt, int completion) =>
                new LlmCostCalculation
                {
                    ModelId = modelId,
                    Provider = provider,
                    PromptTokens = prompt,
                    CompletionTokens = completion,
                    InputCost = 0.000001m,
                    OutputCost = 0.000002m
                });

        var logger = Mock.Of<ILogger<DeepSeekLlmClient>>();

        return new DeepSeekLlmClient(mockFactory.Object, scopeFactory, mockCostCalculator.Object, logger);
    }

    /// <summary>
    /// Creates a configured DeepSeekLlmClient whose HTTP calls are all served by
    /// <paramref name="response"/>. The resolver returns a dummy key so CheckHealthAsync reaches
    /// the HTTP call (unlike <see cref="CreateUnconfiguredClient"/> which short-circuits).
    /// </summary>
    private static DeepSeekLlmClient CreateClientWithResponse(HttpResponseMessage response)
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        var httpClient = new HttpClient(handler.Object);
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var resolver = new Mock<IProviderCredentialResolver>();
        resolver
            .Setup(r => r.ResolveAsync(DeepSeekLlmClient.ProviderKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-api-key");

        var services = new ServiceCollection();
        services.AddSingleton(resolver.Object);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        return new DeepSeekLlmClient(
            mockFactory.Object,
            scopeFactory,
            Mock.Of<ILlmCostCalculator>(),
            Mock.Of<ILogger<DeepSeekLlmClient>>());
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json) =>
        new(statusCode) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}
