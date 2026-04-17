using SkiaSharp;

namespace Api.Services.ImageProcessing;

/// <summary>
/// SkiaSharp-based image preprocessor for LLM vision input.
/// Handles resize, JPEG conversion, and size enforcement.
/// </summary>
internal class SkiaImagePreprocessor : IImagePreprocessor
{
    private const int FallbackJpegQuality = 60;
    private const int DefaultJpegQuality = 85;

    /// <inheritdoc/>
    public Task<ProcessedImage> ProcessAsync(byte[] imageData, string mediaType, ImageProcessingOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(imageData);
        if (imageData.Length == 0)
        {
            throw new ArgumentException("Image data is empty", nameof(imageData));
        }

        options ??= new ImageProcessingOptions();

        using var original = SKBitmap.Decode(imageData)
            ?? throw new InvalidOperationException("Failed to decode image data");

        var targetWidth = original.Width;
        var targetHeight = original.Height;

        // Resize if exceeds max dimensions (maintain aspect ratio)
        if (targetWidth > options.MaxWidth || targetHeight > options.MaxHeight)
        {
            var widthRatio = (double)options.MaxWidth / targetWidth;
            var heightRatio = (double)options.MaxHeight / targetHeight;
            var ratio = Math.Min(widthRatio, heightRatio);

            targetWidth = (int)(targetWidth * ratio);
            targetHeight = (int)(targetHeight * ratio);
        }

        using var resized = (targetWidth != original.Width || targetHeight != original.Height)
            ? original.Resize(new SKImageInfo(targetWidth, targetHeight), SKSamplingOptions.Default)
            : original;

        if (resized == null)
        {
            throw new InvalidOperationException("Failed to resize image");
        }

        // Encode as JPEG (or original format if conversion not requested)
        var encodeFormat = options.ConvertToJpeg ? SKEncodedImageFormat.Jpeg : GetFormat(mediaType);
        var outputMediaType = options.ConvertToJpeg ? "image/jpeg" : mediaType;

        using var image = SKImage.FromBitmap(resized);
        var encoded = image.Encode(encodeFormat, DefaultJpegQuality);

        // If still too large after resize, reduce quality
        if (encoded.Size > options.MaxSizeBytes && encodeFormat == SKEncodedImageFormat.Jpeg)
        {
            encoded.Dispose();
            encoded = image.Encode(SKEncodedImageFormat.Jpeg, FallbackJpegQuality);
            outputMediaType = "image/jpeg";
        }

        var outputData = encoded.ToArray();

        var result = new ProcessedImage(
            outputData,
            outputMediaType,
            targetWidth,
            targetHeight,
            outputData.LongLength);

        encoded.Dispose();

        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public string? DetectMediaType(byte[] data)
    {
        if (data == null || data.Length < 4)
        {
            return null;
        }

        // JPEG: FF D8 FF
        if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
        {
            return "image/jpeg";
        }

        // PNG: 89 50 4E 47 (‰PNG)
        if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
        {
            return "image/png";
        }

        // WebP: RIFF....WEBP (bytes 0-3 = RIFF, bytes 8-11 = WEBP)
        if (data.Length >= 12
            && data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46    // RIFF
            && data[8] == 0x57 && data[9] == 0x45 && data[10] == 0x42 && data[11] == 0x50)  // WEBP
        {
            return "image/webp";
        }

        return null;
    }

    private static SKEncodedImageFormat GetFormat(string mediaType) =>
        mediaType.ToLowerInvariant() switch
        {
            "image/png" => SKEncodedImageFormat.Png,
            "image/webp" => SKEncodedImageFormat.Webp,
            "image/gif" => SKEncodedImageFormat.Gif,
            _ => SKEncodedImageFormat.Jpeg,
        };
}
