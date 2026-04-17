using Api.Services.ImageProcessing;
using Api.Tests.Constants;
using FluentAssertions;
using SkiaSharp;
using Xunit;

namespace Api.Tests.Services.ImageProcessing;

/// <summary>
/// Unit tests for SkiaImagePreprocessor.
/// Session Vision AI feature.
/// </summary>
public class SkiaImagePreprocessorTests
{
    private readonly SkiaImagePreprocessor _sut = new();

    // ─── ProcessAsync — resize ──────────────────────────────────────────────

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task ProcessAsync_ResizesLargeImage()
    {
        // Arrange: create a 4000x3000 JPEG
        var imageData = CreateTestJpeg(4000, 3000);

        // Act
        var result = await _sut.ProcessAsync(imageData, "image/jpeg");

        // Assert: default max is 1024
        result.Width.Should().BeLessThanOrEqualTo(1024);
        result.Height.Should().BeLessThanOrEqualTo(1024);
        result.Data.Should().NotBeEmpty();
        result.MediaType.Should().Be("image/jpeg");
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task ProcessAsync_PreservesSmallImageDimensions()
    {
        // Arrange: create a 400x300 JPEG (under the 1024 default limit)
        var imageData = CreateTestJpeg(400, 300);

        // Act
        var result = await _sut.ProcessAsync(imageData, "image/jpeg");

        // Assert: dimensions should remain unchanged
        result.Width.Should().Be(400);
        result.Height.Should().Be(300);
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task ProcessAsync_WithEmptyData_Throws()
    {
        var act = () => _sut.ProcessAsync([], "image/jpeg");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ─── DetectMediaType ────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void DetectMediaType_IdentifiesJpeg()
    {
        // JPEG magic bytes: FF D8 FF
        var jpegHeader = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };

        var result = _sut.DetectMediaType(jpegHeader);

        result.Should().Be("image/jpeg");
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void DetectMediaType_IdentifiesPng()
    {
        // PNG magic bytes: 89 50 4E 47
        var pngHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A };

        var result = _sut.DetectMediaType(pngHeader);

        result.Should().Be("image/png");
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void DetectMediaType_ReturnsNullForUnknownData()
    {
        var unknownData = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05 };

        var result = _sut.DetectMediaType(unknownData);

        result.Should().BeNull();
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void DetectMediaType_ReturnsNullForTooShortData()
    {
        var shortData = new byte[] { 0xFF, 0xD8 };

        var result = _sut.DetectMediaType(shortData);

        result.Should().BeNull();
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private static byte[] CreateTestJpeg(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.CornflowerBlue);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 75);
        return data.ToArray();
    }
}
