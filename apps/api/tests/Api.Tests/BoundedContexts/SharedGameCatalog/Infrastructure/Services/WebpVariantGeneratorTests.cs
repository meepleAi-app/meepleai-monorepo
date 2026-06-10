using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xunit;

// SUT exception type clashes with SixLabors.ImageSharp.ImageProcessingException
// (a transient parent type from the imaging pipeline). Alias the SUT type so
// tests unambiguously target our domain exception.
using DomainImageProcessingException = Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services.ImageProcessingException;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure.Services;

/// <summary>
/// Unit tests for <see cref="WebpVariantGenerator"/> — issue #1823 M6.
/// Verifies aspect-ratio-preserving center crop + WebP encoding via
/// SixLabors.ImageSharp 3.1.12 (ADR DEC-3d: managed C#, no native deps).
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
        var pngBytes = await CreateSolidImagePngAsync(width: 800, height: 600, color: Color.Red);
        var sut = CreateSut();

        var output = await sut.GenerateWebpAsync(pngBytes, TargetWidth, TargetHeight, CancellationToken.None);

        output.Should().NotBeNullOrEmpty();
        using var decoded = Image.Load(output);
        decoded.Width.Should().Be(TargetWidth,
            "ResizeMode.Crop must enforce exact target dimensions");
        decoded.Height.Should().Be(TargetHeight,
            "ResizeMode.Crop must enforce exact target dimensions");
    }

    [Fact]
    public async Task GenerateWebpAsync_SquareImage_CropsCenterTo200x300()
    {
        // 600x600 source → 200x300 output: ImageSharp.Crop preserves the 2:3 aspect
        // ratio by clipping the wider/taller edges (centered) before resizing.
        var pngBytes = await CreateSolidImagePngAsync(width: 600, height: 600, color: Color.Blue);
        var sut = CreateSut();

        var output = await sut.GenerateWebpAsync(pngBytes, TargetWidth, TargetHeight, CancellationToken.None);

        using var decoded = Image.Load(output);
        decoded.Width.Should().Be(TargetWidth);
        decoded.Height.Should().Be(TargetHeight);
    }

    [Fact]
    public async Task GenerateWebpAsync_PortraitImage_ResizesAndCropsTo200x300()
    {
        // 400x800 → 200x300: source aspect 1:2 vs target 2:3, requires crop top/bottom
        // (since source is "taller" than target ratio).
        var pngBytes = await CreateSolidImagePngAsync(width: 400, height: 800, color: Color.Green);
        var sut = CreateSut();

        var output = await sut.GenerateWebpAsync(pngBytes, TargetWidth, TargetHeight, CancellationToken.None);

        using var decoded = Image.Load(output);
        decoded.Width.Should().Be(TargetWidth);
        decoded.Height.Should().Be(TargetHeight);
    }

    [Fact]
    public async Task GenerateWebpAsync_DefaultDimensions_200x300_AspectRatio()
    {
        // Explicit test for the canonical cover thumbnail (200x300, 2:3 portrait).
        // This is the dimension production callers will pass per #1823 spec.
        var pngBytes = await CreateSolidImagePngAsync(width: 1000, height: 1500, color: Color.Yellow);
        var sut = CreateSut();

        var output = await sut.GenerateWebpAsync(pngBytes, 200, 300, CancellationToken.None);

        using var decoded = Image.Load(output);
        decoded.Width.Should().Be(200);
        decoded.Height.Should().Be(300);
    }

    [Fact]
    public async Task GenerateWebpAsync_OutputIsWebpFormat()
    {
        // WebP magic bytes per RFC 6386: "RIFF" at offset 0, "WEBP" at offset 8.
        // This is the cheapest way to verify the encoder ran (vs JPEG/PNG fallback).
        var pngBytes = await CreateSolidImagePngAsync(width: 400, height: 400, color: Color.Black);
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
        // ImageSharp auto-detects input format. Verify JPEG → WebP works
        // (production sources are PNG/JPEG/WebP per Wikimedia Commons).
        var jpegBytes = await CreateSolidImageJpegAsync(width: 800, height: 600, color: Color.Purple);
        var sut = CreateSut();

        var output = await sut.GenerateWebpAsync(jpegBytes, TargetWidth, TargetHeight, CancellationToken.None);

        output.Should().NotBeNullOrEmpty();
        System.Text.Encoding.ASCII.GetString(output, 0, 4).Should().Be("RIFF");
        System.Text.Encoding.ASCII.GetString(output, 8, 4).Should().Be("WEBP");
        using var decoded = Image.Load(output);
        decoded.Width.Should().Be(TargetWidth);
        decoded.Height.Should().Be(TargetHeight);
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
        var pngBytes = await CreateSolidImagePngAsync(width: 100, height: 100, color: Color.Red);
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
        var pngBytes = await CreateSolidImagePngAsync(width: 100, height: 100, color: Color.Red);
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

        await act.Should().ThrowAsync<DomainImageProcessingException>(
            "unreadable sources should surface as a typed domain exception, not raw ImageSharp errors");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Cancellation
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateWebpAsync_Cancellation_ThrowsOperationCanceledException()
    {
        var pngBytes = await CreateSolidImagePngAsync(width: 400, height: 400, color: Color.Red);
        var sut = CreateSut();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await sut.GenerateWebpAsync(pngBytes, TargetWidth, TargetHeight, cts.Token);

        // OperationCanceledException OR TaskCanceledException (derived from OCE).
        await act.Should().ThrowAsync<OperationCanceledException>(
            "callers must be able to abort via the cancellation token");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static WebpVariantGenerator CreateSut() => new(NullLogger<WebpVariantGenerator>.Instance);

    private static async Task<byte[]> CreateSolidImagePngAsync(int width, int height, Color color)
    {
        using var image = new Image<Rgba32>(width, height);
        image.Mutate(x => x.BackgroundColor(color));
        using var ms = new MemoryStream();
        await image.SaveAsync(ms, new PngEncoder());
        return ms.ToArray();
    }

    private static async Task<byte[]> CreateSolidImageJpegAsync(int width, int height, Color color)
    {
        using var image = new Image<Rgba32>(width, height);
        image.Mutate(x => x.BackgroundColor(color));
        using var ms = new MemoryStream();
        await image.SaveAsync(ms, new JpegEncoder { Quality = 90 });
        return ms.ToArray();
    }
}
