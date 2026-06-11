using System.Net;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Resilience;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using Xunit;

namespace Api.Tests.Integration.SharedGameCatalog;

/// <summary>
/// Integration tests for the M10 <see cref="WikimediaCircuitBreakerHandler"/>
/// when registered via the standard <c>AddHttpClient(...).AddHttpMessageHandler(...)</c>
/// DI extension. Validates that:
/// <list type="bullet">
///   <item>A typed HttpClient resolved from <see cref="IServiceProvider"/> gets a
///   functional circuit breaker — the unit tests only assert behavior on a
///   hand-wired DelegatingHandler chain.</item>
///   <item>Two distinct typed clients (Wikidata SPARQL vs Commons API) get
///   independent breaker instances — a SPARQL outage MUST NOT collapse Commons
///   traffic (ADR DEC-3f per-client isolation).</item>
/// </list>
/// </summary>
/// <remarks>
/// Does not need Testcontainers — exercises the in-process DI + HttpClient
/// factory pipeline with a fake inner handler. Issue #1823 Wave 3 M16 per
/// Lisa Crispin spec-panel recommendation.
/// </remarks>
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "1823")]
public class WikimediaCircuitBreakerIntegrationTests
{
    private interface IFakeWikidataClient
    {
        Task<HttpResponseMessage> GetAsync(string path, CancellationToken ct);
    }

    private interface IFakeCommonsClient
    {
        Task<HttpResponseMessage> GetAsync(string path, CancellationToken ct);
    }

    private sealed class FakeWikidataClient : IFakeWikidataClient
    {
        private readonly HttpClient _client;
        public FakeWikidataClient(HttpClient client) => _client = client;
        public Task<HttpResponseMessage> GetAsync(string path, CancellationToken ct) =>
            _client.GetAsync(path, ct);
    }

    private sealed class FakeCommonsClient : IFakeCommonsClient
    {
        private readonly HttpClient _client;
        public FakeCommonsClient(HttpClient client) => _client = client;
        public Task<HttpResponseMessage> GetAsync(string path, CancellationToken ct) =>
            _client.GetAsync(path, ct);
    }

    private sealed class FixedResponseHandler : DelegatingHandler
    {
        private readonly Func<HttpResponseMessage> _factory;
        public int CallCount { get; private set; }

        public FixedResponseHandler(Func<HttpResponseMessage> factory)
        {
            _factory = factory;
            // No InnerHandler assignment — when this handler sits in the
            // ConfigurePrimaryHttpMessageHandler slot, the HttpClientFactory
            // pipeline builder injects the chain end for us. Assigning a
            // dummy HttpClientHandler here would only be a dead-code leak.
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(_factory());
        }
    }

    private static ServiceProvider BuildHost(
        out FixedResponseHandler wikidataInner,
        out FixedResponseHandler commonsInner,
        Func<HttpResponseMessage>? wikidataFactory = null,
        Func<HttpResponseMessage>? commonsFactory = null)
    {
        var wikidata = new FixedResponseHandler(wikidataFactory
            ?? (() => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        var commons = new FixedResponseHandler(commonsFactory
            ?? (() => new HttpResponseMessage(HttpStatusCode.OK)));
        wikidataInner = wikidata;
        commonsInner = commons;

        var services = new ServiceCollection();
        services.AddLogging();

        services.AddHttpClient<IFakeWikidataClient, FakeWikidataClient>(client =>
            client.BaseAddress = new Uri("https://wikidata.test/"))
            .AddHttpMessageHandler(sp => new WikimediaCircuitBreakerHandler(
                sp.GetRequiredService<ILogger<WikimediaCircuitBreakerHandler>>(),
                clientName: "wikidata-sparql"))
            .ConfigurePrimaryHttpMessageHandler(() => wikidata);

        services.AddHttpClient<IFakeCommonsClient, FakeCommonsClient>(client =>
            client.BaseAddress = new Uri("https://commons.test/"))
            .AddHttpMessageHandler(sp => new WikimediaCircuitBreakerHandler(
                sp.GetRequiredService<ILogger<WikimediaCircuitBreakerHandler>>(),
                clientName: "commons-api"))
            .ConfigurePrimaryHttpMessageHandler(() => commons);

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Wikidata5xxBurst_OpensCircuit_OnFourthCall_BrokenCircuit()
    {
        using var sp = BuildHost(out var wikidata, out _);
        var client = sp.GetRequiredService<IFakeWikidataClient>();

        // 3 consecutive 5xx ticks the breaker open.
        for (var i = 0; i < WikimediaCircuitBreakerHandler.ConsecutiveFailureThreshold; i++)
        {
            var resp = await client.GetAsync("/probe", default);
            ((int)resp.StatusCode).Should().BeGreaterThanOrEqualTo(500);
        }

        // 4th call is short-circuited before reaching the inner handler.
        var act = async () => await client.GetAsync("/probe", default);
        await act.Should().ThrowAsync<BrokenCircuitException>();

        wikidata.CallCount.Should().Be(WikimediaCircuitBreakerHandler.ConsecutiveFailureThreshold);
    }

    [Fact]
    public async Task WikidataCircuitOpen_DoesNotAffectCommonsClient()
    {
        using var sp = BuildHost(out _, out var commons);
        var wikidataClient = sp.GetRequiredService<IFakeWikidataClient>();
        var commonsClient = sp.GetRequiredService<IFakeCommonsClient>();

        // Trip the Wikidata breaker open.
        for (var i = 0; i < WikimediaCircuitBreakerHandler.ConsecutiveFailureThreshold; i++)
        {
            await wikidataClient.GetAsync("/probe", default);
        }
        await wikidataClient.Invoking(c => c.GetAsync("/probe", default))
            .Should().ThrowAsync<BrokenCircuitException>();

        // Commons remains usable — ADR DEC-3f per-client isolation.
        var commonsResp = await commonsClient.GetAsync("/api", default);
        commonsResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "a SPARQL outage MUST NOT collapse Commons traffic — independent breaker instances");
        commons.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task NonTransient4xx_DoesNotTickBreaker()
    {
        using var sp = BuildHost(out var wikidata, out _,
            wikidataFactory: () => new HttpResponseMessage(HttpStatusCode.NotFound));
        var client = sp.GetRequiredService<IFakeWikidataClient>();

        // 4 consecutive 404 — breaker stays CLOSED, inner handler still hit.
        for (var i = 0; i < 4; i++)
        {
            var resp = await client.GetAsync("/probe", default);
            resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        wikidata.CallCount.Should().Be(4,
            "4xx is client-side — the breaker MUST forward every request to the inner handler");
    }
}
