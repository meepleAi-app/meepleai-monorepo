using Api.Services.Providers.Probe;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Services.Providers.Probe;

[Trait("Category", "Unit")]
public sealed class TokenFingerprintTests
{
    [Fact]
    public void Compute_KnownToken_ReturnsExpectedHash()
    {
        // SHA256("hello") = 2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824 → first 8 = "2cf24dba"
        TokenFingerprint.Compute("hello").Should().Be("2cf24dba");
    }

    [Fact]
    public void Compute_SameToken_Deterministic()
    {
        var a = TokenFingerprint.Compute("sk-or-v1-abcdef");
        var b = TokenFingerprint.Compute("sk-or-v1-abcdef");
        a.Should().Be(b);
    }

    [Fact]
    public void Compute_DifferentTokens_DifferentFingerprints()
    {
        TokenFingerprint.Compute("token-a").Should().NotBe(TokenFingerprint.Compute("token-b"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Compute_EmptyOrNull_ReturnsNull(string? token)
    {
        TokenFingerprint.Compute(token).Should().BeNull();
    }

    [Fact]
    public void Compute_ResultIsLowercaseHexLength8()
    {
        var fp = TokenFingerprint.Compute("any");
        fp.Should().MatchRegex("^[a-f0-9]{8}$");
    }
}
