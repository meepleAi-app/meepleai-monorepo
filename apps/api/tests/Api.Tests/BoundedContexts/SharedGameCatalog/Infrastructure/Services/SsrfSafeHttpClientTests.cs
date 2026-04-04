using System.Net;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure.Services;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class SsrfSafeHttpClientTests
{
    #region ValidateUrlScheme Tests

    [Theory]
    [InlineData("https://example.com/file.pdf")]
    [InlineData("https://cdn.boardgamegeek.com/rules/game.pdf")]
    public void ValidateUrlScheme_ValidHttpsUrl_ShouldNotThrow(string url)
    {
        var act = () => SsrfSafeHttpClient.ValidateUrlScheme(url);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("http://example.com/file.pdf")]
    [InlineData("http://localhost/file.pdf")]
    public void ValidateUrlScheme_HttpUrl_ShouldThrowArgumentException(string url)
    {
        var act = () => SsrfSafeHttpClient.ValidateUrlScheme(url);

        act.Should().Throw<ArgumentException>()
            .WithMessage("Only HTTPS URLs are allowed*");
    }

    [Theory]
    [InlineData("ftp://example.com/file.pdf")]
    [InlineData("file:///etc/passwd")]
    public void ValidateUrlScheme_NonHttpScheme_ShouldThrowArgumentException(string url)
    {
        var act = () => SsrfSafeHttpClient.ValidateUrlScheme(url);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("")]
    public void ValidateUrlScheme_InvalidUrl_ShouldThrowArgumentException(string url)
    {
        var act = () => SsrfSafeHttpClient.ValidateUrlScheme(url);

        act.Should().Throw<ArgumentException>()
            .WithMessage("Invalid URL*");
    }

    #endregion

    #region IsPrivateOrReserved Tests

    [Theory]
    [InlineData("127.0.0.1")]       // Loopback
    [InlineData("127.0.0.2")]       // Loopback range
    [InlineData("10.0.0.1")]        // 10.0.0.0/8
    [InlineData("10.255.255.255")]  // 10.0.0.0/8 end
    [InlineData("172.16.0.1")]      // 172.16.0.0/12 start
    [InlineData("172.31.255.255")]  // 172.16.0.0/12 end
    [InlineData("192.168.0.1")]     // 192.168.0.0/16
    [InlineData("192.168.255.255")] // 192.168.0.0/16 end
    [InlineData("169.254.0.1")]     // Link-local / AWS metadata
    [InlineData("169.254.169.254")] // AWS metadata endpoint
    [InlineData("0.0.0.0")]         // 0.0.0.0/8
    public void IsPrivateOrReserved_PrivateIp_ShouldReturnTrue(string ipStr)
    {
        var ip = IPAddress.Parse(ipStr);

        SsrfSafeHttpClient.IsPrivateOrReserved(ip).Should().BeTrue();
    }

    [Theory]
    [InlineData("8.8.8.8")]         // Google DNS
    [InlineData("1.1.1.1")]         // Cloudflare DNS
    [InlineData("104.18.32.7")]     // Public IP
    [InlineData("172.15.255.255")]  // Just outside 172.16.0.0/12
    [InlineData("172.32.0.0")]      // Just outside 172.16.0.0/12
    [InlineData("192.167.0.1")]     // Not 192.168.x.x
    public void IsPrivateOrReserved_PublicIp_ShouldReturnFalse(string ipStr)
    {
        var ip = IPAddress.Parse(ipStr);

        SsrfSafeHttpClient.IsPrivateOrReserved(ip).Should().BeFalse();
    }

    [Fact]
    public void IsPrivateOrReserved_IPv6Loopback_ShouldReturnTrue()
    {
        SsrfSafeHttpClient.IsPrivateOrReserved(IPAddress.IPv6Loopback).Should().BeTrue();
    }

    [Fact]
    public void IsPrivateOrReserved_IPv6LinkLocal_ShouldReturnTrue()
    {
        var ip = IPAddress.Parse("fe80::1");

        SsrfSafeHttpClient.IsPrivateOrReserved(ip).Should().BeTrue();
    }

    #endregion
}
