using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="PaidAiHostGuardExtensions"/> — the wiring that installs the
/// fail-closed guard on every HttpClient produced by IHttpClientFactory (REQ-AI-TEST-001).
/// Uses a counting primary handler so no real network I/O ever occurs.
/// </summary>
public class PaidAiHostGuardExtensionsTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task AddPaidAiHostGuard_BlocksPaidAiCall_OnNamedClient()
    {
        var primary = new CountingPrimaryHandler();
        var services = new ServiceCollection();
        services.AddHttpClient("OpenRouter").ConfigurePrimaryHttpMessageHandler(() => primary);
        services.AddPaidAiHostGuard();

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("OpenRouter");

        await Assert.ThrowsAsync<PaidAiCallInTestException>(
            () => client.PostAsync("https://openrouter.ai/api/v1/chat/completions", new StringContent("{}")));

        // Fail-closed: the guard blocked the call before it reached the (mocked) network handler.
        Assert.Equal(0, primary.Calls);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task AddPaidAiHostGuard_AllowsLocalCall_OnNamedClient()
    {
        var primary = new CountingPrimaryHandler();
        var services = new ServiceCollection();
        services.AddHttpClient("Embedding").ConfigurePrimaryHttpMessageHandler(() => primary);
        services.AddPaidAiHostGuard();

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("Embedding");

        var response = await client.PostAsync("http://localhost:8000/embed", new StringContent("{}"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, primary.Calls);
    }

    private sealed class CountingPrimaryHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
