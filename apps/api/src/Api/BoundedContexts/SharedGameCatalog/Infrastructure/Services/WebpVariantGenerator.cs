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

    public Task<byte[]> GenerateWebpAsync(
        byte[] originalImage,
        int width,
        int height,
        CancellationToken ct) =>
        GenerateWebpAsync(originalImage, width, height, focalX: 0.5, focalY: 0.5, ct);

    public async Task<byte[]> GenerateWebpAsync(
        byte[] originalImage,
        int width,
        int height,
        double focalX,
        double focalY,
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

        EnsureFocalInRange(focalX, nameof(focalX));
        EnsureFocalInRange(focalY, nameof(focalY));

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

            // FillArea=true reproduces ImageSharp's ResizeMode.Crop "cover" semantics:
            // aspect-ratio-preserving fill (both dimensions >= target). The crop that
            // lands on EXACT target dimensions is then positioned by the focal point
            // (center by default — the BoardGameGeek thumbnail convention, #1823).
            image.Resize(new MagickGeometry((uint)width, (uint)height) { FillArea = true });
            ApplyFocalCrop(image, (uint)width, (uint)height, focalX, focalY);

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

    /// <summary>
    /// Crops the aspect-filled image to exactly <paramref name="width"/>×<paramref name="height"/>,
    /// positioning the window on the focal point. Center (0.5, 0.5) uses
    /// <c>Extent(Center)</c> so it is byte-identical to the legacy center crop; any other
    /// focal uses an offset <c>Crop</c>.
    /// </summary>
    private static void ApplyFocalCrop(MagickImage image, uint width, uint height, double focalX, double focalY)
    {
        if (focalX.Equals(0.5) && focalY.Equals(0.5))
        {
            image.Extent(width, height, Gravity.Center);
            return;
        }

        var cropX = FocalOffset(focalX, image.Width, width);
        var cropY = FocalOffset(focalY, image.Height, height);
        image.Crop(new MagickGeometry(cropX, cropY, width, height));
        image.ResetPage();
    }

    /// <summary>
    /// Top-left offset of a <paramref name="targetDim"/>-sized crop window centered on the
    /// focal point within a <paramref name="filledDim"/>-sized image, clamped so the window
    /// stays fully inside the image (which is &gt;= target after the fill resize).
    /// </summary>
    private static int FocalOffset(double focal, uint filledDim, uint targetDim)
    {
        var ideal = (int)Math.Round(focal * filledDim, MidpointRounding.AwayFromZero) - (int)(targetDim / 2);
        var max = (int)filledDim - (int)targetDim;
        return Math.Clamp(ideal, 0, Math.Max(0, max));
    }

    private static void EnsureFocalInRange(double value, string paramName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0.0 || value > 1.0)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "Focal point must be a finite value in [0, 1].");
        }
    }
}
