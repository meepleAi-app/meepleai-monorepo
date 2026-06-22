using ImageMagick;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;

/// <summary>
/// <see cref="IWebpVariantGenerator"/> backed by Magick.NET 14.x
/// (<c>Magick.NET-Q8-AnyCPU</c>, Apache 2.0). Produces aspect-ratio-preserving
/// center crops at the requested dimensions and encodes the result as WebP
/// quality 85 — the empirical sweet spot for cover thumbnails (visually
/// indistinguishable from quality 90 at ~30% smaller payload).
/// </summary>
/// <remarks>
/// ADR DEC-3d-1 (LOCKED 2026-06-20, issue #2055 Phase G AC-G2):
/// migrated from SixLabors.ImageSharp 3.1.12 to Magick.NET because ImageSharp
/// 3.x is governed by the Six Labors Split License (commercial use requires a
/// paid license, incompatible with MeepleAI's proprietary licensing) and
/// 2.1.x is upstream EOL. Magick.NET ships native binaries (~70MB) but the
/// runtime image already pulls Tesseract/Docnet, so the footprint impact is
/// acceptable. Singleton-safe — see DI registration in
/// <see cref="DependencyInjection.SharedGameCatalogServiceExtensions"/>.
/// </remarks>
internal sealed class WebpVariantGenerator : IWebpVariantGenerator
{
    /// <summary>
    /// WebP encoder quality. 85 is the project standard for cover thumbnails
    /// (matches <c>GamebookPhotoStorageService</c> JPEG quality and produces
    /// ~30% smaller payloads than quality 90 with no visible difference at the
    /// 200x300 target resolution).
    /// </summary>
    private const int WebpQuality = 85;

    private readonly ILogger<WebpVariantGenerator> _logger;

    public WebpVariantGenerator(ILogger<WebpVariantGenerator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<byte[]> GenerateWebpAsync(
        byte[] originalImage,
        int width,
        int height,
        CancellationToken ct)
    {
        if (originalImage is null || originalImage.Length == 0)
        {
            throw new ArgumentException(
                "Source image bytes are required.",
                nameof(originalImage));
        }

        if (width <= 0)
        {
            throw new ArgumentException(
                $"Target width must be > 0 (got {width}).",
                nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentException(
                $"Target height must be > 0 (got {height}).",
                nameof(height));
        }

        ct.ThrowIfCancellationRequested();

        MagickImage image;
        try
        {
            // Magick.NET auto-detects PNG/JPEG/WebP/GIF/BMP/TIFF from the
            // magic-byte header. Wrap the synchronous constructor in Task.Run
            // so callers preserve the async signature contract.
            using var sourceStream = new MemoryStream(originalImage, writable: false);
            image = await Task.Run(() => new MagickImage(sourceStream), ct).ConfigureAwait(false);
        }
        catch (MagickException ex)
        {
            _logger.LogWarning(ex,
                "Failed to decode source image ({Bytes} bytes).",
                originalImage.Length);
            throw new ImageProcessingException(
                "Source image format is not recognized, unsupported, or corrupted.", ex);
        }

        try
        {
            ct.ThrowIfCancellationRequested();

            // FillArea=true + Extent(Center) reproduces ImageSharp's
            // ResizeMode.Crop semantics: aspect-ratio-preserving CENTER crop
            // that lands on EXACT target dimensions. This is the
            // BoardGameGeek thumbnail convention reproduced for #1823
            // cover normalization.
            image.Resize(new MagickGeometry((uint)width, (uint)height) { FillArea = true });
            image.Extent((uint)width, (uint)height, Gravity.Center);

            image.Format = MagickFormat.WebP;
            image.Quality = WebpQuality;

            using var outputStream = new MemoryStream();
            await Task.Run(() => image.Write(outputStream), ct).ConfigureAwait(false);

            return outputStream.ToArray();
        }
        catch (MagickException ex)
        {
            _logger.LogWarning(ex,
                "Failed to resize/encode WebP variant at {Width}x{Height}.",
                width, height);
            throw new ImageProcessingException(
                $"Failed to generate WebP variant: {ex.Message}", ex);
        }
        finally
        {
            image.Dispose();
        }
    }
}
