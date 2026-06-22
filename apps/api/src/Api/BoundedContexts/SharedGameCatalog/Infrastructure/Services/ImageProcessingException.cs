namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;

/// <summary>
/// Thrown by <see cref="IWebpVariantGenerator"/> when the source image bytes
/// cannot be decoded — either because they are corrupted, truncated, or encoded
/// in a format Magick.NET does not recognize (PNG, JPEG, WebP, GIF, BMP, TIFF
/// are the bundled decoders).
/// </summary>
/// <remarks>
/// Surfaces as a typed domain exception so callers (the cover enrichment
/// pipeline) can distinguish "bad upstream payload" from "infrastructure error"
/// and apply the appropriate retry / quarantine policy. Per CLAUDE.md pitfall
/// #2568, we never leak <see cref="System.InvalidOperationException"/> from
/// the infrastructure layer for input-shape failures.
/// </remarks>
public sealed class ImageProcessingException : Exception
{
    public ImageProcessingException(string message) : base(message) { }

    public ImageProcessingException(string message, Exception inner) : base(message, inner) { }
}
