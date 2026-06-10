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
    public async Task FetchCoverImageAsync_HttpCancellation_RethrowsOperationCanceledException()
    {
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

        var act = async () => await provider.FetchCoverImageAsync("Q17215001", CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
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
}
