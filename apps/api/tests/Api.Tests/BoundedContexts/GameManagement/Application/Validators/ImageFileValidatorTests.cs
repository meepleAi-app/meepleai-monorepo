using Api.BoundedContexts.GameManagement.Application.Validators;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Validators;

/// <summary>
/// Unit tests for ImageFileValidator utility class.
/// Tests magic byte validation and MIME type detection for secure image uploads.
/// Issue #2255: Security fix for MIME type validation vulnerability.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class ImageFileValidatorTests
{
    #region Magic Byte Validation Tests

    [Fact]
    public async Task ValidateMagicBytesAsync_WithValidPng_ReturnsTrue()
    {
        // Arrange - PNG magic bytes: 89 50 4E 47 0D 0A 1A 0A
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00 };
        using var stream = new MemoryStream(pngBytes);

        // Act
        var result = await ImageFileValidator.ValidateMagicBytesAsync(stream, "image/png");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateMagicBytesAsync_WithValidJpegJfif_ReturnsTrue()
    {
        // Arrange - JPEG JFIF magic bytes: FF D8 FF E0
        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 };
        using var stream = new MemoryStream(jpegBytes);

        // Act
        var result = await ImageFileValidator.ValidateMagicBytesAsync(stream, "image/jpeg");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateMagicBytesAsync_WithValidJpegExif_ReturnsTrue()
    {
        // Arrange - JPEG Exif magic bytes: FF D8 FF E1
        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE1, 0x00, 0x10, 0x45, 0x78 };

        // Act
        var result = await ImageFileValidator.ValidateMagicBytesAsync(stream, "image/jpeg");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateMagicBytesAsync_WithValidJpegCanon_ReturnsTrue()
    {
        // Arrange - JPEG Canon magic bytes: FF D8 FF E2
        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE2, 0x00, 0x10, 0x00, 0x00 };

        // Act
        var result = await ImageFileValidator.ValidateMagicBytesAsync(stream, "image/jpeg");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateMagicBytesAsync_WithValidJpegSamsung_ReturnsTrue()
    {
        // Arrange - JPEG Samsung magic bytes: FF D8 FF E3
        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE3, 0x00, 0x10, 0x00, 0x00 };

        // Act
        var result = await ImageFileValidator.ValidateMagicBytesAsync(stream, "image/jpeg");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateMagicBytesAsync_WithValidJpegRaw_ReturnsTrue()
    {
        // Arrange - JPEG raw magic bytes: FF D8 FF DB
        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xDB, 0x00, 0x10, 0x00, 0x00 };

        // Act
        var result = await ImageFileValidator.ValidateMagicBytesAsync(stream, "image/jpeg");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateMagicBytesAsync_WithValidWebp_ReturnsTrue()
    {
        // Arrange - WebP RIFF magic bytes: 52 49 46 46 (RIFF)
        var webpBytes = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00 };
        using var stream = new MemoryStream(webpBytes);

        // Act
        var result = await ImageFileValidator.ValidateMagicBytesAsync(stream, "image/webp");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateMagicBytesAsync_WithInvalidMagicBytes_ReturnsFalse()
    {
        // Arrange - Random bytes that don't match PNG signature
        var invalidBytes = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 };
        using var stream = new MemoryStream(invalidBytes);

        // Act
        var result = await ImageFileValidator.ValidateMagicBytesAsync(stream, "image/png");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateMagicBytesAsync_WithMismatchedMimeType_ReturnsFalse()
    {
        // Arrange - PNG magic bytes but checking for JPEG
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        // Act
        var result = await ImageFileValidator.ValidateMagicBytesAsync(stream, "image/jpeg");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateMagicBytesAsync_WithUnsupportedMimeType_ReturnsFalse()
    {
        // Arrange
        var someBytes = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 };
        using var stream = new MemoryStream(someBytes);

        // Act
        var result = await ImageFileValidator.ValidateMagicBytesAsync(stream, "image/gif");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateMagicBytesAsync_WithTooSmallFile_ReturnsFalse()
    {
        // Arrange - File smaller than any valid signature
        var tinyBytes = new byte[] { 0x89, 0x50 };
        using var stream = new MemoryStream(tinyBytes);

        // Act
        var result = await ImageFileValidator.ValidateMagicBytesAsync(stream, "image/png");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateMagicBytesAsync_WithEmptyStream_ReturnsFalse()
    {
        // Arrange
        using var stream = new MemoryStream(Array.Empty<byte>());

        // Act
        var result = await ImageFileValidator.ValidateMagicBytesAsync(stream, "image/png");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateMagicBytesAsync_WithNonSeekableStream_ThrowsArgumentException()
    {
        // Arrange
        using var nonSeekableStream = new NonSeekableStream();

        // Act
        var act = () => ImageFileValidator.ValidateMagicBytesAsync(nonSeekableStream, "image/png");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*seekable*");
    }

    [Fact]
    public async Task ValidateMagicBytesAsync_ResetsStreamPosition()
    {
        // Arrange
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00 };
        stream.Position = 5; // Set to middle of stream

        // Act
        await ImageFileValidator.ValidateMagicBytesAsync(stream, "image/png");

        // Assert
        stream.Position.Should().Be(0);
    }

    #endregion

    #region GetMimeTypeFromFileName Tests

    [Theory]
    [InlineData("image.png", "image/png")]
    [InlineData("photo.jpg", "image/jpeg")]
    [InlineData("photo.jpeg", "image/jpeg")]
    [InlineData("picture.webp", "image/webp")]
    [InlineData("IMAGE.PNG", "image/png")]
    [InlineData("PHOTO.JPG", "image/jpeg")]
    [InlineData("Picture.WebP", "image/webp")]
    public void GetMimeTypeFromFileName_WithSupportedExtension_ReturnsMimeType(string fileName, string expectedMimeType)
    {
        // Act
        var result = ImageFileValidator.GetMimeTypeFromFileName(fileName);

        // Assert
        result.Should().Be(expectedMimeType);
    }

    [Theory]
    [InlineData("document.pdf")]
    [InlineData("script.js")]
    [InlineData("data.json")]
    [InlineData("image.gif")]
    [InlineData("image.bmp")]
    [InlineData("image.tiff")]
    [InlineData("image.svg")] // SVG intentionally excluded for security
    public void GetMimeTypeFromFileName_WithUnsupportedExtension_ReturnsNull(string fileName)
    {
        // Act
        var result = ImageFileValidator.GetMimeTypeFromFileName(fileName);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetMimeTypeFromFileName_WithNoExtension_ReturnsNull()
    {
        // Act
        var result = ImageFileValidator.GetMimeTypeFromFileName("filename");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetMimeTypeFromFileName_WithEmptyString_ReturnsNull()
    {
        // Act
        var result = ImageFileValidator.GetMimeTypeFromFileName("");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetMimeTypeFromFileName_WithPathAndExtension_ReturnsMimeType()
    {
        // Act
        var result = ImageFileValidator.GetMimeTypeFromFileName("/path/to/image.png");

        // Assert
        result.Should().Be("image/png");
    }

    #endregion

    #region AllowedMimeTypes Tests

    [Theory]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    [InlineData("image/jpg")]
    [InlineData("image/webp")]
    [InlineData("IMAGE/PNG")] // Case insensitive
    [InlineData("Image/Jpeg")]
    public void AllowedMimeTypes_ContainsSupportedTypes(string mimeType)
    {
        // Assert
        ImageFileValidator.AllowedMimeTypes.Should().Contain(mimeType);
    }

    [Theory]
    [InlineData("image/gif")]
    [InlineData("image/svg+xml")]
    [InlineData("image/bmp")]
    [InlineData("image/tiff")]
    [InlineData("application/pdf")]
    [InlineData("text/html")]
    public void AllowedMimeTypes_DoesNotContainUnsupportedTypes(string mimeType)
    {
        // Assert
        ImageFileValidator.AllowedMimeTypes.Should().NotContain(mimeType);
    }

    [Fact]
    public void AllowedMimeTypes_DoesNotContainSvg_ForSecurityReasons()
    {
        // SVG can contain embedded JavaScript (XSS risk)
        // Assert
        ImageFileValidator.AllowedMimeTypes.Should().NotContain("image/svg+xml");
        ImageFileValidator.AllowedMimeTypes.Should().NotContain("image/svg");
    }

    [Fact]
    public void AllowedMimeTypes_HasExpectedCount()
    {
        // Assert - Only 4 safe image types
        ImageFileValidator.AllowedMimeTypes.Should().HaveCount(4);
    }

    #endregion

    #region Security Tests

    [Fact]
    public async Task ValidateMagicBytesAsync_WithExecutableMaskedAsPng_ReturnsFalse()
    {
        // Arrange - EXE/PE file signature (MZ) pretending to be PNG
        var exeBytes = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00 };
        using var stream = new MemoryStream(exeBytes);

        // Act
        var result = await ImageFileValidator.ValidateMagicBytesAsync(stream, "image/png");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateMagicBytesAsync_WithZipMaskedAsImage_ReturnsFalse()
    {
        // Arrange - ZIP file signature (PK) pretending to be image
        var zipBytes = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x0A, 0x00, 0x00, 0x00 };
        using var stream = new MemoryStream(zipBytes);

        // Act
        var result = await ImageFileValidator.ValidateMagicBytesAsync(stream, "image/jpeg");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateMagicBytesAsync_WithPdfMaskedAsImage_ReturnsFalse()
    {
        // Arrange - PDF file signature (%PDF) pretending to be image
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 };
        using var stream = new MemoryStream(pdfBytes);

        // Act
        var result = await ImageFileValidator.ValidateMagicBytesAsync(stream, "image/png");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Helper Classes

    /// <summary>
    /// Non-seekable stream for testing validation error handling.
    /// </summary>
    private class NonSeekableStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => 0;
        public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => 0;
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    #endregion
}
