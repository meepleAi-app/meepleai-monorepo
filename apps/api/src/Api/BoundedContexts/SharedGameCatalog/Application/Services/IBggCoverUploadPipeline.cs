namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Issue #2947 — uploads a downloaded BGG cover image to R2 under a
/// deterministic key so <see cref="CoverUrlResolver"/> can reconstruct it.
/// <para>
/// Unlike <c>CoverR2UploadPipeline</c> (Wikidata) and <c>PdfCoverUploadPipeline</c>
/// which store a suffix-stripped DB key and append a suffix at read time, the
/// BGG cover keeps its ORIGINAL image extension (BGG serves jpg/png, not webp).
/// The returned key is therefore the FULL physical object key
/// (<c>bgg-covers/{bggId}/cover{extension}</c>) and the resolver's L2.5 branch
/// passes it verbatim to <c>GetPresignedUrlForRawKeyAsync</c> with NO suffix.
/// </para>
/// </summary>
internal interface IBggCoverUploadPipeline
{
    /// <summary>
    /// Uploads <paramref name="imageBytes"/> to R2 as
    /// <c>bgg-covers/{bggId}/cover{extension}</c> with an immutable cache-control
    /// header, then returns that exact key — the value to persist on
    /// <c>SharedGameEntity.BggCoverR2Key</c>.
    /// </summary>
    /// <param name="bggId">BoardGameGeek game id; the cover namespace.</param>
    /// <param name="imageBytes">The downloaded cover image bytes. Must be non-null and non-empty.</param>
    /// <param name="extension">Dot-prefixed lowercase extension (e.g. <c>.jpg</c>);
    /// null/empty/invalid normalizes to <c>.jpg</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The full deterministic physical key persisted to the DB.</returns>
    /// <exception cref="System.ArgumentException">When <paramref name="imageBytes"/> is null or empty.</exception>
    /// <exception cref="Amazon.S3.AmazonS3Exception">When the underlying S3 client fails.</exception>
    /// <exception cref="System.OperationCanceledException">When <paramref name="ct"/> signals cancellation.</exception>
    Task<string> UploadAsync(int bggId, byte[] imageBytes, string extension, CancellationToken ct);
}
