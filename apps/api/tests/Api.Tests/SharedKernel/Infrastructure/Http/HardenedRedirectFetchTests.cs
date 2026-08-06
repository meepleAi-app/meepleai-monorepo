using System.Diagnostics;
using System.Net;
using Api.SharedKernel.Infrastructure.Http;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.SharedKernel.Infrastructure.Http;

/// <summary>
/// Issue #3495 Slice D — the hardened redirect engine shared by every arbitrary-URL / redirecting
/// egress sink (findings C4 wall-clock deadline, H2 per-hop scheme + port re-validation).
/// <para>
/// The connect-pin owns the IP boundary; this engine owns everything the pin cannot express:
/// a bounded manual redirect follow, per-hop scheme/port re-validation, a streamed byte ceiling,
/// and a TOTAL wall-clock budget across all hops (a chain of individually-fast hops must not be
/// able to hold a request open indefinitely).
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedKernel")]
[Trait("Issue", "3495")]
public sealed class HardenedRedirectFetchTests
{
    private const string Sink = "test_sink";

    private static HttpResponseMessage Redirect(string location) =>
        new(HttpStatusCode.Found) { Headers = { Location = new Uri(location, UriKind.RelativeOrAbsolute) } };

    private static HttpResponseMessage Ok(byte[] body) =>
        new(HttpStatusCode.OK) { Content = new ByteArrayContent(body) };

    // ------------------------------------------------------------------
    // C4 — total wall-clock deadline
    // ------------------------------------------------------------------

    [Fact]
    public async Task Deadline_IsCumulativeAcrossHops_NotPerHop()
    {
        // Each hop is comfortably inside the budget on its own; together they exceed it. A per-hop
        // timeout would let this chain run forever — the deadline must be wall-clock TOTAL.
        var hop = 0;
        using var handler = new DelegateHandler(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(120), ct);
            return Redirect($"https://example.com/hop{++hop}");
        });
        using var client = new HttpClient(handler);

        var act = () => HardenedRedirectFetch.FetchAsync(
            client, "https://example.com/start", maxBytes: 1024, sink: Sink,
            deadline: TimeSpan.FromMilliseconds(250), configureRequest: null, ct: CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<HardenedFetchException>();
        thrown.Which.Reason.Should().Be(HardenedFetchBlockReason.Timeout);
        hop.Should().BeLessThan(5, "the deadline must cut the chain short before the hop cap");
    }

    [Fact]
    public async Task Deadline_AbortsASlowBody_AndFailsClosed()
    {
        using var handler = new DelegateHandler(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
            return Ok(new byte[] { 1 });
        });
        using var client = new HttpClient(handler);
        var stopwatch = Stopwatch.StartNew();

        var act = () => HardenedRedirectFetch.FetchAsync(
            client, "https://example.com/slow", maxBytes: 1024, sink: Sink,
            deadline: TimeSpan.FromMilliseconds(200), configureRequest: null, ct: CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<HardenedFetchException>();
        thrown.Which.Reason.Should().Be(HardenedFetchBlockReason.Timeout);
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10), "the deadline must fire, not the client timeout");
    }

    [Fact]
    public async Task CallerCancellation_PropagatesAsOperationCanceled_NotAsTimeout()
    {
        // A caller cancel and an expired deadline both surface as a cancelled token inside the
        // engine; they MUST stay distinguishable, or a batch handler mistakes a per-item budget for
        // a user abort (the #2157 lesson on the Commons client).
        using var cts = new CancellationTokenSource();
        using var handler = new DelegateHandler(async (_, ct) =>
        {
            await cts.CancelAsync();
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            return Ok(new byte[] { 1 });
        });
        using var client = new HttpClient(handler);

        var act = () => HardenedRedirectFetch.FetchAsync(
            client, "https://example.com/x", maxBytes: 1024, sink: Sink,
            deadline: TimeSpan.FromSeconds(30), configureRequest: null, ct: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ------------------------------------------------------------------
    // H2 — per-hop scheme and port re-validation
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("https://example.com:8080/x")]   // app-server port
    [InlineData("https://example.com:22/x")]     // ssh
    [InlineData("https://example.com:6379/x")]   // redis
    public async Task RedirectToAnomalousPort_IsBlocked(string location)
    {
        using var handler = new DelegateHandler((_, _) => Task.FromResult(Redirect(location)));
        using var client = new HttpClient(handler);

        var act = () => HardenedRedirectFetch.FetchAsync(
            client, "https://example.com/start", maxBytes: 1024, sink: Sink,
            deadline: TimeSpan.FromSeconds(5), configureRequest: null, ct: CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<HardenedFetchException>();
        thrown.Which.Reason.Should().Be(HardenedFetchBlockReason.Port);
    }

    [Fact]
    public async Task ExplicitDefaultHttpsPort_IsAllowed()
    {
        using var handler = new DelegateHandler(request =>
            request.RequestUri!.AbsoluteUri.Contains("cdn", StringComparison.Ordinal)
                ? Ok(new byte[] { 7 })
                : Redirect("https://cdn.example.com:443/final"));
        using var client = new HttpClient(handler);

        var result = await HardenedRedirectFetch.FetchAsync(
            client, "https://example.com/start", maxBytes: 1024, sink: Sink,
            deadline: TimeSpan.FromSeconds(5), configureRequest: null, ct: CancellationToken.None);

        result.Should().Equal(new byte[] { 7 });
    }

    [Fact]
    public async Task InitialUrlWithAnomalousPort_IsBlockedBeforeAnyRequest()
    {
        var requests = 0;
        using var handler = new DelegateHandler((_, _) =>
        {
            requests++;
            return Task.FromResult(Ok(new byte[] { 1 }));
        });
        using var client = new HttpClient(handler);

        var act = () => HardenedRedirectFetch.FetchAsync(
            client, "https://example.com:9000/x", maxBytes: 1024, sink: Sink,
            deadline: TimeSpan.FromSeconds(5), configureRequest: null, ct: CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<HardenedFetchException>();
        thrown.Which.Reason.Should().Be(HardenedFetchBlockReason.Port);
        requests.Should().Be(0, "the port gate must reject before dialling");
    }

    [Theory]
    [InlineData("http://example.com/x")]
    [InlineData("file:///etc/passwd")]
    [InlineData("gopher://internal/x")]
    public async Task RedirectToNonHttpsScheme_IsBlocked(string location)
    {
        using var handler = new DelegateHandler((_, _) => Task.FromResult(Redirect(location)));
        using var client = new HttpClient(handler);

        var act = () => HardenedRedirectFetch.FetchAsync(
            client, "https://example.com/start", maxBytes: 1024, sink: Sink,
            deadline: TimeSpan.FromSeconds(5), configureRequest: null, ct: CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<HardenedFetchException>();
        thrown.Which.Reason.Should().Be(HardenedFetchBlockReason.Scheme);
    }

    // ------------------------------------------------------------------
    // Redirect bounds, relative resolution, size ceiling
    // ------------------------------------------------------------------

    [Fact]
    public async Task RelativeLocation_ResolvesAgainstTheCurrentHop()
    {
        var seen = new List<Uri>();
        using var handler = new DelegateHandler(request =>
        {
            seen.Add(request.RequestUri!);
            return seen.Count == 1 ? Redirect("/images/final.png") : Ok(new byte[] { 42 });
        });
        using var client = new HttpClient(handler);

        var result = await HardenedRedirectFetch.FetchAsync(
            client, "https://cdn.example.com/a/b/start", maxBytes: 1024, sink: Sink,
            deadline: TimeSpan.FromSeconds(5), configureRequest: null, ct: CancellationToken.None);

        result.Should().Equal(new byte[] { 42 });
        seen[1].Should().Be(new Uri("https://cdn.example.com/images/final.png"));
    }

    [Fact]
    public async Task RedirectLoop_IsBlocked()
    {
        using var handler = new DelegateHandler((_, _) => Task.FromResult(Redirect("https://example.com/start")));
        using var client = new HttpClient(handler);

        var act = () => HardenedRedirectFetch.FetchAsync(
            client, "https://example.com/start", maxBytes: 1024, sink: Sink,
            deadline: TimeSpan.FromSeconds(5), configureRequest: null, ct: CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<HardenedFetchException>();
        thrown.Which.Reason.Should().Be(HardenedFetchBlockReason.RedirectExhausted);
    }

    [Fact]
    public async Task TooManyHops_AreBlocked()
    {
        var hop = 0;
        using var handler = new DelegateHandler((_, _) => Task.FromResult(Redirect($"https://example.com/hop{++hop}")));
        using var client = new HttpClient(handler);

        var act = () => HardenedRedirectFetch.FetchAsync(
            client, "https://example.com/start", maxBytes: 1024, sink: Sink,
            deadline: TimeSpan.FromSeconds(5), configureRequest: null, ct: CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<HardenedFetchException>();
        thrown.Which.Reason.Should().Be(HardenedFetchBlockReason.RedirectExhausted);
    }

    [Fact]
    public async Task BodyOverCeiling_IsAbortedMidStream()
    {
        using var handler = new DelegateHandler((_, _) => Task.FromResult(Ok(new byte[2048])));
        using var client = new HttpClient(handler);

        var act = () => HardenedRedirectFetch.FetchAsync(
            client, "https://example.com/blob", maxBytes: 1024, sink: Sink,
            deadline: TimeSpan.FromSeconds(5), configureRequest: null, ct: CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<HardenedFetchException>();
        thrown.Which.Reason.Should().Be(HardenedFetchBlockReason.SizeCap);
    }

    [Fact]
    public async Task RelativeRequestUri_ResolvesAgainstTheClientBaseAddress()
    {
        // Typed clients (Commons) issue relative paths against their BaseAddress; the engine must
        // honour that instead of forcing every caller to build absolute URLs.
        var seen = new List<Uri>();
        using var handler = new DelegateHandler(request =>
        {
            seen.Add(request.RequestUri!);
            return Ok(new byte[] { 9 });
        });
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://commons.wikimedia.org/") };

        var result = await HardenedRedirectFetch.FetchAsync(
            client, "wiki/Special:FilePath/Cover.jpg", maxBytes: 1024, sink: Sink,
            deadline: TimeSpan.FromSeconds(5), configureRequest: null, ct: CancellationToken.None);

        result.Should().Equal(new byte[] { 9 });
        seen[0].Should().Be(new Uri("https://commons.wikimedia.org/wiki/Special:FilePath/Cover.jpg"));
    }

    [Theory]
    [InlineData("gzip")]
    [InlineData("br")]
    [InlineData("deflate")]
    public async Task ContentEncodedResponses_AreRefused(string encoding)
    {
        // #3495 C5/L3: our handlers do not auto-decompress, so the ceiling would be counting the
        // COMPRESSED bytes — a small response could still expand into a huge allocation downstream.
        // Refusing the encoding keeps maxBytes a bound on what the caller actually handles.
        using var handler = new DelegateHandler(_ =>
        {
            var response = Ok(new byte[] { 1, 2, 3 });
            response.Content.Headers.ContentEncoding.Add(encoding);
            return response;
        });
        using var client = new HttpClient(handler);

        var act = () => HardenedRedirectFetch.FetchAsync(
            client, "https://example.com/blob", maxBytes: 1024, sink: Sink,
            deadline: TimeSpan.FromSeconds(5), configureRequest: null, ct: CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<HardenedFetchException>();
        thrown.Which.Reason.Should().Be(HardenedFetchBlockReason.ContentEncoding);
    }

    [Fact]
    public async Task AnIdentityResponse_IsStillAccepted()
    {
        using var handler = new DelegateHandler(_ => Ok(new byte[] { 5, 6 }));
        using var client = new HttpClient(handler);

        var result = await HardenedRedirectFetch.FetchAsync(
            client, "https://example.com/blob", maxBytes: 1024, sink: Sink,
            deadline: TimeSpan.FromSeconds(5), configureRequest: null, ct: CancellationToken.None);

        result.Should().Equal(new byte[] { 5, 6 });
    }

    [Fact]
    public async Task RelativePathOnANonHttpsBaseAddress_IsDialled_ButItsRedirectsAreStillGated()
    {
        // A relative path is OUR configuration (a typed client's BaseAddress), so the first hop is
        // dialled as configured — this is what keeps fixed-host clients working against a local
        // contract server. The redirect target is untrusted input all the same and stays gated.
        var seen = new List<Uri>();
        using var handler = new DelegateHandler(request =>
        {
            seen.Add(request.RequestUri!);
            return seen.Count == 1 ? Redirect("http://localhost:1234/next") : Ok(new byte[] { 1 });
        });
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:1234/") };

        var act = () => HardenedRedirectFetch.FetchAsync(
            client, "probe", maxBytes: 1024, sink: Sink,
            deadline: TimeSpan.FromSeconds(5), configureRequest: null, ct: CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<HardenedFetchException>();
        thrown.Which.Reason.Should().Be(HardenedFetchBlockReason.Scheme);
        seen.Should().ContainSingle("the configured first hop is dialled, its redirect is not");
    }

    [Fact]
    public async Task AbsoluteNonHttpsUrl_IsRefused_EvenWithABaseAddress()
    {
        // Caller input never inherits the base address's trust.
        var requests = 0;
        using var handler = new DelegateHandler((_, _) =>
        {
            requests++;
            return Task.FromResult(Ok(new byte[] { 1 }));
        });
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:1234/") };

        var act = () => HardenedRedirectFetch.FetchAsync(
            client, "http://localhost:1234/probe", maxBytes: 1024, sink: Sink,
            deadline: TimeSpan.FromSeconds(5), configureRequest: null, ct: CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<HardenedFetchException>();
        thrown.Which.Reason.Should().Be(HardenedFetchBlockReason.Scheme);
        requests.Should().Be(0);
    }

    [Fact]
    public async Task ConfigureRequest_AppliesToEveryHop()
    {
        var accepts = new List<string?>();
        using var handler = new DelegateHandler(request =>
        {
            accepts.Add(request.Headers.Accept.ToString());
            return accepts.Count == 1 ? Redirect("https://cdn.example.com/final") : Ok(new byte[] { 3 });
        });
        using var client = new HttpClient(handler);

        await HardenedRedirectFetch.FetchAsync(
            client, "https://example.com/start", maxBytes: 1024, sink: Sink,
            deadline: TimeSpan.FromSeconds(5),
            configureRequest: r => r.Headers.Accept.ParseAdd("image/*"),
            ct: CancellationToken.None);

        accepts.Should().HaveCount(2);
        accepts.Should().AllSatisfy(a => a.Should().Contain("image/*",
            "a redirected hop must carry the same headers as the first request"));
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
