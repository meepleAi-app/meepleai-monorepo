using System.Net;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.Services.Pdf;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Services;

public sealed class BggCoverDownloaderTests
{
    private readonly Mock<IBlobStorageService> _blobMock = new();
    private readonly Mock<ILogger<BggCoverDownloader>> _loggerMock = new();

    // A public, non-private IP literal (RFC 5737 TEST-NET-3). Using an IP literal keeps the SSRF
    // guard's DNS resolution offline/deterministic in unit tests (Dns.GetHostAddressesAsync on a
    // literal returns it without a DNS query), while still passing the private-IP check.
    private const string PublicHttpsUrl = "https://203.0.113.10/abc.jpg";

    [Fact]
    public async Task DownloadAndUploadAsync_OnSuccess_ReturnsR2Key()
    {
        // Arrange
        var httpClient = BuildHttpClient(HttpStatusCode.OK, content: new byte[] { 0x89, 0x50, 0x4E, 0x47 /* fake PNG header */ });
        _blobMock.Setup(b => b.StoreAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                BlobCategory.GameImage,
                "bgg-cover-13",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobStorageResult(Success: true, FileId: "bgg-cover-13", FilePath: "bgg-cover-13", FileSizeBytes: 4, ErrorMessage: null));

        var sut = new BggCoverDownloader(httpClient, _blobMock.Object, _loggerMock.Object);

        // Act
        var result = await sut.DownloadAndUploadAsync(13, PublicHttpsUrl, CancellationToken.None);

        // Assert
        result.Should().Be("bgg-cover-13");
    }

    [Fact]
    public async Task DownloadAndUploadAsync_OnHttpError_ReturnsNull()
    {
        // Arrange
        var httpClient = BuildHttpClient(HttpStatusCode.NotFound);
        var sut = new BggCoverDownloader(httpClient, _blobMock.Object, _loggerMock.Object);

        // Act
        var result = await sut.DownloadAndUploadAsync(13, PublicHttpsUrl, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        _blobMock.Verify(b => b.StoreAsync(
            It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DownloadAndUploadAsync_OnUploadFailure_ReturnsNull()
    {
        // Arrange
        var httpClient = BuildHttpClient(HttpStatusCode.OK, content: new byte[] { 0x01 });
        _blobMock.Setup(b => b.StoreAsync(
                It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobStorageResult(Success: false, FileId: null, FilePath: null, FileSizeBytes: 0, ErrorMessage: "S3 unavailable"));

        var sut = new BggCoverDownloader(httpClient, _blobMock.Object, _loggerMock.Object);

        // Act
        var result = await sut.DownloadAndUploadAsync(13, PublicHttpsUrl, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    // ---- SSRF guard (#2655 finding #10) ----

    [Theory]
    [InlineData("http://203.0.113.10/abc.jpg")]      // non-HTTPS scheme
    [InlineData("ftp://203.0.113.10/abc.jpg")]        // non-HTTP scheme
    public async Task DownloadAndUploadAsync_NonHttpsUrl_BlockedWithoutFetching(string url)
    {
        var handler = TrackingHandler(HttpStatusCode.OK, new byte[] { 0x01 });
        var sut = new BggCoverDownloader(new HttpClient(handler.Object), _blobMock.Object, _loggerMock.Object);

        var result = await sut.DownloadAndUploadAsync(13, url, CancellationToken.None);

        result.Should().BeNull("a non-HTTPS URL must be blocked by the SSRF guard");
        VerifyNoHttpCall(handler);
    }

    [Theory]
    [InlineData("https://127.0.0.1/abc.jpg")]         // loopback
    [InlineData("https://169.254.169.254/latest")]    // cloud metadata endpoint
    [InlineData("https://10.0.0.5/abc.jpg")]          // RFC 1918 private
    public async Task DownloadAndUploadAsync_PrivateOrReservedIp_BlockedWithoutFetching(string url)
    {
        var handler = TrackingHandler(HttpStatusCode.OK, new byte[] { 0x01 });
        var sut = new BggCoverDownloader(new HttpClient(handler.Object), _blobMock.Object, _loggerMock.Object);

        var result = await sut.DownloadAndUploadAsync(13, url, CancellationToken.None);

        result.Should().BeNull("a URL resolving to a private/reserved IP must be blocked by the SSRF guard");
        VerifyNoHttpCall(handler);
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
