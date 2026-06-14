using System.Diagnostics.Metrics;
using System.Net;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Providers;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure.Providers;

/// <summary>
/// Tests for <see cref="WikidataCatalogProvider.FetchCoverImageAsync(string, CancellationToken)"/>.
/// Issue #1823 Phase B M3 — Wikidata cover SPARQL fetch.
/// Spec: ADR DEC-3a (extend existing provider) + DEC-3e (shared 5 RPS rate-limiter) + DEC-3g (latency metric).
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class WikidataCatalogProviderCoverImageTests
{
    private static (HttpClient client, Mock<HttpMessageHandler> handler) MakeClient(
        HttpStatusCode status,
        string body)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/sparql-results+json"),
            });
        var client = new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("https://query.wikidata.org/"),
        };
        return (client, handler);
    }

    private static WikidataCatalogProvider MakeProvider(
        HttpClient client,
        IWikimediaRateLimiter? rateLimiter = null)
    {
        rateLimiter ??= Mock.Of<IWikimediaRateLimiter>();
        return new WikidataCatalogProvider(client, NullLogger<WikidataCatalogProvider>.Instance, rateLimiter);
    }

    private const string BodyWithImage = """
        { "results": { "bindings": [{
          "game":  {"value": "http://www.wikidata.org/entity/Q17215001"},
          "image": {"value": "http://commons.wikimedia.org/wiki/Special:FilePath/Playing%20board%20game%20-%20Play%201048%201724352635173.jpg"}
        }]}}
        """;

    private const string BodyNoImage = """
        { "results": { "bindings": [{
          "game":  {"value": "http://www.wikidata.org/entity/Q17215001"}
        }]}}
        """;

    private const string BodyEmpty = """{"results":{"bindings":[]}}""";

    [Fact]
    public async Task FetchCoverImageAsync_ValidQid_WithP18_ReturnsFilenameAndSourceUrl()
    {
        var (client, _) = MakeClient(HttpStatusCode.OK, BodyWithImage);
        var provider = MakeProvider(client);

        var result = await provider.FetchCoverImageAsync("Q17215001", CancellationToken.None);

        result.HasImage.Should().BeTrue();
        result.Filename.Should().Be("Playing%20board%20game%20-%20Play%201048%201724352635173.jpg");
        result.SourceUrl.Should().Be("https://www.wikidata.org/wiki/Q17215001");
    }

    [Fact]
    public async Task FetchCoverImageAsync_ValidQid_NoP18_ReturnsNotFound()
    {
        var (client, _) = MakeClient(HttpStatusCode.OK, BodyNoImage);
        var provider = MakeProvider(client);

        var result = await provider.FetchCoverImageAsync("Q17215001", CancellationToken.None);

        result.HasImage.Should().BeFalse();
        result.Filename.Should().BeNull();
        result.SourceUrl.Should().Be("https://www.wikidata.org/wiki/Q17215001");
    }

    [Fact]
    public async Task FetchCoverImageAsync_EmptyBindings_ReturnsNotFound()
    {
        var (client, _) = MakeClient(HttpStatusCode.OK, BodyEmpty);
        var provider = MakeProvider(client);

        var result = await provider.FetchCoverImageAsync("Q17215001", CancellationToken.None);

        result.HasImage.Should().BeFalse();
        result.Filename.Should().BeNull();
        result.SourceUrl.Should().Be("https://www.wikidata.org/wiki/Q17215001");
    }

    [Fact]
    public async Task FetchCoverImageAsync_InvalidQidFormat_ReturnsNotFoundWithoutHttpCall()
    {
        var (client, handler) = MakeClient(HttpStatusCode.OK, BodyWithImage);
        var provider = MakeProvider(client);

        var result = await provider.FetchCoverImageAsync("invalid", CancellationToken.None);

        result.HasImage.Should().BeFalse();
        result.Filename.Should().BeNull();
        result.SourceUrl.Should().Be("https://www.wikidata.org/wiki/invalid");

        // No HTTP round-trip when QID fails regex validation.
        handler.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task FetchCoverImageAsync_QidWithSparqlInjectionAttempt_ReturnsNotFoundWithoutHttpCall()
    {
        var (client, handler) = MakeClient(HttpStatusCode.OK, BodyWithImage);
        var provider = MakeProvider(client);

        // Injection attempt — must be rejected by regex.
        var result = await provider.FetchCoverImageAsync("Q123} . ?x ?y ?z", CancellationToken.None);

        result.HasImage.Should().BeFalse();
        handler.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task FetchCoverImageAsync_AcquiresRateLimiterBeforeHttpCall()
    {
        var (client, handler) = MakeClient(HttpStatusCode.OK, BodyWithImage);
        var rateLimiterMock = new Mock<IWikimediaRateLimiter>(MockBehavior.Strict);

        var rateLimiterAcquired = false;
        var httpCalledBeforeRateLimiter = false;

        rateLimiterMock
            .Setup(r => r.AcquireAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                rateLimiterAcquired = true;
                return ValueTask.CompletedTask;
            });

        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                if (!rateLimiterAcquired)
                {
                    httpCalledBeforeRateLimiter = true;
                }
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(BodyWithImage, System.Text.Encoding.UTF8, "application/sparql-results+json"),
                };
            });

        var provider = MakeProvider(client, rateLimiterMock.Object);

        await provider.FetchCoverImageAsync("Q17215001", CancellationToken.None);

        rateLimiterMock.Verify(r => r.AcquireAsync(It.IsAny<CancellationToken>()), Times.Once());
        httpCalledBeforeRateLimiter.Should().BeFalse("rate limiter must be acquired BEFORE the SPARQL HTTP call");
    }

    [Fact]
    public async Task FetchCoverImageAsync_InvalidQid_DoesNotAcquireRateLimiter()
    {
        var (client, _) = MakeClient(HttpStatusCode.OK, BodyWithImage);
        var rateLimiterMock = new Mock<IWikimediaRateLimiter>(MockBehavior.Strict);
        // No setup — strict mock will throw if AcquireAsync is invoked.
        var provider = MakeProvider(client, rateLimiterMock.Object);

        var result = await provider.FetchCoverImageAsync("invalid", CancellationToken.None);

        result.HasImage.Should().BeFalse();
        rateLimiterMock.Verify(
            r => r.AcquireAsync(It.IsAny<CancellationToken>()),
            Times.Never());
    }

    [Fact]
    public async Task FetchCoverImageAsync_RecordsSparqlLatencyMetric_OnSuccess()
    {
        var (client, _) = MakeClient(HttpStatusCode.OK, BodyWithImage);
        var provider = MakeProvider(client);

        var recordedValues = new List<double>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Name == "meepleai.wikidata.sparql.latency_seconds")
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, state) =>
        {
            recordedValues.Add(value);
        });
        listener.Start();

        await provider.FetchCoverImageAsync("Q17215001", CancellationToken.None);

        recordedValues.Should().HaveCount(1, "exactly one latency observation per successful SPARQL round-trip");
        recordedValues[0].Should().BeGreaterThanOrEqualTo(0.0);
    }

    [Fact]
    public async Task FetchCoverImageAsync_Http5xx_LogsWarningAndReturnsNotFound()
    {
        var (client, _) = MakeClient(HttpStatusCode.InternalServerError, "boom");
        var provider = MakeProvider(client);

        // Should not throw — caller decides retry.
        var result = await provider.FetchCoverImageAsync("Q17215001", CancellationToken.None);

        result.HasImage.Should().BeFalse();
        result.Filename.Should().BeNull();
        result.SourceUrl.Should().Be("https://www.wikidata.org/wiki/Q17215001");
    }

    [Fact]
    public async Task FetchCoverImageAsync_CallerCancellation_RethrowsOperationCanceledException()
    {
        // Issue #2157: caller cancellation MUST propagate. We assert by using a
        // CancellationTokenSource that IS cancelled — the provider's exception
        // filter `when (ct.IsCancellationRequested)` rethrows for real caller
        // cancel and swallows-as-NotFound only for HttpClient.Timeout leaks.
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException("cancelled"));
        var client = new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("https://query.wikidata.org/"),
        };
        var provider = MakeProvider(client);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await provider.FetchCoverImageAsync("Q17215001", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    /// <summary>
    /// Issue #2157: <see cref="HttpClient.Timeout"/> firing produces
    /// <see cref="TaskCanceledException"/> (subclass of
    /// <see cref="OperationCanceledException"/>) EVEN WHEN the caller's
    /// <see cref="CancellationToken"/> is NOT cancelled. The provider must
    /// distinguish these two cases by guarding the rethrow with
    /// <c>when (ct.IsCancellationRequested)</c> and mapping the HTTP timeout
    /// to <see cref="WikidataCoverImageResult.NotFound"/>. Otherwise the
    /// batch handler's `OperationCanceledException` catch aborts the entire
    /// batch on the first slow Wikidata response, surfacing a 500 to the
    /// admin client and forfeiting any partial enrichment.
    /// </summary>
    [Fact]
    public async Task FetchCoverImageAsync_HttpClientTimeout_ReturnsNotFound_DoesNotRethrow()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("HttpClient timeout simulation"));
        var client = new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("https://query.wikidata.org/"),
        };
        var provider = MakeProvider(client);

        using var callerCts = new CancellationTokenSource();
        // callerCts is NOT cancelled — the TaskCanceledException originates
        // from HttpClient.Timeout, not the caller's token.
        var result = await provider.FetchCoverImageAsync("Q17215001", callerCts.Token);

        result.HasImage.Should().BeFalse("HttpClient.Timeout maps to NotFound, not exception");
        result.Filename.Should().BeNull();
        result.SourceUrl.Should().Be("https://www.wikidata.org/wiki/Q17215001");
        callerCts.IsCancellationRequested.Should().BeFalse(
            "sanity check: caller token was never cancelled in this scenario");
    }

    /// <summary>
    /// Issue #2157 integration-style coverage: spin up a real
    /// <see cref="HttpClient"/> with a 100 ms timeout pointed at a delegating
    /// handler that delays 500 ms — exercises the actual
    /// <see cref="HttpClient.Timeout"/> code path (vs the Moq throw above)
    /// to lock the guard against future Polly / .NET runtime changes.
    /// </summary>
    [Fact]
    public async Task FetchCoverImageAsync_RealHttpClientTimeout_IsCaughtAsNotFound()
    {
        using var slowHandler = new SlowResponseHandler(TimeSpan.FromMilliseconds(500));
        using var httpClient = new HttpClient(slowHandler)
        {
            BaseAddress = new Uri("https://query.wikidata.org/"),
            Timeout = TimeSpan.FromMilliseconds(100),
        };
        var rateLimiter = new Mock<IWikimediaRateLimiter>();
        rateLimiter.Setup(r => r.AcquireAsync(It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);
        var provider = new WikidataCatalogProvider(
            httpClient,
            NullLogger<WikidataCatalogProvider>.Instance,
            rateLimiter.Object);

        using var callerCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var result = await provider.FetchCoverImageAsync("Q17215001", callerCts.Token);

        result.HasImage.Should().BeFalse();
        callerCts.IsCancellationRequested.Should().BeFalse(
            "real HttpClient.Timeout fires independently of caller token");
    }

    private sealed class SlowResponseHandler : DelegatingHandler
    {
        private readonly TimeSpan _delay;

        public SlowResponseHandler(TimeSpan delay)
        {
            _delay = delay;
            InnerHandler = new HttpClientHandler();
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(_delay, cancellationToken).ConfigureAwait(false);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"results\":{\"bindings\":[]}}", System.Text.Encoding.UTF8, "application/sparql-results+json"),
            };
        }
    }

    [Fact]
    public async Task FetchCoverImageAsync_MalformedJson_ReturnsNotFound()
    {
        var (client, _) = MakeClient(HttpStatusCode.OK, "not json at all");
        var provider = MakeProvider(client);

        var result = await provider.FetchCoverImageAsync("Q17215001", CancellationToken.None);

        result.HasImage.Should().BeFalse();
        result.Filename.Should().BeNull();
    }

    [Fact]
    public async Task FetchCoverImageAsync_FilenamePreservesUrlEncoding()
    {
        // Sanity: the Wikidata IRI uses URL-encoded filenames (per Wikidata convention).
        // We must preserve the raw encoded form, NOT decode it — downstream Commons API
        // and R2 keys expect the raw encoded filename.
        var (client, _) = MakeClient(HttpStatusCode.OK, BodyWithImage);
        var provider = MakeProvider(client);

        var result = await provider.FetchCoverImageAsync("Q17215001", CancellationToken.None);

        result.Filename.Should().Contain("%20", "spaces must remain URL-encoded as %20");
        result.Filename.Should().NotContain(" ", "no raw spaces — verify no accidental URL decoding");
    }

    [Fact]
    public async Task FetchCoverImageAsync_DifferentQid_BuildsCorrectSourceUrl()
    {
        var (client, _) = MakeClient(HttpStatusCode.OK, BodyWithImage);
        var provider = MakeProvider(client);

        var result = await provider.FetchCoverImageAsync("Q98056728", CancellationToken.None);

        result.SourceUrl.Should().Be("https://www.wikidata.org/wiki/Q98056728");
    }

    /// <summary>
    /// ADR DEC-3g — network setup failure (e.g. DNS error, connection refused)
    /// raised BEFORE a response is received does NOT emit the SPARQL latency
    /// metric: the metric measures completed round-trips only, not connection
    /// attempts.
    /// </summary>
    [Fact]
    public async Task FetchCoverImageAsync_NetworkError_DoesNotRecordSparqlLatencyMetric()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("simulated network error"));
        var client = new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("https://query.wikidata.org/"),
        };
        var provider = MakeProvider(client);

        var recordedValues = new List<double>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Name == "meepleai.wikidata.sparql.latency_seconds")
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, state) =>
        {
            recordedValues.Add(value);
        });
        listener.Start();

        var result = await provider.FetchCoverImageAsync("Q17215001", CancellationToken.None);

        result.HasImage.Should().BeFalse("network error caught by provider");
        recordedValues.Should().BeEmpty(
            "metric should NOT be emitted when network setup fails before a response is received");
    }

    /// <summary>
    /// ADR DEC-3g — a non-success HTTP status (5xx) still completes the SPARQL
    /// round-trip, so the latency metric MUST be emitted. This locks the
    /// contract previously broken when the metric lived inside the try block
    /// after a non-cancellation exception path (code review finding score 85
    /// on PR #2104).
    /// </summary>
    [Fact]
    public async Task FetchCoverImageAsync_5xxResponse_RecordsSparqlLatencyMetric()
    {
        var (client, _) = MakeClient(HttpStatusCode.InternalServerError, "boom");
        var provider = MakeProvider(client);

        var recordedValues = new List<double>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Name == "meepleai.wikidata.sparql.latency_seconds")
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, state) =>
        {
            recordedValues.Add(value);
        });
        listener.Start();

        var result = await provider.FetchCoverImageAsync("Q17215001", CancellationToken.None);

        result.HasImage.Should().BeFalse("5xx response returns NotFound");
        recordedValues.Should().HaveCount(
            1,
            "metric MUST be emitted on every completed round-trip, including non-success HTTP status");
        recordedValues[0].Should().BeGreaterThanOrEqualTo(0.0);
    }
}
