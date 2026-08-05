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

    // IsPrivateOrReserved tests removed with the method (#3495 fix 5/N): the pre-connect DNS
    // check was TOCTOU and is retired in favor of the connect-pin (SsrfPinnedConnect), whose
    // IANA IP policy is covered by SsrfPolicyTests. The IP boundary is no longer this class's.

    #region Redirect / scheme / size-cap tests (#3495 fix 5/N)

    private static HttpResponseMessage Redirect(HttpStatusCode code, string location)
    {
        var r = new HttpResponseMessage(code);
        r.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);
        return r;
    }

    private static HttpResponseMessage Payload() => new(HttpStatusCode.OK)
    {
        Content = new ByteArrayContent(System.Text.Encoding.ASCII.GetBytes("image-bytes")),
    };

    [Fact]
    public async Task DownloadImageAsync_FollowsHttpsRedirect_ToFinalPayload()
    {
        using var handler = new SequenceHttpMessageHandler(
            _ => Redirect(HttpStatusCode.Found, "https://cdn.example/final.png"),
            _ => Payload());
        using var httpClient = new HttpClient(handler);
        var sut = new SsrfSafeHttpClient(httpClient);

        var result = await sut.DownloadImageAsync("https://example.com/cover.png", CancellationToken.None);

        System.Text.Encoding.ASCII.GetString(result).Should().Be("image-bytes");
        handler.RequestedUris.Should().HaveCount(2);
        handler.RequestedUris[1].Should().Be(new Uri("https://cdn.example/final.png"));
    }

    [Theory]
    [InlineData("http://cdn.example/final.png")]     // https → http downgrade
    [InlineData("file:///etc/passwd")]                // scheme downgrade to file
    [InlineData("gopher://internal/x")]               // exotic scheme
    public async Task DownloadImageAsync_RejectsSchemeDowngradeOnRedirect(string location)
    {
        using var handler = new SequenceHttpMessageHandler(_ => Redirect(HttpStatusCode.Found, location));
        using var httpClient = new HttpClient(handler);
        var sut = new SsrfSafeHttpClient(httpClient);

        var act = () => sut.DownloadImageAsync("https://example.com/cover.png", CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Only HTTPS URLs are allowed*");
    }

    [Fact]
    public async Task DownloadImageAsync_RejectsRedirectLoop()
    {
        using var handler = new SequenceHttpMessageHandler(
            _ => Redirect(HttpStatusCode.Found, "https://example.com/cover.png")); // → itself
        using var httpClient = new HttpClient(handler);
        var sut = new SsrfSafeHttpClient(httpClient);

        var act = () => sut.DownloadImageAsync("https://example.com/cover.png", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*loop*");
    }

    [Fact]
    public async Task DownloadImageAsync_RejectsTooManyRedirects()
    {
        var n = 0;
        using var handler = new SequenceHttpMessageHandler(
            _ => Redirect(HttpStatusCode.Found, $"https://example.com/hop{++n}.png"));
        using var httpClient = new HttpClient(handler);
        var sut = new SsrfSafeHttpClient(httpClient);

        var act = () => sut.DownloadImageAsync("https://example.com/cover.png", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*maximum*redirects*");
    }

    [Fact]
    public async Task FetchWithLimitAsync_AbortsMidStreamOverCeiling()
    {
        // 2 KB body against a 1 KB ceiling → must throw before buffering the whole payload.
        var body = new byte[2048];
        using var handler = new SequenceHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(body),
        });
        using var httpClient = new HttpClient(handler);
        var sut = new SsrfSafeHttpClient(httpClient);

        var act = () => sut.FetchWithLimitAsync("https://example.com/blob", 1024, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*exceeds*");
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

    #endregion

    #region DownloadImageAsync Disposal Tests (Issue #3239)

    [Fact]
    public async Task DownloadImageAsync_DisposesResponseContentStream()
    {
        var payload = System.Text.Encoding.ASCII.GetBytes("fake-image-content");
        var sourceStream = new DisposeTrackingStream(payload);
        using var handler = new StubHttpMessageHandler(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(sourceStream),
        });
        using var httpClient = new HttpClient(handler);
        var sut = new SsrfSafeHttpClient(httpClient);

        // The stub handler answers directly, so no connection (and no SSRF pin) is involved: this
        // test is only about the response lifetime.
        var result = await sut.DownloadImageAsync("https://8.8.8.8/cover.png", CancellationToken.None);

        // The full payload is returned to the caller as an independent buffer...
        System.Text.Encoding.ASCII.GetString(result).Should().Be("fake-image-content");

        // ...and the source HttpResponseMessage content stream must be disposed (it was leaked before the fix).
        sourceStream.Disposed.Should().BeTrue(
            "the hardened fetch must dispose the HttpResponseMessage and its content stream");
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpResponseMessage> _responder;

        public StubHttpMessageHandler(Func<HttpResponseMessage> responder) => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(_responder());
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

    #endregion
}
