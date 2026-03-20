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
using FluentAssertions;

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

        (result.Success).Should().BeFalse();
        result.ErrorMessage.Should().ContainEquivalentOf("content type");
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

        (result.Success).Should().BeFalse();
        result.ErrorMessage.Should().ContainEquivalentOf("content type");
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

        (result.Success).Should().BeFalse();
        result.ErrorMessage.Should().Be("S3 error");
    }

    [Fact]
    public async Task UploadAsync_WhenSuccessful_ReturnsBlobUrlAndSize()
    {
        using var stream = CreateMinimalJpegStream();
        var fileId = Guid.NewGuid().ToString("N");
        var storagePath = $"session-photos-abc/{fileId}_photo.jpg";

        _blobStorageMock
            .Setup(x => x.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobStorageResult(true, fileId, storagePath, 5000));

        var result = await _sut.UploadAsync(
            Guid.NewGuid(), Guid.NewGuid(), stream,
            "photo.jpg", "image/jpeg", stream.Length,
            AttachmentType.BoardState, "A caption", 1);

        (result.Success).Should().BeTrue();
        result.BlobUrl.Should().Be(storagePath);
        result.FileSizeBytes.Should().Be(5000);
    }

    [Fact]
    public async Task UploadAsync_UsesSessionIdAsStorageFolder()
    {
        using var stream = CreateMinimalJpegStream();
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

        capturedFolder.Should().Be($"session-photos-{sessionId:N}");
    }

    [Fact]
    public async Task UploadAsync_IncludesPlayerIdInFileName()
    {
        using var stream = CreateMinimalJpegStream();
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

        capturedFileName.Should().NotBeNull();
        capturedFileName.Should().StartWith(playerId.ToString("N"));
        capturedFileName.Should().EndWith(".jpg");
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

        capturedFileName.Should().NotBeNull();
        capturedFileName.Should().EndWith(".png");
    }

    [Fact]
    public async Task UploadAsync_ThumbnailFailure_StillReturnsSuccess()
    {
        using var stream = CreateMinimalJpegStream();

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

        (result.Success).Should().BeTrue();
        result.ThumbnailUrl.Should().BeNull();
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

        result.Should().Be("https://s3.example.com/signed-url");
    }

    [Fact]
    public async Task GetDownloadUrlAsync_WithLocalStorage_ReturnsBlobUrl()
    {
        var blobUrl = "folder/fileId_photo.jpg";
        _blobStorageMock
            .Setup(x => x.GetPresignedDownloadUrlAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync((string?)null);

        var result = await _sut.GetDownloadUrlAsync(blobUrl);

        result.Should().Be(blobUrl);
    }

    [Fact]
    public async Task GetDownloadUrlAsync_WithEmptyUrl_ThrowsArgumentException()
    {
        var act = 
            () => _sut.GetDownloadUrlAsync("");
        await act.Should().ThrowAsync<ArgumentException>();
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

        deletedIds.Count.Should().Be(2);
        deletedIds.Should().Contain("abc");
        deletedIds.Should().Contain("def");
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

        deleteCount.Should().Be(1);
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

        width.Should().Be(expectedWidth);
        height.Should().Be(expectedHeight);
    }

    [Fact]
    public void CalculateThumbnailDimensions_SmallImage_ReturnsOriginalSize()
    {
        var (width, height) = SessionAttachmentService.CalculateThumbnailDimensions(100, 50);

        width.Should().Be(100);
        height.Should().Be(50);
    }

    [Fact]
    public void CalculateThumbnailDimensions_ExactMax_ReturnsMax()
    {
        var (width, height) = SessionAttachmentService.CalculateThumbnailDimensions(300, 300);

        width.Should().Be(300);
        height.Should().Be(300);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullBlobStorage_ThrowsArgumentNullException()
    {
        var act = () =>
            new SessionAttachmentService(null!, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        var act = () =>
            new SessionAttachmentService(_blobStorageMock.Object, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Thumbnail Generation Tests

    [Fact]
    public async Task GenerateThumbnailAsync_WithValidJpeg_ReturnsStream()
    {
        using var source = CreateMinimalJpegImage(600, 400);
        var result = await SessionAttachmentService.GenerateThumbnailAsync(source);

        result.Should().NotBeNull();
        (result!.Length > 0).Should().BeTrue();
        result.Dispose();
    }

    [Fact]
    public async Task GenerateThumbnailAsync_ResizesToMaxDimension()
    {
        using var source = CreateMinimalJpegImage(1200, 800);
        using var result = await SessionAttachmentService.GenerateThumbnailAsync(source);

        result.Should().NotBeNull();
        // Verify the output is a valid JPEG by checking magic bytes
        result!.Position = 0;
        var header = new byte[2];
        _ = await result.ReadAsync(header.AsMemory(0, 2));
        header[0].Should().Be(0xFF);
        header[1].Should().Be(0xD8);
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
