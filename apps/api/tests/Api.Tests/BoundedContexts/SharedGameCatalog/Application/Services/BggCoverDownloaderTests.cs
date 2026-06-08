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
        var result = await sut.DownloadAndUploadAsync(13, "https://cf.geekdo-images.com/abc.jpg", CancellationToken.None);

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
        var result = await sut.DownloadAndUploadAsync(13, "https://cf.geekdo-images.com/missing.jpg", CancellationToken.None);

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
        var result = await sut.DownloadAndUploadAsync(13, "https://cf.geekdo-images.com/abc.jpg", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    private static HttpClient BuildHttpClient(HttpStatusCode statusCode, byte[]? content = null)
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
        return new HttpClient(handler.Object);
    }
}
