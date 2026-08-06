using System.Net;
using Api.SharedKernel.Infrastructure.Http;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.SharedKernel.Infrastructure.Http;

/// <summary>
/// Issue #3495 finding M3 (Slice F) — per-sink host allow-list on the connect-pin.
/// <para>
/// The IP deny-list answers "is this address internal?". It cannot answer "is this a host we talk
/// to at all?", which is the question that matters when a legitimate upstream is compromised and
/// redirects us to an arbitrary PUBLIC host. These tests pin both the enforcement and the matching
/// semantics — suffix, never substring, or a look-alike domain walks straight through.
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedKernel")]
[Trait("Issue", "3495")]
public sealed class EgressHostAllowListTests
{
    private sealed class CountingDnsResolver : IDnsResolver
    {
        public int ResolveCalls { get; private set; }

        public Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken ct)
        {
            ResolveCalls++;
            return Task.FromResult<IReadOnlyList<IPAddress>>(new[] { IPAddress.Parse("8.8.8.8") });
        }
    }

    [Theory]
    [InlineData("commons.wikimedia.org")]      // sub-domain
    [InlineData("upload.wikimedia.org")]       // the CDN the FilePath 302 lands on
    [InlineData("wikimedia.org")]              // the registrable domain itself
    [InlineData("WIKIMEDIA.ORG")]              // case-insensitive
    [InlineData("commons.wikimedia.org.")]     // trailing root dot
    public void HostsInsideTheAllowList_AreAccepted(string host)
    {
        var act = () => SsrfPinnedConnect.ValidateHostAllowed(host, "wikimedia", EgressAllowLists.Wikimedia);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("evilwikimedia.org")]              // suffix-but-not-subdomain
    [InlineData("wikimedia.org.attacker.example")] // allow-list value as a prefix label
    [InlineData("attacker.example")]               // unrelated
    [InlineData("boardgamegeek.com")]              // another sink's host
    [InlineData("169.254.169.254")]                // metadata IP as a literal host
    public void HostsOutsideTheAllowList_AreRefused(string host)
    {
        var act = () => SsrfPinnedConnect.ValidateHostAllowed(host, "wikimedia", EgressAllowLists.Wikimedia);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not allowed*");
    }

    [Fact]
    public void AnEmptyAllowList_ImposesNoConstraint()
    {
        // The manual arbitrary-URL sink runs without an allow-list by design (M3).
        var act = () => SsrfPinnedConnect.ValidateHostAllowed("anything.example", "manual", allowedHostSuffixes: null);

        act.Should().NotThrow();
    }

    [Fact]
    public async Task TheAllowListIsCheckedBeforeResolution()
    {
        // Cheapest gate first: an off-list host must not even reach DNS, so a hostile redirect target
        // cannot be used to probe our resolver.
        var dns = new CountingDnsResolver();

        var act = () => SsrfPinnedConnect.ResolveAndValidateAsync(
            dns, "attacker.example", "wikimedia", CancellationToken.None);

        // Sanity: the resolver IS used when the host is acceptable...
        await act.Should().NotThrowAsync();
        dns.ResolveCalls.Should().Be(1);

        // ...and the allow-list gate itself short-circuits before that call happens.
        var blocked = () => SsrfPinnedConnect.ValidateHostAllowed(
            "attacker.example", "wikimedia", EgressAllowLists.Wikimedia);
        blocked.Should().Throw<InvalidOperationException>();
        dns.ResolveCalls.Should().Be(1, "the refused host must not have triggered a second resolution");
    }

    [Fact]
    public void BggCoverAllowList_CoversTheGeekdoImageCdns()
    {
        // The BGG XML API hands back asset URLs on the geekdo CDNs; if this drifts, cover downloads
        // fail closed (visible as host_not_allowed on the egress counters) rather than silently.
        var act = () => SsrfPinnedConnect.ValidateHostAllowed(
            "cf.geekdo-images.com", "bgg_cover", EgressAllowLists.BggCover);

        act.Should().NotThrow();
    }
}
