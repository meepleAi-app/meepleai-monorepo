using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;

/// <summary>
/// <see cref="IWebpVariantGenerator"/> backed by SixLabors.ImageSharp 3.1.12.
/// Produces aspect-ratio-preserving center crops at the requested dimensions
/// and encodes the result as WebP quality 85 — the empirical sweet spot for
/// cover thumbnails (visually indistinguishable from quality 90 at ~30% smaller
/// payload).
/// </summary>
/// <remarks>
/// ADR DEC-3d: managed C# only, no native deps. ImageSharp 3.1.12 bundles
/// PNG/JPEG/WebP/GIF/BMP/TIFF decoders and the libwebp-compatible encoder used
/// here. Singleton-safe — see DI registration in
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

        Image image;
        try
        {
            // LoadAsync auto-detects PNG/JPEG/WebP/GIF/BMP/TIFF from the
            // magic-byte header — see ImageSharp.Formats.IImageFormatDetector.
            using var sourceStream = new MemoryStream(originalImage, writable: false);
            image = await Image.LoadAsync(sourceStream, ct).ConfigureAwait(false);
        }
        catch (UnknownImageFormatException ex)
        {
            _logger.LogWarning(ex,
                "Failed to decode source image: unknown/unsupported format ({Bytes} bytes).",
                originalImage.Length);
            throw new ImageProcessingException(
                "Source image format is not recognized or supported.", ex);
        }
        catch (InvalidImageContentException ex)
        {
            _logger.LogWarning(ex,
                "Failed to decode source image: corrupted content ({Bytes} bytes).",
                originalImage.Length);
            throw new ImageProcessingException(
                "Source image content is corrupted or truncated.", ex);
        }

        try
        {
            // ResizeMode.Crop = aspect-ratio-preserving CENTER crop, then resize
            // to fit the target Size exactly. This is the BoardGameGeek
            // thumbnail convention reproduced for #1823 cover normalization.
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Crop,
                Size = new Size(width, height),
            }));

            using var outputStream = new MemoryStream();
            await image.SaveAsWebpAsync(
                outputStream,
                new WebpEncoder { Quality = WebpQuality },
                ct).ConfigureAwait(false);

            return outputStream.ToArray();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
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
