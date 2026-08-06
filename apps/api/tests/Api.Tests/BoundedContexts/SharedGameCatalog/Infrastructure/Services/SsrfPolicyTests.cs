using System.Net;
using Api.SharedKernel.Infrastructure.Http;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure.Services;

/// <summary>
/// Deny-list matrix for the SSRF IP classifier (issue #3495, finding H3 — IANA
/// Special-Purpose registries, fail-closed on unknown high ranges). Each blocked range
/// has an in-range case; the allowed set pins the adjacent boundaries so the ranges
/// don't over-block public space.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class SsrfPolicyTests
{
    [Theory]
    // ── IPv4 pre-existing (must stay blocked) ──
    [InlineData("0.0.0.0")]           // 0.0.0.0/8 this-network
    [InlineData("10.0.0.1")]          // 10/8 private
    [InlineData("10.255.255.255")]    // 10/8 end
    [InlineData("127.0.0.1")]         // loopback
    [InlineData("127.0.0.2")]         // 127/8 loopback range
    [InlineData("172.16.0.1")]        // 172.16/12 start
    [InlineData("172.31.255.255")]    // 172.16/12 end
    [InlineData("192.168.0.1")]       // 192.168/16
    [InlineData("169.254.0.1")]       // link-local
    [InlineData("169.254.169.254")]   // cloud metadata endpoint
    // ── IPv4 newly added (H3) ──
    [InlineData("100.64.0.1")]        // 100.64/10 CGNAT
    [InlineData("100.127.255.255")]   // CGNAT end
    [InlineData("192.0.0.1")]         // 192.0.0/24 IETF protocol assignments
    [InlineData("192.0.2.5")]         // 192.0.2/24 TEST-NET-1
    [InlineData("192.88.99.1")]       // 192.88.99/24 6to4 anycast
    [InlineData("198.18.0.1")]        // 198.18/15 benchmarking
    [InlineData("198.19.255.255")]    // 198.18/15 end
    [InlineData("198.51.100.5")]      // 198.51.100/24 TEST-NET-2
    [InlineData("203.0.113.5")]       // 203.0.113/24 TEST-NET-3
    [InlineData("224.0.0.1")]         // 224/4 multicast
    [InlineData("239.255.255.255")]   // 224/4 multicast end
    [InlineData("240.0.0.1")]         // 240/4 reserved / class-E
    [InlineData("255.255.255.255")]   // limited broadcast (within 240/4)
    // ── IPv4-mapped IPv6 must be unwrapped and blocked ──
    [InlineData("::ffff:10.0.0.1")]   // mapped private
    [InlineData("::ffff:169.254.169.254")] // mapped metadata
    // ── IPv6 ──
    [InlineData("::")]                // unspecified
    [InlineData("::1")]               // loopback
    [InlineData("::7f00:1")]          // ::/96 IPv4-compatible (deprecated)
    [InlineData("64:ff9b::7f00:1")]   // NAT64 well-known prefix -> 127.0.0.1
    [InlineData("2001::1")]           // Teredo 2001:0000::/32
    [InlineData("2001:db8::1")]       // documentation 2001:db8::/32
    [InlineData("2002::1")]           // 6to4 2002::/16
    // ── Tunnelled IPv4 (#3495 Slice F, H3): blocked whether the EMBEDDED v4 is private or public.
    // The DoD asked to decode the embedded address and recurse into the v4 rules, which would ADMIT
    // the public-embedded cases below. We keep the wholesale block instead — 6to4/Teredo are
    // deprecated and no sink needs NAT64 egress, so decode-and-allow would only widen the surface.
    // These rows are the contract for that decision: if someone implements the decode, they fail.
    [InlineData("64:ff9b::a00:1")]    // NAT64 -> 10.0.0.1 (private embedded)
    [InlineData("64:ff9b::808:808")]  // NAT64 -> 8.8.8.8  (PUBLIC embedded, still blocked)
    [InlineData("2002:0a00:0001::1")] // 6to4 -> 10.0.0.1  (private embedded)
    [InlineData("2002:0808:0808::1")] // 6to4 -> 8.8.8.8   (PUBLIC embedded, still blocked)
    [InlineData("2001:0:4136:e378::1")] // Teredo with a public server v4, still blocked
    [InlineData("fc00::1")]           // ULA fc00::/8
    [InlineData("fd00::1")]           // ULA fd00::/8
    [InlineData("fe80::1")]           // link-local
    [InlineData("fec0::1")]           // site-local (deprecated)
    [InlineData("ff02::1")]           // multicast
    public void IsBlocked_ReservedOrPrivate_ReturnsTrue(string ipStr)
    {
        SsrfPolicy.IsBlocked(IPAddress.Parse(ipStr)).Should().BeTrue($"{ipStr} is reserved/private");
    }

    [Theory]
    // ── genuine public IPs ──
    [InlineData("8.8.8.8")]           // Google DNS
    [InlineData("1.1.1.1")]           // Cloudflare DNS
    [InlineData("104.18.32.7")]       // Cloudflare CDN
    [InlineData("2001:4860:4860::8888")] // Google public IPv6 (NOT Teredo/doc/6to4)
    // ── adjacency: just outside each blocked range ──
    [InlineData("100.63.255.255")]    // just below 100.64/10
    [InlineData("100.128.0.0")]       // just above 100.64/10
    [InlineData("172.15.255.255")]    // just below 172.16/12
    [InlineData("172.32.0.0")]        // just above 172.16/12
    [InlineData("192.167.0.1")]       // not 192.168/16
    [InlineData("192.0.1.0")]         // not 192.0.0/24
    [InlineData("198.17.255.255")]    // just below 198.18/15
    [InlineData("198.20.0.0")]        // just above 198.18/15
    [InlineData("223.255.255.255")]   // just below 224/4 multicast
    public void IsBlocked_PublicOrAdjacent_ReturnsFalse(string ipStr)
    {
        SsrfPolicy.IsBlocked(IPAddress.Parse(ipStr)).Should().BeFalse($"{ipStr} is public");
    }
}
