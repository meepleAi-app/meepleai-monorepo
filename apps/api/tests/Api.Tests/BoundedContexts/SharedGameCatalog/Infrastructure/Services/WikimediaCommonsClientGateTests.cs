using System.Net;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure.Services;

/// <summary>
/// Issue #3495 Slice D (finding H1) — the Commons image download now follows the
/// <c>Special:FilePath</c> 302 MANUALLY through <c>HardenedRedirectFetch</c> instead of letting the
/// handler auto-follow it.
/// <para>
/// This supersedes the #1823 M8 invariant "do NOT disable redirects": the redirect is still
/// followed, so the image bytes stay reachable, but each hop is re-validated (HTTPS-only, default
/// port) and the body is bounded. Before this slice the download read the response with no ceiling
/// at all and a hostile 302 could point anywhere the pin still considered public.
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "3495")]
public sealed class WikimediaCommonsClientGateTests
{
    private const string CommonsBaseUrl = "https://commons.wikimedia.org/";

    private static WikimediaCommonsClient CreateClient(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri(CommonsBaseUrl) },
            new Mock<IWikimediaRateLimiter>().Object,
            new Mock<ILogger<WikimediaCommonsClient>>().Object);

    [Fact]
    public async Task FetchImageBytesAsync_FollowsTheSpecialFilePathRedirect_ToUploadWikimedia()
    {
        var expected = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x11 };
        var seen = new List<Uri>();
        using var handler = new DelegateHandler(request =>
        {
            seen.Add(request.RequestUri!);
            return seen.Count == 1
                ? new HttpResponseMessage(HttpStatusCode.Found)
                {
                    Headers = { Location = new Uri("https://upload.wikimedia.org/wikipedia/commons/a/b/Cover.jpg") },
                }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(expected) };
        });
        var client = CreateClient(handler);

        var result = await client.FetchImageBytesAsync("Cover.jpg", CancellationToken.None);

        result.Should().BeEquivalentTo(expected, "the M8 image path must keep working through the gate");
        seen.Should().HaveCount(2);
        seen[0].AbsolutePath.Should().StartWith("/wiki/Special:FilePath/");
        seen[1].Host.Should().Be("upload.wikimedia.org");
    }

    [Fact]
    public async Task FetchImageBytesAsync_RejectsARedirectThatDowngradesToHttp()
    {
        // Previously the handler auto-followed this before any of our code could see it.
        using var handler = new DelegateHandler(_ => new HttpResponseMessage(HttpStatusCode.Found)
        {
            Headers = { Location = new Uri("http://upload.wikimedia.org/plain.jpg") },
        });
        var client = CreateClient(handler);

        var result = await client.FetchImageBytesAsync("Cover.jpg", CancellationToken.None);

        result.Should().BeNull("a scheme downgrade mid-chain must fail closed, fail-soft to the caller");
    }

    [Fact]
    public async Task FetchImageBytesAsync_RejectsARedirectToANonDefaultPort()
    {
        using var handler = new DelegateHandler(_ => new HttpResponseMessage(HttpStatusCode.Found)
        {
            Headers = { Location = new Uri("https://upload.wikimedia.org:8080/internal.jpg") },
        });
        var client = CreateClient(handler);

        var result = await client.FetchImageBytesAsync("Cover.jpg", CancellationToken.None);

        result.Should().BeNull("a redirect to a non-default port is port-probing, not a CDN hop");
    }

    [Fact]
    public async Task FetchImageBytesAsync_AbortsAnOversizeBody()
    {
        // 33MB against the 32MB Commons ceiling: before Slice D there was no ceiling at all.
        using var handler = new DelegateHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[33 * 1024 * 1024]),
        });
        var client = CreateClient(handler);

        var result = await client.FetchImageBytesAsync("Huge.jpg", CancellationToken.None);

        result.Should().BeNull("an unbounded body is an unbounded allocation");
    }

    [Fact]
    public async Task FetchLicenseAsync_StillParsesA200Response_ThroughTheGate()
    {
        const string Body = """
            {"query":{"pages":{"123":{"imageinfo":[{"extmetadata":{
              "LicenseShortName":{"value":"CC BY-SA 4.0"},"Artist":{"value":"<a href='#'>Jane</a>"}}}]}}}}
            """;
        using var handler = new DelegateHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(Body),
        });
        var client = CreateClient(handler);

        var result = await client.FetchLicenseAsync("Cover.jpg", CancellationToken.None);

        result.RawLicense.Should().Be("CC BY-SA 4.0");
        result.Attribution.Should().Be("Jane");
    }

    [Fact]
    public async Task CallerCancellation_StillPropagates_AndIsNotSwallowedAsAGateBlock()
    {
        using var cts = new CancellationTokenSource();
        using var handler = new DelegateHandler(async (_, ct) =>
        {
            await cts.CancelAsync();
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var client = CreateClient(handler);

        var act = () => client.FetchImageBytesAsync("Cover.jpg", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>(
            "the #2157 contract keeps a batch-wide user abort distinguishable from a per-item failure");
    }

    private sealed class DelegateHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;

        public DelegateHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
            => _responder = responder;

        public DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
            => _responder = (request, _) => Task.FromResult(responder(request));

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => _responder(request, cancellationToken);
    }
}
