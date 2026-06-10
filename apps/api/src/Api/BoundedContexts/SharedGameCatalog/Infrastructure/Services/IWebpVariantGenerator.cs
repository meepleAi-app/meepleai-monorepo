namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;

/// <summary>
/// Generates resized + cropped WebP variants of source cover images. Production
/// callers pass a 200x300 target (2:3 portrait) matching the BoardGameGeek
/// thumbnail convention. Used by the Wikidata L2 cover enrichment pipeline
/// (issue #1823) to normalize Wikimedia Commons originals (PNG/JPEG/WebP/GIF/
/// BMP/TIFF) before R2 upload.
/// </summary>
/// <remarks>
/// <para>
/// Implementation choice (ADR DEC-3d, locked sess.46h post-spike): SixLabors.
/// ImageSharp 3.x — managed C#, no native deps. The fully-managed runtime
/// avoids the platform-specific shared-library headaches of SkiaSharp /
/// Magick.NET on Linux Alpine container images.
/// </para>
/// <para>
/// Lifetime: register as a SINGLETON via DI. ImageSharp itself is thread-safe
/// for the read-only encoder/decoder pipeline and the generator holds no
/// per-call state — see DI registration in
/// <c>SharedGameCatalogServiceExtensions.AddSharedGameCatalogContext</c>.
/// </para>
/// </remarks>
public interface IWebpVariantGenerator
{
    /// <summary>
    /// Generates a WebP variant of the original image, cropped to the requested
    /// aspect ratio (centered crop) and resized to fit within the target
    /// dimensions.
    /// </summary>
    /// <param name="originalImage">
    /// Source image bytes. Auto-detected formats: PNG, JPEG, WebP, GIF, BMP,
    /// TIFF (all supported decoders bundled by ImageSharp 3.1.12).
    /// </param>
    /// <param name="width">Target width in pixels (must be &gt; 0).</param>
    /// <param name="height">Target height in pixels (must be &gt; 0).</param>
    /// <param name="ct">Cancellation token propagated to the encoder.</param>
    /// <returns>WebP-encoded byte array (quality 85).</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="originalImage"/> is null/empty or the
    /// dimensions are not strictly positive.
    /// </exception>
    /// <exception cref="ImageProcessingException">
    /// Thrown when the source bytes are unreadable, corrupted, or encoded in an
    /// unsupported format.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="ct"/> fires before completion.
    /// </exception>
    Task<byte[]> GenerateWebpAsync(byte[] originalImage, int width, int height, CancellationToken ct);
}
