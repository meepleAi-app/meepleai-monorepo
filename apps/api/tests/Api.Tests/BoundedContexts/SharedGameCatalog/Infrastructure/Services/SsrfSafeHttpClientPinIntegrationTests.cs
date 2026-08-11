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
/// Proves the arbitrary-URL download client (<see cref="SsrfSafeHttpClient"/>, the #3470 Slice 3
/// prerequisite) is actually wired to the SSRF connect-pin (issue #3495 fix 5/N): a host resolving
/// to a private/reserved address fails closed at connect time — WITHOUT the retired pre-connect DNS
/// check. Resolving through the real DI registration
/// (<see cref="SharedGameCatalogServiceExtensions.AddSsrfSafeHttpClientForTests"/>) guards against the
/// pin being silently dropped; the pin's own fail-closed decision is covered by SsrfPinnedConnectTests.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class SsrfSafeHttpClientPinIntegrationTests
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
    public async Task ManualFetch_HostResolvingToPrivateIp_FailsClosedAtConnectPin()
    {
        var dns = new StubDnsResolver("169.254.169.254"); // cloud metadata endpoint
        var services = new ServiceCollection();
        // Registered BEFORE the seam so its TryAddSingleton<IDnsResolver> does not override this stub.
        services.AddSingleton<IDnsResolver>(dns);
        SharedGameCatalogServiceExtensions.AddSsrfSafeHttpClientForTests(services);
        using var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<SsrfSafeHttpClient>();

        // The host literal (8.8.8.8) is public and passes the scheme check, but the pinned resolver
        // returns the metadata IP, so the pin throws at connect and the download aborts.
        var act = () => client.DownloadImageAsync("https://8.8.8.8/cover.jpg", CancellationToken.None);

        await act.Should().ThrowAsync<Exception>("the SSRF connect-pin must block a private-resolving host");
        // The stub is consulted ONLY by the pin's ConnectCallback — a non-zero count proves
        // ConfigureSsrfPin is actually wired. Without the pin the default handler would do its own
        // DNS (8.8.8.8, public) and never touch this stub, so this fails deterministically if the pin
        // is ever silently dropped from AddSsrfSafeHttpClient.
        dns.ResolveCalls.Should().BeGreaterThan(0, "the connect-pin must consult the injected resolver");
    }
}
