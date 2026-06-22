namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;

/// <summary>
/// Issue #1823 Phase B (ADR DEC-3h) — uploads WebP-encoded cover bytes to the
/// Cloudflare R2 bucket with a deterministic per-game key, returning the
/// suffix-stripped key suitable for storage in
/// <c>SharedGameEntity.WikidataCoverR2Key</c>.
/// <para>
/// The pipeline writes to the physical R2 object key
/// <c>covers/{gameId}/cover.webp</c> (with <c>.webp</c> suffix), but the value
/// returned to the caller is <c>covers/{gameId}/cover</c> (WITHOUT
/// <c>.webp</c>). This asymmetry matches the contract enforced by
/// <see cref="Api.BoundedContexts.SharedGameCatalog.Application.Services.CoverUrlResolver"/>:
/// CoverUrlResolver appends <c>".webp"</c> when constructing presigned URLs
/// (see <c>CoverUrlResolver.cs:73-79</c>). Storing the full path
/// <c>covers/.../cover.webp</c> in the DB would cause a double-suffix bug
/// (<c>covers/.../cover.webp.webp</c> → 404).
/// </para>
/// <para>
/// All uploads share the same key per <c>gameId</c>, so re-publishing simply
/// overwrites the existing object. Cloudflare CDN cache stays warm because
/// the path never changes; invalidate via Cloudflare cache purge if needed.
/// </para>
/// </summary>
internal interface ICoverR2UploadPipeline
{
    /// <summary>
    /// Uploads <paramref name="webpBytes"/> to R2 as
    /// <c>covers/{gameId}/cover.webp</c> with
    /// <c>Content-Type: image/webp</c> and
    /// <c>Cache-Control: public, max-age=31536000, immutable</c> (ADR DEC-3h).
    /// </summary>
    /// <param name="gameId">The shared game id; also the cover namespace.</param>
    /// <param name="webpBytes">The encoded WebP image. Must be non-null and non-empty.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The DB-safe key WITHOUT the <c>.webp</c> suffix (e.g.
    /// <c>covers/abc-def/cover</c>). Save this in
    /// <see cref="Api.Infrastructure.Entities.SharedGameCatalog.SharedGameEntity"/>.<c>WikidataCoverR2Key</c>.
    /// </returns>
    /// <exception cref="System.ArgumentException">
    /// Thrown when <paramref name="webpBytes"/> is null or empty.
    /// </exception>
    /// <exception cref="Amazon.S3.AmazonS3Exception">
    /// Thrown when the underlying S3 client fails; callers decide whether to retry.
    /// </exception>
    /// <exception cref="System.OperationCanceledException">
    /// Thrown if <paramref name="ct"/> signals cancellation.
    /// </exception>
    Task<string> UploadAsync(Guid gameId, byte[] webpBytes, CancellationToken ct);
}
