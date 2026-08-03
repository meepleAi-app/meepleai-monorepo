using System.Net;
using Api.BoundedContexts.Administration.Infrastructure.DependencyInjection;
using Api.BoundedContexts.Administration.Infrastructure.External;
using Api.SharedKernel.Infrastructure.Http;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Infrastructure.External;

/// <summary>
/// Proves the Slack webhook egress is actually wired to the SSRF connect-pin (issue #3495 fix 3/N):
/// a webhook whose host resolves to a private/reserved address must fail closed at connect time —
/// WITHOUT the removed pre-connect DNS check. Resolving through the real DI registration
/// (<see cref="AdministrationServiceExtensions.AddSlackWebhookClientForTests"/>) guards against the
/// pin being silently dropped from the pipeline; the pin's own fail-closed decision is covered by
/// SsrfPinnedConnectTests.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
public sealed class SlackWebhookClientPinIntegrationTests
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
    public async Task Slack_WebhookResolvingToPrivateIp_FailsClosedAtConnectPin()
    {
        var dns = new StubDnsResolver("10.0.0.5");
        var services = new ServiceCollection();
        // Registered BEFORE the seam so its TryAddSingleton<IDnsResolver> does not override this stub.
        services.AddSingleton<IDnsResolver>(dns);
        AdministrationServiceExtensions.AddSlackWebhookClientForTests(services);
        using var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<ISlackWebhookClient>();

        // The host literal (8.8.8.8) is public, but the pinned resolver returns a private address,
        // so the pin throws at connect and SendAsync surfaces a failure rather than dialing out.
        var result = await client.SendAsync(
            "https://8.8.8.8/services/T1/B1/abc",
            new SlackMessage("Hello"),
            CancellationToken.None);

        result.Success.Should().BeFalse("the SSRF connect-pin must block a private-resolving webhook host");
        // The stub is consulted ONLY by the pin's ConnectCallback — a non-zero count proves
        // ConfigureSsrfPin is actually wired. Without the pin the default handler would do its own
        // DNS (8.8.8.8, public) and never touch this stub, so this assertion fails deterministically
        // if the pin is ever silently dropped from AddSlackWebhookClient.
        dns.ResolveCalls.Should().BeGreaterThan(0, "the connect-pin must consult the injected resolver");
    }
}
