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
/// Issue #3495 findings H8/C3 (Slice E) — the BGG cover sink carries its OWN circuit breaker, so a
/// failing cover CDN stops being dialled instead of costing every caller a full connect budget.
/// <para>
/// The test drives the real DI registration and counts DNS resolutions: the pin resolves once per
/// dial attempt, so "resolutions stop increasing while calls keep coming" is direct evidence the
/// breaker short-circuits BEFORE the connect. Failures are produced by the pin itself (a host that
/// resolves to a reserved address), which keeps the test offline and deterministic.
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "3495")]
public sealed class BggCoverDownloaderResilienceTests
{
    /// <summary>Matches the failureThreshold configured on the cover client's resilience handler.</summary>
    private const int BreakerThreshold = 3;

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
    public async Task CoverSink_OpensItsCircuit_AndStopsDialling_AfterConsecutiveFailures()
    {
        var dns = new StubDnsResolver("169.254.169.254");
        var pipeline = new Mock<IBggCoverUploadPipeline>();
        var services = new ServiceCollection();
        services.AddSingleton<IDnsResolver>(dns);
        services.AddSingleton(pipeline.Object);
        SharedGameCatalogServiceExtensions.AddBggCoverDownloaderForTests(services);
        using var provider = services.BuildServiceProvider();

        var downloader = provider.GetRequiredService<IBggCoverDownloader>();

        for (var call = 0; call < BreakerThreshold; call++)
        {
            var result = await downloader.DownloadAndUploadAsync(
                13, "https://cf.geekdo-images.com/cover.jpg", CancellationToken.None);
            result.Should().BeNull("the SSRF pin blocks every one of these dials");
        }

        dns.ResolveCalls.Should().Be(BreakerThreshold, "each pre-breaker call dials once through the pin");

        // The circuit is open now: further calls must be rejected without another dial.
        var afterBreak = await downloader.DownloadAndUploadAsync(
            13, "https://cf.geekdo-images.com/cover.jpg", CancellationToken.None);

        afterBreak.Should().BeNull("a rejected call still degrades gracefully to 'no cover'");
        dns.ResolveCalls.Should().Be(
            BreakerThreshold,
            "the open circuit must short-circuit before the connect-pin resolves anything again");
        pipeline.Verify(
            p => p.UploadAsync(It.IsAny<int>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
