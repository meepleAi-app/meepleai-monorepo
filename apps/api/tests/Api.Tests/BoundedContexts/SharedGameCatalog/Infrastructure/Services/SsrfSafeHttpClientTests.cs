using System.Net;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
using Api.SharedKernel.Infrastructure.Http;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure.Services;

/// <summary>
/// The manual / arbitrary-URL cover sink. Since #3495 Slice D the redirect, scheme, port, deadline
/// and ceiling logic lives in <see cref="HardenedRedirectFetch"/> (covered exhaustively by
/// <c>HardenedRedirectFetchTests</c>); what stays specific to this type — and is asserted here — is
/// the binding: the manual sink's own client, its 10MB image ceiling, and the guarded behaviour
/// surviving end-to-end through the wrapper.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class SsrfSafeHttpClientTests
{
    private static HttpResponseMessage Redirect(HttpStatusCode code, string location)
    {
        var response = new HttpResponseMessage(code);
        response.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);
        return response;
    }

    [Fact]
    public async Task DownloadImageAsync_FollowsHttpsRedirect_ToFinalPayload()
    {
        var payload = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        using var handler = new SequenceHttpMessageHandler(
            _ => Redirect(HttpStatusCode.Found, "https://cdn.example/final.png"),
            _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) });
        using var httpClient = new HttpClient(handler);
        var sut = new SsrfSafeHttpClient(httpClient);

        var result = await sut.DownloadImageAsync("https://example.com/cover.png", CancellationToken.None);

        result.Should().Equal(payload);
        handler.RequestedUris.Should().HaveCount(2);
        handler.RequestedUris[1].Should().Be(new Uri("https://cdn.example/final.png"));
    }

    [Theory]
    [InlineData("http://cdn.example/final.png")]      // https → http downgrade
    [InlineData("file:///etc/passwd")]                 // scheme downgrade to file
    [InlineData("gopher://internal/x")]                // exotic scheme
    public async Task DownloadImageAsync_RejectsSchemeDowngradeOnRedirect(string location)
    {
        using var handler = new SequenceHttpMessageHandler(_ => Redirect(HttpStatusCode.Found, location));
        using var httpClient = new HttpClient(handler);
        var sut = new SsrfSafeHttpClient(httpClient);

        var act = () => sut.DownloadImageAsync("https://example.com/cover.png", CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<HardenedFetchException>();
        thrown.Which.Reason.Should().Be(HardenedFetchBlockReason.Scheme);
    }

    [Fact]
    public async Task DownloadImageAsync_RejectsARedirectToANonDefaultPort()
    {
        using var handler = new SequenceHttpMessageHandler(
            _ => Redirect(HttpStatusCode.Found, "https://cdn.example:8080/final.png"));
        using var httpClient = new HttpClient(handler);
        var sut = new SsrfSafeHttpClient(httpClient);

        var act = () => sut.DownloadImageAsync("https://example.com/cover.png", CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<HardenedFetchException>();
        thrown.Which.Reason.Should().Be(HardenedFetchBlockReason.Port);
    }

    [Fact]
    public async Task DownloadImageAsync_EnforcesTheTenMegabyteCoverCeiling()
    {
        // The ceiling is the wrapper's contract (the engine takes it as a parameter), so it belongs
        // here rather than in the engine tests: 11MB must never come back as bytes.
        using var handler = new SequenceHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[11 * 1024 * 1024]),
        });
        using var httpClient = new HttpClient(handler);
        var sut = new SsrfSafeHttpClient(httpClient);

        var act = () => sut.DownloadImageAsync("https://example.com/huge.png", CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<HardenedFetchException>();
        thrown.Which.Reason.Should().Be(HardenedFetchBlockReason.SizeCap);
    }

    [Fact]
    public async Task DownloadImageAsync_DisposesResponseContentStream()
    {
        // Issue #3239 regression: the response content stream used to leak.
        var payload = System.Text.Encoding.ASCII.GetBytes("fake-image-content");
        var sourceStream = new DisposeTrackingStream(payload);
        using var handler = new SequenceHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(sourceStream),
        });
        using var httpClient = new HttpClient(handler);
        var sut = new SsrfSafeHttpClient(httpClient);

        var result = await sut.DownloadImageAsync("https://8.8.8.8/cover.png", CancellationToken.None);

        System.Text.Encoding.ASCII.GetString(result).Should().Be("fake-image-content");
        sourceStream.Disposed.Should().BeTrue(
            "the hardened fetch must dispose the HttpResponseMessage and its content stream");
    }

    private sealed class SequenceHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responders;
        public List<Uri> RequestedUris { get; } = new();

        public SequenceHttpMessageHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] responders)
            => _responders = new Queue<Func<HttpRequestMessage, HttpResponseMessage>>(responders);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestedUris.Add(request.RequestUri!);
            // Reuse the last responder once the queue is down to one (steady-state redirect/payload).
            var responder = _responders.Count > 1 ? _responders.Dequeue() : _responders.Peek();
            return Task.FromResult(responder(request));
        }
    }

    private sealed class DisposeTrackingStream : MemoryStream
    {
        public DisposeTrackingStream(byte[] buffer) : base(buffer, writable: false)
        {
        }

        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }

        public override ValueTask DisposeAsync()
        {
            Disposed = true;
            return base.DisposeAsync();
        }
    }
}
