using System.Net;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.DependencyInjection;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
using Api.SharedKernel.Infrastructure.Http;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure.Services;

/// <summary>
/// Proves the Wikimedia Commons egress is actually wired to the SSRF connect-pin (issue #3495
/// follow-up): resolved through the REAL catalog-seed registration
/// (<see cref="SharedGameCatalogServiceExtensions.AddCatalogSeedProvidersForTests"/>), a Commons
/// host resolving to a private/reserved address fails closed at connect time. The stub resolver is
/// consulted ONLY by the pin's ConnectCallback, so asserting <c>ResolveCalls &gt; 0</c> guards
/// against the pin being silently dropped — with no network dependency. This also composes the pin
/// with the <c>WikimediaCircuitBreakerHandler</c> delegating handler present on the same client, and
/// exercises the client's NARROW catch (HttpRequestException) — confirming the pin's connect-time
/// failure surfaces as HttpRequestException rather than escaping as a raw InvalidOperationException.
/// (Wikidata SPARQL + BGG XMLAPI2 use the identical one-line <c>.ConfigureSsrfPin()</c> in the same
/// registration; this Commons case — the one with a redirect requirement and a narrow catch — is the
/// representative proof for the catalog-seed egress slice.)
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class WikimediaCommonsClientPinIntegrationTests
{
    private sealed class StubDnsResolver : IDnsResolver
    {
        private readonly IPAddress _address;
        public int ResolveCalls { get; private set; }

        public StubDnsResolver(string ip) => _address = IPAddress.Parse(ip);

        public Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken ct)
        {
            ResolveCalls++;
            return Task.FromResult<IReadOnlyList<IPAddress>>(new[] { _address });
        }
    }

    [Fact]
    public async Task Commons_HostResolvingToPrivateIp_FailsClosedAtConnectPin()
    {
        var dns = new StubDnsResolver("169.254.169.254"); // cloud metadata endpoint
        var services = new ServiceCollection();
        // Registered BEFORE the seam so its TryAddSingleton<IDnsResolver> does not override this stub.
        services.AddSingleton<IDnsResolver>(dns);
        SharedGameCatalogServiceExtensions.AddCatalogSeedProvidersForTests(services);
        using var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<IWikimediaCommonsClient>();

        // commons.wikimedia.org resolves (via the pinned stub) to a blocked address, so the pin
        // throws at connect; FetchLicenseAsync must swallow that into NotAvailable (not propagate).
        var result = await client.FetchLicenseAsync("Some_cover.jpg", CancellationToken.None);

        result.Should().Be(
            CommonsLicenseResult.NotAvailable(),
            "the SSRF connect-pin must block a private-resolving Commons host and fail closed");
        dns.ResolveCalls.Should().BeGreaterThan(
            0, "the connect-pin must consult the injected resolver — proving ConfigureSsrfPin is wired through the real registration");
    }
}
