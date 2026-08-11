using System.Net;
using Xunit;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="PaidAiHostGuardHandler"/>.
///
/// REQ-AI-TEST-001: automated tests must never consume paid AI tokens. This handler is the
/// fail-closed network guard: in the Testing/CI environment it is installed on every HttpClient
/// created by IHttpClientFactory, so any attempt to reach a paid LLM provider (OpenRouter,
/// DeepSeek, OpenAI, Anthropic) throws instead of spending — even if a real API key is present.
/// </summary>
public class PaidAiHostGuardHandlerTests
{
    private static HttpMessageInvoker CreateInvoker(RecordingInnerHandler inner)
        => new(new PaidAiHostGuardHandler { InnerHandler = inner });

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("https://openrouter.ai/api/v1/chat/completions")]
    [InlineData("https://api.openai.com/v1/chat/completions")]
    [InlineData("https://api.anthropic.com/v1/messages")]
    [InlineData("https://api.deepseek.com/v1/chat/completions")]
    public async Task SendAsync_ToPaidAiHost_Throws_WithoutReachingNetwork(string url)
    {
        var inner = new RecordingInnerHandler(HttpStatusCode.OK);
        var invoker = CreateInvoker(inner);

        var ex = await Assert.ThrowsAsync<PaidAiCallInTestException>(
            () => invoker.SendAsync(new HttpRequestMessage(HttpMethod.Post, url), CancellationToken.None));

        // Fail-closed: the request must be blocked BEFORE it is delegated to the network.
        Assert.False(inner.WasCalled, "Guard must block the request before the inner (network) handler runs.");
        Assert.Contains(new Uri(url).Host, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("https://sub.openrouter.ai/api/v1/chat/completions")]
    [InlineData("https://openrouter.ai:443/api/v1/chat/completions")]
    [InlineData("https://openrouter.ai./api/v1/chat/completions")] // trailing-dot FQDN
    [InlineData("https://OPENROUTER.AI/api/v1/chat/completions")]  // uppercase
    public async Task SendAsync_ToPaidAiSubdomainOrPort_Throws(string url)
    {
        var inner = new RecordingInnerHandler(HttpStatusCode.OK);
        var invoker = CreateInvoker(inner);

        await Assert.ThrowsAsync<PaidAiCallInTestException>(
            () => invoker.SendAsync(new HttpRequestMessage(HttpMethod.Post, url), CancellationToken.None));

        Assert.False(inner.WasCalled);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("http://localhost:8000/embed")]
    [InlineData("http://embedding:8000/embed")]
    [InlineData("http://reranker:8000/rerank")]
    [InlineData("http://ollama:11434/api/generate")]
    public async Task SendAsync_ToLocalOrSelfHostedHost_Delegates(string url)
    {
        var inner = new RecordingInnerHandler(HttpStatusCode.OK);
        var invoker = CreateInvoker(inner);

        var response = await invoker.SendAsync(
            new HttpRequestMessage(HttpMethod.Post, url), CancellationToken.None);

        Assert.True(inner.WasCalled, "Local / self-hosted AI hosts must be delegated, not blocked.");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Inner handler that records whether it was invoked and returns a canned response.
    /// Lets each test assert the guard blocked BEFORE any (mocked) network I/O.
    /// </summary>
    private sealed class RecordingInnerHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;

        public RecordingInnerHandler(HttpStatusCode status) => _status = status;

        public bool WasCalled { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult(new HttpResponseMessage(_status));
        }
    }
}
