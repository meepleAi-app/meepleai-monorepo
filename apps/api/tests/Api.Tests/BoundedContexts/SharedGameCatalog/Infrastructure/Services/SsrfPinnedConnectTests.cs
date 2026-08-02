using System.Net;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure.Services;

/// <summary>
/// The SSRF connect pin's security decision (issue #3495, findings C1/H4/M4/M5). Resolve
/// exactly once, fail closed if ANY resolved address is blocked or if none resolve, and pin
/// the validated public address — so the socket dials the validated IP with no second
/// resolution (DNS-rebinding closed by construction).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class SsrfPinnedConnectTests
{
    private sealed class FakeDnsResolver : IDnsResolver
    {
        private readonly IReadOnlyList<IPAddress> _addresses;
        public int ResolveCalls { get; private set; }

        public FakeDnsResolver(params string[] ips)
            => _addresses = ips.Select(IPAddress.Parse).ToArray();

        public Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken ct)
        {
            ResolveCalls++;
            return Task.FromResult(_addresses);
        }
    }

    [Fact]
    public async Task ResolveAndValidate_PublicHost_PinsFirstAddress_ResolvingExactlyOnce()
    {
        var dns = new FakeDnsResolver("8.8.8.8");

        var pinned = await SsrfPinnedConnect.ResolveAndValidateAsync(dns, "cdn.example.com", CancellationToken.None);

        pinned.Should().Be(IPAddress.Parse("8.8.8.8"));
        dns.ResolveCalls.Should().Be(1, "the pin must not re-resolve after validation");
    }

    [Fact]
    public async Task ResolveAndValidate_PrivateAddress_FailsClosed()
    {
        var dns = new FakeDnsResolver("10.0.0.5");

        var act = () => SsrfPinnedConnect.ResolveAndValidateAsync(dns, "evil.example.com", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*blocked*");
    }

    [Fact]
    public async Task ResolveAndValidate_MetadataAddress_FailsClosed()
    {
        var dns = new FakeDnsResolver("169.254.169.254");

        var act = () => SsrfPinnedConnect.ResolveAndValidateAsync(dns, "meta.example.com", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ResolveAndValidate_MixedPublicThenPrivate_FailsClosed()
    {
        // Rebinding-style multi-record: a single lookup returning both a public and a private
        // address must be rejected (never pin the public one and ignore the private).
        var dns = new FakeDnsResolver("8.8.8.8", "169.254.169.254");

        var act = () => SsrfPinnedConnect.ResolveAndValidateAsync(dns, "rebind.example.com", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ResolveAndValidate_EmptyResolution_FailsClosed()
    {
        var dns = new FakeDnsResolver();

        var act = () => SsrfPinnedConnect.ResolveAndValidateAsync(dns, "void.example.com", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*resolve*");
    }
}
