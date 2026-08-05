using System.Net;
using Api.BoundedContexts.UserNotifications.Infrastructure.DependencyInjection;
using Api.Extensions;
using Api.SharedKernel.Infrastructure.Http;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Architecture;

/// <summary>
/// Issue #3495 Slice E — behavioural counterpart to
/// <see cref="EgressHttpClientPinArchitectureTests"/>: the source gate proves
/// <c>ConfigureSsrfPin</c> is written at the registration site, these tests prove the resulting
/// client actually dials through the pin.
/// <para>
/// Each test resolves the client from the REAL registration seam and asserts two things: the request
/// fails closed when the host resolves to a private/reserved address, and the injected resolver was
/// consulted. The second assertion is what makes the test non-vacuous — without the pin the default
/// handler would do its own DNS and never touch the stub, so a silently dropped pin fails this
/// deterministically. Mirrors <c>BggCoverDownloaderPinIntegrationTests</c> (#3495 fix 3/N).
/// </para>
/// <para>
/// The typed <c>IBggApiClient</c> is exercised through the same registration helper as the named
/// client below, but is not driven end-to-end here: its transient-error retry policy would sleep
/// 2+4+8s on the blocked connect. Its pin is enforced by the source gate, which covers both call
/// sites.
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Architecture")]
[Trait("Issue", "3495")]
public sealed class PinnedEgressClientWiringTests
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
    public async Task BggApiClient_FailsClosed_WhenTheBggHostResolvesToAPrivateAddress()
    {
        // 169.254.169.254 is the cloud metadata endpoint: the canonical SSRF target.
        var dns = new StubDnsResolver("169.254.169.254");
        var services = new ServiceCollection();
        // Registered BEFORE the seam so ConfigureSsrfPin's TryAddSingleton does not override the stub.
        services.AddSingleton<IDnsResolver>(dns);
        InfrastructureServiceExtensions.AddBoardGameGeekClientsForTests(services);
        using var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("BggApi");

        var act = () => client.GetAsync(new Uri("search?query=catan", UriKind.Relative), CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>(
            "the SSRF connect-pin must block a BGG host that resolves to a reserved address");
        dns.ResolveCalls.Should().BeGreaterThan(0, "the connect-pin must consult the injected resolver");
    }

    [Fact]
    public async Task SlackApiClient_FailsClosed_WhenTheWebhookHostResolvesToAPrivateAddress()
    {
        var dns = new StubDnsResolver("127.0.0.1");
        var services = new ServiceCollection();
        services.AddSingleton<IDnsResolver>(dns);
        UserNotificationsServiceExtensions.AddSlackApiClientForTests(services);
        using var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("SlackApi");

        // The URL is public, but the stored Slack connection could point anywhere — the pin decides
        // on the RESOLVED address, which is loopback here.
        using var body = new StringContent("{}");
        var act = () => client.PostAsync(
            new Uri("https://hooks.slack.com/services/T000/B000/xxx"), body, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>(
            "the SSRF connect-pin must block a Slack target that resolves to loopback");
        dns.ResolveCalls.Should().BeGreaterThan(0, "the connect-pin must consult the injected resolver");
    }
}
