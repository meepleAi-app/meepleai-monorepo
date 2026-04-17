namespace Api.Services.ImageProcessing;

/// <summary>
/// Result of image preprocessing (resize, format conversion, quality reduction).
/// </summary>
internal record ProcessedImage(byte[] Data, string MediaType, int Width, int Height, long SizeBytes);

/// <summary>
/// Options for image preprocessing (max dimensions, size limits, format conversion).
/// </summary>
internal record ImageProcessingOptions(
    int MaxWidth = 1024, int MaxHeight = 1024,
    long MaxSizeBytes = 5_000_000, bool ConvertToJpeg = true);

/// <summary>
/// Preprocesses images for LLM vision input (resize, compress, format conversion).
/// </summary>
internal interface IImagePreprocessor
{
    /// <summary>
    /// Process an image: resize if needed, convert format, enforce size limits.
    /// </summary>
    Task<ProcessedImage> ProcessAsync(byte[] imageData, string mediaType, ImageProcessingOptions? options = null);

    /// <summary>
    /// Detect the media type of an image from its magic bytes.
    /// Returns null if the format is not recognized.
    /// </summary>
    string? DetectMediaType(byte[] data);
}
