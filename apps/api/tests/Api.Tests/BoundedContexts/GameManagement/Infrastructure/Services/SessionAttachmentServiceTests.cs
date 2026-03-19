using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment;
using Api.BoundedContexts.GameManagement.Infrastructure.Services;
using Api.Services.Pdf;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Infrastructure.Services;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class SessionAttachmentServiceTests
{
    private readonly Mock<IBlobStorageService> _blobStorageMock;
    private readonly Mock<ILogger<SessionAttachmentService>> _loggerMock;
    private readonly SessionAttachmentService _sut;

    public SessionAttachmentServiceTests()
    {
        _blobStorageMock = new Mock<IBlobStorageService>();
        _loggerMock = new Mock<ILogger<SessionAttachmentService>>();
        _sut = new SessionAttachmentService(_blobStorageMock.Object, _loggerMock.Object);
    }

    #region Upload Tests

    [Fact]
    public async Task UploadAsync_WithInvalidContentType_ReturnsFailure()
    {
        using var stream = new MemoryStream(new byte[100]);

        var result = await _sut.UploadAsync(
            Guid.NewGuid(), Guid.NewGuid(), stream,
            "file.gif", "image/gif", 100,
            AttachmentType.BoardState, null, null);

        Assert.False(result.Success);
        Assert.Contains("content type", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadAsync_WithInvalidMagicBytes_ReturnsFailure()
    {
        // Create a stream with invalid magic bytes (not JPEG or PNG)
        var fakeData = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09 };
        using var stream = new MemoryStream(fakeData);

        var result = await _sut.UploadAsync(
            Guid.NewGuid(), Guid.NewGuid(), stream,
            "photo.jpg", "image/jpeg", fakeData.Length,
            AttachmentType.BoardState, null, null);

        Assert.False(result.Success);
        Assert.Contains("content type", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadAsync_WhenBlobStoreFails_ReturnsFailure()
    {
        using var stream = CreateMinimalJpegStream();

        _blobStorageMock
            .Setup(x => x.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobStorageResult(false, null, null, 0, "S3 error"));

        var result = await _sut.UploadAsync(
            Guid.NewGuid(), Guid.NewGuid(), stream,
            "photo.jpg", "image/jpeg", stream.Length,
            AttachmentType.BoardState, null, null);

        Assert.False(result.Success);
        Assert.Equal("S3 error", result.ErrorMessage);
    }

    [Fact]
    public async Task UploadAsync_WhenSuccessful_ReturnsBlobUrlAndSize()
    {
        var fileId = Guid.NewGuid().ToString("N");
        var storagePath = $"session-photos-abc/{fileId}_photo.jpg";

        _blobStorageMock
            .Setup(x => x.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobStorageResult(true, fileId, storagePath, 5000));

        var result = await _sut.UploadAsync(
            Guid.NewGuid(), Guid.NewGuid(), stream,
            "photo.jpg", "image/jpeg", stream.Length,
            AttachmentType.BoardState, "A caption", 1);

        Assert.True(result.Success);
        Assert.Equal(storagePath, result.BlobUrl);
        Assert.Equal(5000, result.FileSizeBytes);
    }

    [Fact]
    public async Task UploadAsync_UsesSessionIdAsStorageFolder()
    {
        var sessionId = Guid.NewGuid();
        string? capturedFolder = null;

        _blobStorageMock
            .Setup(x => x.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Stream, string, string, CancellationToken>((_, _, folder, _) => capturedFolder = folder)
            .ReturnsAsync(new BlobStorageResult(true, "fid", "path", 100));

        await _sut.UploadAsync(
            sessionId, Guid.NewGuid(), stream,
            "photo.jpg", "image/jpeg", stream.Length,
            AttachmentType.BoardState, null, null);

        Assert.Equal($"session-photos-{sessionId:N}", capturedFolder);
    }

    [Fact]
    public async Task UploadAsync_IncludesPlayerIdInFileName()
    {
        var playerId = Guid.NewGuid();
        string? capturedFileName = null;

        _blobStorageMock
            .Setup(x => x.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Stream, string, string, CancellationToken>((_, fileName, _, _) => capturedFileName = fileName)
            .ReturnsAsync(new BlobStorageResult(true, "fid", "path", 100));

        await _sut.UploadAsync(
            Guid.NewGuid(), playerId, stream,
            "photo.jpg", "image/jpeg", stream.Length,
            AttachmentType.BoardState, null, null);

        Assert.NotNull(capturedFileName);
        Assert.StartsWith(playerId.ToString("N"), capturedFileName);
        Assert.EndsWith(".jpg", capturedFileName);
    }

    [Fact]
    public async Task UploadAsync_WithPngContentType_UsesCorrectExtension()
    {
        using var stream = CreateMinimalPngStream();
        string? capturedFileName = null;
        var callCount = 0;

        _blobStorageMock
            .Setup(x => x.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Stream, string, string, CancellationToken>((_, fileName, _, _) =>
            {
                callCount++;
                if (callCount == 1) capturedFileName = fileName; // Capture only the original upload
            })
            .ReturnsAsync(new BlobStorageResult(true, "fid", "path", 100));

        await _sut.UploadAsync(
            Guid.NewGuid(), Guid.NewGuid(), stream,
            "photo.png", "image/png", stream.Length,
            AttachmentType.BoardState, null, null);

        Assert.NotNull(capturedFileName);
        Assert.EndsWith(".png", capturedFileName);
    }

    [Fact]
    public async Task UploadAsync_ThumbnailFailure_StillReturnsSuccess()
    {

        var callCount = 0;
        _blobStorageMock
            .Setup(x => x.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1) // original upload succeeds
                    return new BlobStorageResult(true, "fid", "path/original.jpg", 5000);
                // thumbnail upload fails
                return new BlobStorageResult(false, null, null, 0, "thumbnail failed");
            });

        var result = await _sut.UploadAsync(
            Guid.NewGuid(), Guid.NewGuid(), stream,
            "photo.jpg", "image/jpeg", stream.Length,
            AttachmentType.BoardState, null, null);

        Assert.True(result.Success);
        Assert.Null(result.ThumbnailUrl);
    }

    #endregion

    #region GetDownloadUrl Tests

    [Fact]
    public async Task GetDownloadUrlAsync_WithPresignedUrl_ReturnsPresigned()
    {
        var blobUrl = "folder/fileId_photo.jpg";
        _blobStorageMock
            .Setup(x => x.GetPresignedDownloadUrlAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync("https://s3.example.com/signed-url");

        var result = await _sut.GetDownloadUrlAsync(blobUrl);

        Assert.Equal("https://s3.example.com/signed-url", result);
    }

    [Fact]
    public async Task GetDownloadUrlAsync_WithLocalStorage_ReturnsBlobUrl()
    {
        var blobUrl = "folder/fileId_photo.jpg";
        _blobStorageMock
            .Setup(x => x.GetPresignedDownloadUrlAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync((string?)null);

        var result = await _sut.GetDownloadUrlAsync(blobUrl);

        Assert.Equal(blobUrl, result);
    }

    [Fact]
    public async Task GetDownloadUrlAsync_WithEmptyUrl_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.GetDownloadUrlAsync(""));
    }

    #endregion

    #region DeleteBlobs Tests

    [Fact]
    public async Task DeleteBlobsAsync_DeletesBothOriginalAndThumbnail()
    {
        var deletedIds = new List<string>();
        _blobStorageMock
            .Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((fileId, _, _) => deletedIds.Add(fileId))
            .ReturnsAsync(true);

        await _sut.DeleteBlobsAsync("folder/abc_photo.jpg", "folder/def_thumb.jpg");

        Assert.Equal(2, deletedIds.Count);
        Assert.Contains("abc", deletedIds);
        Assert.Contains("def", deletedIds);
    }

    [Fact]
    public async Task DeleteBlobsAsync_WithNoThumbnail_DeletesOnlyOriginal()
    {
        var deleteCount = 0;
        _blobStorageMock
            .Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, _, _) => deleteCount++)
            .ReturnsAsync(true);

        await _sut.DeleteBlobsAsync("folder/abc_photo.jpg", null);

        Assert.Equal(1, deleteCount);
    }

    #endregion

    #region Thumbnail Dimension Tests

    [Theory]
    [InlineData(600, 400, 300, 200)]
    [InlineData(400, 600, 200, 300)]
    [InlineData(300, 300, 300, 300)]
    [InlineData(150, 100, 150, 100)]
    [InlineData(1200, 800, 300, 200)]
    [InlineData(800, 1200, 200, 300)]
    public void CalculateThumbnailDimensions_MaintainsAspectRatio(
        int inputWidth, int inputHeight, int expectedWidth, int expectedHeight)
    {
        var (width, height) = SessionAttachmentService.CalculateThumbnailDimensions(inputWidth, inputHeight);

        Assert.Equal(expectedWidth, width);
        Assert.Equal(expectedHeight, height);
    }

    [Fact]
    public void CalculateThumbnailDimensions_SmallImage_ReturnsOriginalSize()
    {
        var (width, height) = SessionAttachmentService.CalculateThumbnailDimensions(100, 50);

        Assert.Equal(100, width);
        Assert.Equal(50, height);
    }

    [Fact]
    public void CalculateThumbnailDimensions_ExactMax_ReturnsMax()
    {
        var (width, height) = SessionAttachmentService.CalculateThumbnailDimensions(300, 300);

        Assert.Equal(300, width);
        Assert.Equal(300, height);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullBlobStorage_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SessionAttachmentService(null!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SessionAttachmentService(_blobStorageMock.Object, null!));
    }

    #endregion

    #region Thumbnail Generation Tests

    [Fact]
    public async Task GenerateThumbnailAsync_WithValidJpeg_ReturnsStream()
    {
        using var source = CreateMinimalJpegImage(600, 400);
        var result = await SessionAttachmentService.GenerateThumbnailAsync(source);

        Assert.NotNull(result);
        Assert.True(result!.Length > 0);
        result.Dispose();
    }

    [Fact]
    public async Task GenerateThumbnailAsync_ResizesToMaxDimension()
    {
        using var source = CreateMinimalJpegImage(1200, 800);
        using var result = await SessionAttachmentService.GenerateThumbnailAsync(source);

        Assert.NotNull(result);
        // Verify the output is a valid JPEG by checking magic bytes
        result!.Position = 0;
        var header = new byte[2];
        _ = await result.ReadAsync(header.AsMemory(0, 2));
        Assert.Equal(0xFF, header[0]);
        Assert.Equal(0xD8, header[1]);
    }

    #endregion

    #region Helper Methods

    private static MemoryStream CreateMinimalJpegStream()
    {
        return CreateMinimalJpegImage(100, 100);
    }

    private static MemoryStream CreateMinimalJpegImage(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        var stream = new MemoryStream();
        image.Save(stream, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder());
        stream.Position = 0;
        return stream;
    }

    private static MemoryStream CreateMinimalPngStream()
    {
        using var image = new Image<Rgba32>(100, 100);
        var stream = new MemoryStream();
        image.Save(stream, new SixLabors.ImageSharp.Formats.Png.PngEncoder());
        stream.Position = 0;
        return stream;
    }

    #endregion
}
