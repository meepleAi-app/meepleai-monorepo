using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.Services.Pdf;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Tests for UploadGameImageCommandHandler.
/// Issue #2255: Implements file upload with validation and storage.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class UploadGameImageCommandHandlerTests
{
    private readonly Mock<IBlobStorageService> _storageServiceMock;
    private readonly Mock<ILogger<UploadGameImageCommandHandler>> _loggerMock;
    private readonly UploadGameImageCommandHandler _handler;

    public UploadGameImageCommandHandlerTests()
    {
        _storageServiceMock = new Mock<IBlobStorageService>();
        _loggerMock = new Mock<ILogger<UploadGameImageCommandHandler>>();
        _handler = new UploadGameImageCommandHandler(
            _storageServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidPngImage_ReturnsSuccessResult()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();
        var fileId = Guid.NewGuid().ToString();
        using var stream = CreateValidPngStream();
        var command = new UploadGameImageCommand(
            GameId: gameId,
            FileName: "test.png",
            FileStream: stream,
            ImageType: ImageType.Image);

        _storageServiceMock
            .Setup(s => s.StoreAsync(It.IsAny<Stream>(), "test.png", gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobStorageResult(
                Success: true,
                FileId: fileId,
                FilePath: $"/uploads/{gameId}/{fileId}.png",
                FileSizeBytes: stream.Length));

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        (result.Success).Should().BeTrue();
        result.FileId.Should().Be(fileId);
        result.FileUrl.Should().NotBeNull();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithNullStream_ReturnsFailureResult()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();
        var command = new UploadGameImageCommand(
            GameId: gameId,
            FileName: "test.png",
            FileStream: null!,
            ImageType: ImageType.Image);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        (result.Success).Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid file stream");
    }

    [Fact]
    public async Task Handle_WithOversizedImage_ReturnsFailureResult()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();
        // Create a stream that reports being larger than 5MB
        using var stream = new TestStream(6 * 1024 * 1024); // 6MB
        var command = new UploadGameImageCommand(
            GameId: gameId,
            FileName: "test.png",
            FileStream: stream,
            ImageType: ImageType.Image);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        (result.Success).Should().BeFalse();
        result.ErrorMessage.Should().Contain("exceeds maximum");
    }

    [Fact]
    public async Task Handle_WithOversizedIcon_ReturnsFailureResult()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();
        // Create a stream that reports being larger than 2MB for icons
        using var stream = new TestStream(3 * 1024 * 1024); // 3MB
        var command = new UploadGameImageCommand(
            GameId: gameId,
            FileName: "icon.png",
            FileStream: stream,
            ImageType: ImageType.Icon);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        (result.Success).Should().BeFalse();
        result.ErrorMessage.Should().Contain("exceeds maximum");
    }

    [Fact]
    public async Task Handle_WithInvalidFileExtension_ReturnsFailureResult()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();
        using var stream = CreateValidPngStream();
        var command = new UploadGameImageCommand(
            GameId: gameId,
            FileName: "test.exe",
            FileStream: stream,
            ImageType: ImageType.Image);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        (result.Success).Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid file type");
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act =
            () => _handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Handle_WhenStorageFails_ReturnsFailureResult()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();
        using var stream = CreateValidPngStream();
        var command = new UploadGameImageCommand(
            GameId: gameId,
            FileName: "test.png",
            FileStream: stream,
            ImageType: ImageType.Image);

        _storageServiceMock
            .Setup(s => s.StoreAsync(It.IsAny<Stream>(), "test.png", gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobStorageResult(
                Success: false,
                FileId: null,
                FilePath: null,
                FileSizeBytes: 0,
                ErrorMessage: "Storage unavailable"));

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        (result.Success).Should().BeFalse();
        result.ErrorMessage.Should().Contain("Storage unavailable");
    }

    private static MemoryStream CreateValidPngStream()
    {
        // PNG magic bytes + minimal valid PNG content
        var pngBytes = new byte[] {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG signature
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, // IHDR chunk header
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, // 1x1 dimensions
            0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53, // bit depth, color type, etc.
            0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41, // CRC, IDAT chunk
            0x54, 0x08, 0xD7, 0x63, 0xF8, 0x0F, 0x00, 0x00, // compressed data
            0x01, 0x01, 0x01, 0x00, 0x18, 0xDD, 0x8D, 0xB4, // more data
            0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, // IEND chunk
            0xAE, 0x42, 0x60, 0x82 // CRC
        };

        return new MemoryStream(pngBytes);
    }

    /// <summary>
    /// Test stream that reports a specified length for testing size validation.
    /// </summary>
    private sealed class TestStream : Stream
    {
        private readonly long _length;
        private long _position;

        public TestStream(long length)
        {
            _length = length;
            _position = 0;
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _length;

#pragma warning disable S2292 // Test stream requires explicit backing field for mutable position
        public override long Position
        {
            get => _position;
            set => _position = value;
        }
#pragma warning restore S2292

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var bytesToRead = (int)Math.Min(count, _length - _position);
            _position += bytesToRead;
            return bytesToRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    _position = offset;
                    break;
                case SeekOrigin.Current:
                    _position += offset;
                    break;
                case SeekOrigin.End:
                    _position = _length + offset;
                    break;
            }
            return _position;
        }

        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
