using System.Net;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Services;

public sealed class BggCoverDownloaderTests
{
    private readonly Mock<IBggCoverUploadPipeline> _pipelineMock = new();
    private readonly Mock<ILogger<BggCoverDownloader>> _loggerMock = new();

    // A genuinely-public IP literal keeps the SSRF DNS check offline/deterministic (a literal
    // resolves to itself without a DNS query). RFC 5737 TEST-NET ranges can't be used here — the
    // IANA-driven SsrfPolicy (#3495) correctly blocks them as reserved; 8.8.8.8 is never dialed.
    private const string PublicHttpsUrl = "https://8.8.8.8/abc.jpg";

    [Fact]
    public async Task DownloadAndUploadAsync_OnSuccess_ReturnsPipelineKey()
    {
        var httpClient = BuildHttpClient(HttpStatusCode.OK, content: new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        _pipelineMock
            .Setup(p => p.UploadAsync(13, It.IsAny<byte[]>(), ".jpg", It.IsAny<CancellationToken>()))
            .ReturnsAsync("bgg-covers/13/cover.jpg");

        var sut = new BggCoverDownloader(httpClient, _loggerMock.Object, _pipelineMock.Object);

        var result = await sut.DownloadAndUploadAsync(13, PublicHttpsUrl, CancellationToken.None);

        result.Should().Be("bgg-covers/13/cover.jpg");
        _pipelineMock.Verify(p => p.UploadAsync(13, It.IsAny<byte[]>(), ".jpg", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DownloadAndUploadAsync_PassesUrlExtensionToPipeline()
    {
        var httpClient = BuildHttpClient(HttpStatusCode.OK, content: new byte[] { 0x01 });
        _pipelineMock
            .Setup(p => p.UploadAsync(99, It.IsAny<byte[]>(), ".png", It.IsAny<CancellationToken>()))
            .ReturnsAsync("bgg-covers/99/cover.png");

        var sut = new BggCoverDownloader(httpClient, _loggerMock.Object, _pipelineMock.Object);

        var result = await sut.DownloadAndUploadAsync(99, "https://8.8.8.8/image.PNG", CancellationToken.None);

        result.Should().Be("bgg-covers/99/cover.png");
        _pipelineMock.Verify(p => p.UploadAsync(99, It.IsAny<byte[]>(), ".png", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DownloadAndUploadAsync_OnHttpError_ReturnsNull()
    {
        var httpClient = BuildHttpClient(HttpStatusCode.NotFound);
        var sut = new BggCoverDownloader(httpClient, _loggerMock.Object, _pipelineMock.Object);

        var result = await sut.DownloadAndUploadAsync(13, PublicHttpsUrl, CancellationToken.None);

        result.Should().BeNull();
        _pipelineMock.Verify(p => p.UploadAsync(
            It.IsAny<int>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DownloadAndUploadAsync_OnPipelineThrows_ReturnsNull()
    {
        var httpClient = BuildHttpClient(HttpStatusCode.OK, content: new byte[] { 0x01 });
        _pipelineMock
            .Setup(p => p.UploadAsync(It.IsAny<int>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Amazon.S3.AmazonS3Exception("S3 unavailable"));

        var sut = new BggCoverDownloader(httpClient, _loggerMock.Object, _pipelineMock.Object);

        var result = await sut.DownloadAndUploadAsync(13, PublicHttpsUrl, CancellationToken.None);

        result.Should().BeNull();
    }

    // ---- SSRF guard (#2655 finding #10) — unchanged behaviour ----

    [Theory]
    [InlineData("http://203.0.113.10/abc.jpg")]
    [InlineData("ftp://203.0.113.10/abc.jpg")]
    public async Task DownloadAndUploadAsync_NonHttpsUrl_BlockedWithoutFetching(string url)
    {
        var handler = TrackingHandler(HttpStatusCode.OK, new byte[] { 0x01 });
        var sut = new BggCoverDownloader(new HttpClient(handler.Object), _loggerMock.Object, _pipelineMock.Object);

        var result = await sut.DownloadAndUploadAsync(13, url, CancellationToken.None);

        result.Should().BeNull("a non-HTTPS URL must be blocked by the SSRF guard");
        VerifyNoHttpCall(handler);
    }

    // #3495 fix 3/N: the private/reserved-IP guarantee moved from a pre-connect DNS check in the
    // downloader to the SSRF connect-pin on the DI-wired primary handler (ConfigureSsrfPin). A unit
    // test with a hand-built mock handler cannot exercise the pin, so that guarantee is proven by
    // (a) SsrfPinnedConnectTests (the pin fails closed on private/mixed/empty resolutions) and
    // (b) BggCoverDownloaderPinIntegrationTests (the BGG DI registration is actually pinned).

    [Fact]
    public async Task DownloadAndUploadAsync_BodyExceedsSizeCap_ReturnsNullWithoutUploading()
    {
        // 11 MB body — over the 10 MB image cap (#3495 C5). ByteArrayContent advertises a
        // Content-Length, so this exercises the pre-read rejection; the streamed ceiling covers
        // the chunked/absent-Content-Length case.
        var oversized = new byte[(11 * 1024 * 1024)];
        var httpClient = BuildHttpClient(HttpStatusCode.OK, oversized);
        var sut = new BggCoverDownloader(httpClient, _loggerMock.Object, _pipelineMock.Object);

        var result = await sut.DownloadAndUploadAsync(7, PublicHttpsUrl, CancellationToken.None);

        result.Should().BeNull();
        _pipelineMock.Verify(
            p => p.UploadAsync(It.IsAny<int>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static HttpClient BuildHttpClient(HttpStatusCode statusCode, byte[]? content = null)
        => new(TrackingHandler(statusCode, content).Object);

    private static Mock<HttpMessageHandler> TrackingHandler(HttpStatusCode statusCode, byte[]? content = null)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = content is null ? null : new ByteArrayContent(content)
            });
        return handler;
    }

    private static void VerifyNoHttpCall(Mock<HttpMessageHandler> handler)
        => handler.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
}
