using Api.SharedKernel.Infrastructure.Http;
using FluentAssertions;
using Xunit;

namespace Api.Tests.SharedKernel.Infrastructure.Http;

/// <summary>
/// #3495 C6 — server-side ADR-059 §5 / #2123 BGG asset ban on the manual-cover arbitrary-URL path.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedKernel")]
public sealed class BggHostDenyListTests
{
    [Theory]
    [InlineData("cf.geekdo-images.com")]
    [InlineData("geekdo-images.com")]
    [InlineData("images.geekdo.com")]
    [InlineData("geekdo.com")]
    [InlineData("boardgamegeek.com")]
    [InlineData("www.boardgamegeek.com")]
    [InlineData("api.boardgamegeek.com")]
    [InlineData("GEEKDO-IMAGES.COM")]           // case-insensitive
    [InlineData("cf.geekdo-images.com.")]        // trailing-dot FQDN
    public void IsBannedHost_BannedHosts_ReturnsTrue(string host)
    {
        BggHostDenyList.IsBannedHost(host).Should().BeTrue();
    }

    [Theory]
    [InlineData("commons.wikimedia.org")]
    [InlineData("upload.wikimedia.org")]
    [InlineData("example.com")]
    [InlineData("evilgeekdo.com")]               // substring, not a sub-domain boundary
    [InlineData("evilgeekdo-images.com")]        // the FE substring regex would false-match; we don't
    [InlineData("geekdo.com.attacker.example")]  // banned label not at the suffix boundary
    [InlineData("")]
    [InlineData(null)]
    public void IsBannedHost_NonBggHosts_ReturnsFalse(string? host)
    {
        BggHostDenyList.IsBannedHost(host).Should().BeFalse();
    }

    [Fact]
    public void IsBanned_ParsesAbsoluteUrl_AndMatchesHost()
    {
        BggHostDenyList.IsBanned("https://cf.geekdo-images.com/x/cover.jpg").Should().BeTrue();
        BggHostDenyList.IsBanned("https://commons.wikimedia.org/x.jpg").Should().BeFalse();
        BggHostDenyList.IsBanned("not-a-url").Should().BeFalse();
        BggHostDenyList.IsBanned(null).Should().BeFalse();
    }
}
