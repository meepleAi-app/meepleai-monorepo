using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
using FluentAssertions;
using ImageMagick;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure.Services;

/// <summary>
/// Unit tests for <see cref="WebpVariantGenerator"/> — issue #1823 M6.
/// Verifies aspect-ratio-preserving center crop + WebP encoding via
/// Magick.NET 14.x (ADR DEC-3d-1, issue #2055 Phase G AC-G2: migration
/// from SixLabors.ImageSharp 3.1.12 split-license to Magick.NET Apache 2.0).
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public class WebpVariantGeneratorTests
{
    private const int TargetWidth = 200;
    private const int TargetHeight = 300;

    // ──────────────────────────────────────────────────────────────────────────
    // Happy path — aspect-ratio-preserving center crop produces expected output
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateWebpAsync_LargeImage_ResizesAndCropsTo200x300()
    {
        var pngBytes = CreateSolidImagePng(width: 800, height: 600, color: MagickColors.Red);
        var sut = CreateSut();

        var output = await sut.GenerateWebpAsync(pngBytes, TargetWidth, TargetHeight, CancellationToken.None);

        output.Should().NotBeNullOrEmpty();
        using var decoded = new MagickImage(output);
        decoded.Width.Should().Be((uint)TargetWidth,
            "FillArea + Extent must enforce exact target dimensions");
        decoded.Height.Should().Be((uint)TargetHeight,
            "FillArea + Extent must enforce exact target dimensions");
    }

    [Fact]
    public async Task GenerateWebpAsync_SquareImage_CropsCenterTo200x300()
    {
        // 600x600 source → 200x300 output: Magick.NET FillArea+Extent preserves
        // the 2:3 aspect ratio by clipping the wider/taller edges (centered)
        // before resizing.
        var pngBytes = CreateSolidImagePng(width: 600, height: 600, color: MagickColors.Blue);
        var sut = CreateSut();

        var output = await sut.GenerateWebpAsync(pngBytes, TargetWidth, TargetHeight, CancellationToken.None);

        using var decoded = new MagickImage(output);
        decoded.Width.Should().Be((uint)TargetWidth);
        decoded.Height.Should().Be((uint)TargetHeight);
    }

    [Fact]
    public async Task GenerateWebpAsync_PortraitImage_ResizesAndCropsTo200x300()
    {
        // 400x800 → 200x300: source aspect 1:2 vs target 2:3, requires crop top/bottom
        // (since source is "taller" than target ratio).
        var pngBytes = CreateSolidImagePng(width: 400, height: 800, color: MagickColors.Green);
        var sut = CreateSut();

        var output = await sut.GenerateWebpAsync(pngBytes, TargetWidth, TargetHeight, CancellationToken.None);

        using var decoded = new MagickImage(output);
        decoded.Width.Should().Be((uint)TargetWidth);
        decoded.Height.Should().Be((uint)TargetHeight);
    }

    [Fact]
    public async Task GenerateWebpAsync_DefaultDimensions_200x300_AspectRatio()
    {
        // Explicit test for the canonical cover thumbnail (200x300, 2:3 portrait).
        // This is the dimension production callers will pass per #1823 spec.
        var pngBytes = CreateSolidImagePng(width: 1000, height: 1500, color: MagickColors.Yellow);
        var sut = CreateSut();

        var output = await sut.GenerateWebpAsync(pngBytes, 200, 300, CancellationToken.None);

        using var decoded = new MagickImage(output);
        decoded.Width.Should().Be(200u);
        decoded.Height.Should().Be(300u);
    }

    [Fact]
    public async Task GenerateWebpAsync_OutputIsWebpFormat()
    {
        // WebP magic bytes per RFC 6386: "RIFF" at offset 0, "WEBP" at offset 8.
        // This is the cheapest way to verify the encoder ran (vs JPEG/PNG fallback).
        var pngBytes = CreateSolidImagePng(width: 400, height: 400, color: MagickColors.Black);
        var sut = CreateSut();

        var output = await sut.GenerateWebpAsync(pngBytes, TargetWidth, TargetHeight, CancellationToken.None);

        output.Should().HaveCountGreaterThanOrEqualTo(12);
        System.Text.Encoding.ASCII.GetString(output, 0, 4).Should().Be("RIFF",
            "WebP container starts with RIFF magic bytes");
        System.Text.Encoding.ASCII.GetString(output, 8, 4).Should().Be("WEBP",
            "WebP container has WEBP signature at offset 8");
    }

    [Fact]
    public async Task GenerateWebpAsync_JpegInput_OutputsWebp()
    {
        // Magick.NET auto-detects input format. Verify JPEG → WebP works
        // (production sources are PNG/JPEG/WebP per Wikimedia Commons).
        var jpegBytes = CreateSolidImageJpeg(width: 800, height: 600, color: MagickColors.Purple);
        var sut = CreateSut();

        var output = await sut.GenerateWebpAsync(jpegBytes, TargetWidth, TargetHeight, CancellationToken.None);

        output.Should().NotBeNullOrEmpty();
        System.Text.Encoding.ASCII.GetString(output, 0, 4).Should().Be("RIFF");
        System.Text.Encoding.ASCII.GetString(output, 8, 4).Should().Be("WEBP");
        using var decoded = new MagickImage(output);
        decoded.Width.Should().Be((uint)TargetWidth);
        decoded.Height.Should().Be((uint)TargetHeight);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Input validation (ArgumentException)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateWebpAsync_NullInput_ThrowsArgumentException()
    {
        var sut = CreateSut();

        var act = async () => await sut.GenerateWebpAsync(null!, TargetWidth, TargetHeight, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>(
            "null input is a programmer error and must surface as ArgumentException");
    }

    [Fact]
    public async Task GenerateWebpAsync_EmptyInput_ThrowsArgumentException()
    {
        var sut = CreateSut();

        var act = async () => await sut.GenerateWebpAsync(Array.Empty<byte>(), TargetWidth, TargetHeight, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task GenerateWebpAsync_ZeroOrNegativeWidth_ThrowsArgumentException(int width)
    {
        var pngBytes = CreateSolidImagePng(width: 100, height: 100, color: MagickColors.Red);
        var sut = CreateSut();

        var act = async () => await sut.GenerateWebpAsync(pngBytes, width, TargetHeight, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task GenerateWebpAsync_ZeroOrNegativeHeight_ThrowsArgumentException(int height)
    {
        var pngBytes = CreateSolidImagePng(width: 100, height: 100, color: MagickColors.Red);
        var sut = CreateSut();

        var act = async () => await sut.GenerateWebpAsync(pngBytes, TargetWidth, height, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ImageProcessingException — unreadable / unsupported sources
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateWebpAsync_CorruptedImage_ThrowsImageProcessingException()
    {
        // 64 random bytes that do not match any known image magic header.
        var corrupted = new byte[] {
            0xDE, 0xAD, 0xBE, 0xEF, 0xCA, 0xFE, 0xBA, 0xBE,
            0xDE, 0xAD, 0xBE, 0xEF, 0xCA, 0xFE, 0xBA, 0xBE,
            0xDE, 0xAD, 0xBE, 0xEF, 0xCA, 0xFE, 0xBA, 0xBE,
            0xDE, 0xAD, 0xBE, 0xEF, 0xCA, 0xFE, 0xBA, 0xBE,
            0xDE, 0xAD, 0xBE, 0xEF, 0xCA, 0xFE, 0xBA, 0xBE,
            0xDE, 0xAD, 0xBE, 0xEF, 0xCA, 0xFE, 0xBA, 0xBE,
            0xDE, 0xAD, 0xBE, 0xEF, 0xCA, 0xFE, 0xBA, 0xBE,
            0xDE, 0xAD, 0xBE, 0xEF, 0xCA, 0xFE, 0xBA, 0xBE,
        };
        var sut = CreateSut();

        var act = async () => await sut.GenerateWebpAsync(corrupted, TargetWidth, TargetHeight, CancellationToken.None);

        await act.Should().ThrowAsync<ImageProcessingException>(
            "unreadable sources should surface as a typed domain exception, not raw Magick.NET errors");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Cancellation
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateWebpAsync_Cancellation_ThrowsOperationCanceledException()
    {
        var pngBytes = CreateSolidImagePng(width: 400, height: 400, color: MagickColors.Red);
        var sut = CreateSut();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await sut.GenerateWebpAsync(pngBytes, TargetWidth, TargetHeight, cts.Token);

        // OperationCanceledException OR TaskCanceledException (derived from OCE).
        await act.Should().ThrowAsync<OperationCanceledException>(
            "callers must be able to abort via the cancellation token");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Epic #3470 Slice 1d-b — focal-point crop overload
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateWebpAsync_FocalOverload_ProducesTargetDimensions()
    {
        var pngBytes = CreateSolidImagePng(width: 900, height: 300, color: MagickColors.Red);
        var sut = CreateSut();

        var output = await sut.GenerateWebpAsync(pngBytes, 300, 300, focalX: 0.2, focalY: 0.8, CancellationToken.None);

        using var decoded = new MagickImage(output);
        decoded.Width.Should().Be(300u);
        decoded.Height.Should().Be(300u);
    }

    [Fact]
    public async Task GenerateWebpAsync_CenterFocal_IsByteIdenticalToThreeArgOverload()
    {
        // The 3-arg overload delegates to the focal one with (0.5, 0.5), and the
        // center path keeps the original Extent(Center) crop — so a center focal
        // must reproduce the legacy output exactly (no re-render drift).
        var pngBytes = CreateSolidImagePng(width: 800, height: 600, color: MagickColors.Green);
        var sut = CreateSut();

        var legacy = await sut.GenerateWebpAsync(pngBytes, TargetWidth, TargetHeight, CancellationToken.None);
        var centered = await sut.GenerateWebpAsync(pngBytes, TargetWidth, TargetHeight, 0.5, 0.5, CancellationToken.None);

        centered.Should().Equal(legacy);
    }

    [Theory]
    [InlineData(-0.01, 0.5)]
    [InlineData(1.01, 0.5)]
    [InlineData(0.5, -0.01)]
    [InlineData(0.5, 1.01)]
    public async Task GenerateWebpAsync_OutOfRangeFocal_ThrowsArgumentException(double focalX, double focalY)
    {
        var pngBytes = CreateSolidImagePng(width: 400, height: 400, color: MagickColors.Red);
        var sut = CreateSut();

        var act = async () => await sut.GenerateWebpAsync(pngBytes, TargetWidth, TargetHeight, focalX, focalY, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GenerateWebpAsync_NonFiniteFocal_ThrowsArgumentException()
    {
        var pngBytes = CreateSolidImagePng(width: 400, height: 400, color: MagickColors.Red);
        var sut = CreateSut();

        var actNan = async () => await sut.GenerateWebpAsync(pngBytes, TargetWidth, TargetHeight, double.NaN, 0.5, CancellationToken.None);
        var actInf = async () => await sut.GenerateWebpAsync(pngBytes, TargetWidth, TargetHeight, 0.5, double.PositiveInfinity, CancellationToken.None);

        await actNan.Should().ThrowAsync<ArgumentException>();
        await actInf.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GenerateWebpAsync_HorizontalFocal_KeepsTheFocusedHalf()
    {
        // 900x300 source, left half red / right half blue. A 300x300 crop must slide
        // horizontally with the focal X: focal 0 keeps the red left edge, focal 1 the
        // blue right edge. Proves the focal point drives the crop window.
        var split = CreateHorizontalSplitPng(900, 300, MagickColors.Red, MagickColors.Blue);
        var sut = CreateSut();

        var left = await sut.GenerateWebpAsync(split, 300, 300, focalX: 0.0, focalY: 0.5, CancellationToken.None);
        var right = await sut.GenerateWebpAsync(split, 300, 300, focalX: 1.0, focalY: 0.5, CancellationToken.None);

        left.Should().NotEqual(right, "different focal points must crop different regions");
        DominantIsRed(left).Should().BeTrue("focal 0 keeps the red left edge");
        DominantIsRed(right).Should().BeFalse("focal 1 keeps the blue right edge");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static WebpVariantGenerator CreateSut() => new(NullLogger<WebpVariantGenerator>.Instance);

    private static byte[] CreateHorizontalSplitPng(int width, int height, MagickColor left, MagickColor right)
    {
        using var image = new MagickImage(left, (uint)width, (uint)height);
        using var rightHalf = new MagickImage(right, (uint)(width / 2), (uint)height);
        image.Composite(rightHalf, width / 2, 0, CompositeOperator.Over);
        image.Format = MagickFormat.Png;
        using var ms = new MemoryStream();
        image.Write(ms);
        return ms.ToArray();
    }

    private static bool DominantIsRed(byte[] webp)
    {
        using var decoded = new MagickImage(webp);
        using var pixels = decoded.GetPixels();
        var color = pixels.GetPixel((int)(decoded.Width / 2), (int)(decoded.Height / 2)).ToColor();
        color.Should().NotBeNull();
        return color!.R > color.B;
    }

    private static byte[] CreateSolidImagePng(int width, int height, MagickColor color)
    {
        using var image = new MagickImage(color, (uint)width, (uint)height);
        image.Format = MagickFormat.Png;
        using var ms = new MemoryStream();
        image.Write(ms);
        return ms.ToArray();
    }

    private static byte[] CreateSolidImageJpeg(int width, int height, MagickColor color)
    {
        using var image = new MagickImage(color, (uint)width, (uint)height);
        image.Format = MagickFormat.Jpeg;
        image.Quality = 90;
        using var ms = new MemoryStream();
        image.Write(ms);
        return ms.ToArray();
    }
}
