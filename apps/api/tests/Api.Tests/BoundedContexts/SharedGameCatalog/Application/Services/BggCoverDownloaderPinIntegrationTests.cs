using System.Net;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.DependencyInjection;
using Api.SharedKernel.Infrastructure.Http;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Proves the BGG cover-download egress is actually wired to the SSRF connect-pin (issue #3495 fix
/// 3/N): a cover host resolving to a private/reserved address fails closed at connect time (returns
/// null, never uploads) — WITHOUT the removed pre-connect DNS check. Resolving through the real DI
/// registration (<see cref="SharedGameCatalogServiceExtensions.AddBggCoverDownloaderForTests"/>)
/// guards against the pin being silently dropped; the pin's own fail-closed decision is covered by
/// SsrfPinnedConnectTests.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class BggCoverDownloaderPinIntegrationTests
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
    public async Task Bgg_CoverHostResolvingToPrivateIp_FailsClosedWithoutUploading()
    {
        var dns = new StubDnsResolver("169.254.169.254"); // cloud metadata endpoint
        var pipeline = new Mock<IBggCoverUploadPipeline>();
        var services = new ServiceCollection();
        // Registered BEFORE the seam so its TryAddSingleton<IDnsResolver> does not override this stub.
        services.AddSingleton<IDnsResolver>(dns);
        services.AddSingleton(pipeline.Object);
        SharedGameCatalogServiceExtensions.AddBggCoverDownloaderForTests(services);
        using var provider = services.BuildServiceProvider();

        var downloader = provider.GetRequiredService<IBggCoverDownloader>();

        // The host is a real BGG image CDN (so it clears the #3495 M3 allow-list), but the pinned
        // resolver returns the metadata IP, so the pin throws at connect and the download aborts
        // before any upload.
        var result = await downloader.DownloadAndUploadAsync(
            13, "https://cf.geekdo-images.com/cover.jpg", CancellationToken.None);

        result.Should().BeNull("the SSRF connect-pin must block a private-resolving cover host");
        pipeline.Verify(
            p => p.UploadAsync(It.IsAny<int>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        // The stub is consulted ONLY by the pin's ConnectCallback — a non-zero count proves
        // ConfigureSsrfPin is actually wired. Without the pin the default handler would do its own
        // DNS (8.8.8.8, public) and never touch this stub, so this assertion fails deterministically
        // if the pin is ever silently dropped from AddBggCoverDownloader.
        dns.ResolveCalls.Should().BeGreaterThan(0, "the connect-pin must consult the injected resolver");
    }
}
